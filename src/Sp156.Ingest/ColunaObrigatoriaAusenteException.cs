namespace Sp156.Ingest;

public sealed class ColunaObrigatoriaAusenteException : Exception
{
    public ColunaObrigatoriaAusenteException(IReadOnlyList<string> ausentes, string mensagem)
        : base(mensagem)
    {
        Ausentes = ausentes;
    }

    public IReadOnlyList<string> Ausentes { get; }
}
