### Version 1.2.0

__This is the first version with tracked changes__

__FEATURE__: Code injection logic has been changed:
  * When the Spectrum VM is stopped, the IDE gives a warning to pause it.
  * When the VM is paused, the IDE injects the code to the VM.
  * When the VM is stopped, the IDE starts the VM, pauses it, and then injects the code.

__FEATURE__: The Disassembly tool window has a new command, JUMP (__`J`__ _`hhhh`_). 
This command works only when the Spectrum VM is paused. It sets the Program Counter (PC) to the
specified _`hhhh`_ address. When the Spectum VM continues running &mdash; after the Start command &mdash;
it will carry on from the specified address.
 
__FEATURE__: Now, you can use the __Add VM State to project__ command in the Spectrum emulator tool window
toolbar to save the Spectrum VM state directly into the project hierarchy.

__FEATURE__: You can execute the __Load VM State file__ command from the context menu of a `.vmstate` file in Solution Explorer.