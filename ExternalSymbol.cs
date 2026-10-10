namespace Puma
{
    internal enum ExternalSymbolKind
    {
        Type,
        Function,
        ValueType,
        Trait,
        Module
    }

    internal sealed record ExternalSymbol(string Name, ExternalSymbolKind Kind, bool ReturnsOwnedObject = false);
}
