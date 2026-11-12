namespace Sp156.Ingest;

/// <summary>Linha com dado inválido. Informa só a linha e a coluna, nunca o conteúdo da célula.</summary>
public sealed class LinhaInvalidaException : Exception
{
    public LinhaInvalidaException(int linha, string coluna, string mensagem)
        : base(mensagem)
    {
        Linha = linha;
        Coluna = coluna;
    }

    public int Linha { get; }

    public string Coluna { get; }
}
