namespace Sp156.Ingest.Tests;

/// <summary>
/// Teste que só roda com o arquivo real: a variável SP156_XLSX precisa apontar para um arquivo que
/// existe. Sem isso o teste aparece como "Skipped" (nunca como aprovado) e o CI continua verde.
/// </summary>
public sealed class FactArquivoRealAttribute : FactAttribute
{
    public const string Variavel = "SP156_XLSX";

    public FactArquivoRealAttribute() => Skip = MotivoDoSkip(Caminho);

    public static string? Caminho => Environment.GetEnvironmentVariable(Variavel);

    internal static string? MotivoDoSkip(string? caminho)
    {
        if (string.IsNullOrWhiteSpace(caminho))
            return $"Defina {Variavel} com o caminho do XLSX real para rodar este teste.";

        if (!File.Exists(caminho))
            return $"{Variavel} aponta para um arquivo que não existe.";

        return null;
    }
}
