namespace Puma
{
    internal enum ExternalSymbolKind
    {
        Type,
        Function
    }

    internal sealed record ExternalSymbol(string Name, ExternalSymbolKind Kind, bool ReturnsOwnedObject = false);
}
