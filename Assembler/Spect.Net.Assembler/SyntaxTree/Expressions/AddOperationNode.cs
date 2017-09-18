namespace Spect.Net.Assembler.SyntaxTree.Expressions
{
    /// <summary>
    /// This class represents the add operation
    /// </summary>
    public sealed class AddOperationNode : BinaryOperationNode
    {
        /// <summary>
        /// Calculates the result of the binary operation.
        /// </summary>
        /// <param name="evalContext">Evaluation context</param>
        /// <returns>Result of the operation</returns>
        public override ushort Calculate(IEvaluationContext evalContext)
            => (ushort)(LeftOperand.Evaluate(evalContext) 
                + RightOperand.Evaluate(evalContext));
    }
}