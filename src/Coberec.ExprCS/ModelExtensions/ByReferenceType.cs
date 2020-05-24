namespace Coberec.ExprCS
{
    /// <summary> Reference type. In IL it is denoted as `Type&amp;`, in C# it is usually represented by `ref` keyword. </summary>
    partial class ByReferenceType
    {
        public override string ToString() => $"{Type}&";
    }
}
