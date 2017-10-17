﻿using System;
using System.Linq;
using System.Windows;
using Microsoft.VisualStudio.Shell;
using Spect.Net.Assembler.Assembler;
using Spect.Net.VsPackage.Vsx;
using Spect.Net.VsPackage.Z80Programs;
using Spect.Net.Wpf.Mvvm;
using Task = System.Threading.Tasks.Task;

namespace Spect.Net.VsPackage.Commands
{
    /// <summary>
    /// Run a Z80 program command
    /// </summary>
    [CommandId(0x0800)]
    public class RunZ80ProgramCommand : Z80ProgramCommand
    {
        private AssemblerOutput _output;

        /// <summary>Override this method to define the status query action</summary>
        /// <param name="mc"></param>
        protected override void OnQueryStatus(OleMenuCommand mc)
        {
            base.OnQueryStatus(mc);
            mc.Enabled = !Package.CodeManager.CompilatioInProgress;
        }

        /// <summary>
        /// Override this method to define how to prepare the command on the
        /// main thread of Visual Studio
        /// </summary>
        protected override void PrepareCommandOnMainThread(ref bool cancel)
        {
            // --- Get the item
            GetItem(out var hierarchy, out _);
            if (hierarchy == null)
            {
                cancel = true;
                return;
            }
            Package.CodeManager.CompilatioInProgress = true;
            Package.ApplicationObject.ExecuteCommand("File.SaveAll");
        }

        /// <summary>
        /// Compiles the Z80 code file
        /// </summary>
        protected override async Task ExecuteAsync()
        {
            GetItem(out var hierarchy, out var itemId);
            if (hierarchy == null) return;

            var codeManager = Package.CodeManager;
            var options = Package.Options;

            // --- Step #1: Compile
            _output = codeManager.Compile(hierarchy, itemId);

            if (_output.ErrorCount != 0)
            {
                // --- Compilation completed with errors
                return;
            }

            // --- Step #2: Check non-zero displacements
            if (_output.Segments.Any(s => (s.Displacement ?? 0) != 0) && options.ConfirmNonZeroDisplacement)
            {
                var answer = VsxDialogs.Show("The compiled code contains non-zero displacement" +
                    "value, so the displaced code may fail. Are you sure you want to run the code?",
                    "Non-zero displacement found",
                    MessageBoxButton.YesNo, VsxMessageBoxIcon.Question, 1);
                if (answer == VsxDialogResult.No)
                {
                    return;
                }
            }

            // --- Step #3: Stop the virtual machine if required
            await SwitchToMainThreadAsync();
            var vm = Package.MachineViewModel;
            var machineState = vm.VmState;
            if ((machineState == VmState.Running || machineState == VmState.Paused))
            {
                if (options.ConfirmMachineRestart)
                {
                    var answer = VsxDialogs.Show("Are you sure, you want to restart " +
                        "the ZX Spectrum virtual machine?",
                        "The ZX Spectum virtual machine is running",
                        MessageBoxButton.YesNo, VsxMessageBoxIcon.Question, 1);
                    if (answer == VsxDialogResult.No)
                    {
                        return;
                    }
                }

                // --- Stop the machine and allow 3 frames' time to stop.
                Package.MachineViewModel.StopVmCommand.Execute(null);
                await Task.Delay(60);

                if (vm.VmState != VmState.Stopped)
                {
                    VsxDialogs.Show("The ZX Spectrum virtual machine did not stop.",
                        "Unexpected issue", MessageBoxButton.OK, VsxMessageBoxIcon.Error);
                    return;
                }
            }

            // --- Step #4: Start the virtual machine so that later we can load the program
            vm.StartVmWithCodeCommand.Execute(null);

            const int timeOutInSeconds = 5;
            var counter = 0;

            while (vm.VmState != VmState.Paused && counter < timeOutInSeconds*10)
            {
                await Task.Delay(100);
                counter++;
            }
            if (vm.VmState != VmState.Paused)
            {
                VsxDialogs.Show($"The ZX Spectrum virtual machine did not start within {timeOutInSeconds} seconds.",
                    "Unexpected issue", MessageBoxButton.OK, VsxMessageBoxIcon.Error);
                vm.StopVmCommand.Execute(null);
            }

            // --- Step #5: Inject the code into the memory
            codeManager.InjectCodeIntoVm(_output);

            // --- Step #6: Jump to execute the code
            vm.SpectrumVm.Cpu.Registers.PC = _output.EntryAddress ?? _output.Segments[0].StartAddress;
            vm.StartVmCommand.Execute(null);
        }

        /// <summary>
        /// Override this method to define the completion of successful
        /// command execution on the main thread of Visual Studio
        /// </summary>
        protected override void CompleteOnMainThread()
        {
            Package.ErrorList.Clear();
            if (_output.ErrorCount == 0) return;

            foreach (var error in _output.Errors)
            {
                var errorTask = new ErrorTask
                {
                    Category = TaskCategory.User,
                    ErrorCategory = TaskErrorCategory.Error,
                    HierarchyItem = Package.CodeManager.CurrentHierarchy,
                    Document = ItemPath,
                    Line = error.Line,
                    Column = error.Column,
                    Text = error.ErrorCode == null
                        ? error.Message
                        : $"{error.ErrorCode}: {error.Message}",
                    CanDelete = true
                };
                errorTask.Navigate += ErrorTaskOnNavigate;
                Package.ErrorList.AddErrorTask(errorTask);
            }

            Package.ApplicationObject.ExecuteCommand("View.ErrorList");
        }

        /// <summary>
        /// Override this method to define the action to execute on the main
        /// thread of Visual Studio -- finally
        /// </summary>
        protected override void FinallyOnMainThread()
        {
            Package.CodeManager.CompilatioInProgress = false;
            VsxDialogs.Show("Compilation completed.");
        }

        /// <summary>
        /// Navigate to the sender task.
        /// </summary>
        private void ErrorTaskOnNavigate(object sender, EventArgs eventArgs)
        {
            if (sender is ErrorTask task)
            {
                Package.ErrorList.Navigate(task);
            }
        }
    }
}