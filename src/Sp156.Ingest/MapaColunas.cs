using System.Globalization;
using System.Text;

namespace Sp156.Ingest;

/// <summary>Posição de cada coluna obrigatória, resolvida pelo nome do cabeçalho.</summary>
internal sealed class MapaColunas
{
    public static IReadOnlyList<string> Obrigatorias { get; } = new[]
    {
        "Data de Abertura", "Data de Finalização", "Meio de Comunicação", "Tema", "Assunto",
        "Serviço", "Status", "Órgão", "Prefeitura Operacional", "Distrito",
    };

    private readonly Dictionary<string, int> _posicoes;

    private MapaColunas(Dictionary<string, int> posicoes) => _posicoes = posicoes;

    public int Quantidade => _posicoes.Count;

    public int Posicao(string coluna) => _posicoes[coluna];

    public static MapaColunas Criar(IReadOnlyList<string?> cabecalhos)
    {
        // Comparação exata, mas com os dois lados em NFC.
        var normalizados = cabecalhos.Select(c => c?.Normalize(NormalizationForm.FormC)).ToList();

        var posicoes = new Dictionary<string, int>(StringComparer.Ordinal);
        var ausentes = new List<string>();
        var duplicadas = new List<string>();
        foreach (var coluna in Obrigatorias)
        {
            var alvo = coluna.Normalize(NormalizationForm.FormC);
            var achadas = Enumerable.Range(0, normalizados.Count)
                .Where(i => string.Equals(normalizados[i], alvo, StringComparison.Ordinal))
                .ToList();

            if (achadas.Count == 0) ausentes.Add(coluna);
            else if (achadas.Count > 1) duplicadas.Add($"{coluna} ({achadas.Count} ocorrências)");
            else posicoes[coluna] = achadas[0];
        }

        if (ausentes.Count > 0)
            throw new ColunaObrigatoriaAusenteException(ausentes, MensagemDeAusentes(ausentes, cabecalhos));

        if (duplicadas.Count > 0)
            throw new InvalidDataException($"Cabeçalho obrigatório duplicado: {string.Join(", ", duplicadas)}.");

        return new MapaColunas(posicoes);
    }

    // A mensagem nunca ecoa o conteúdo do arquivo: só a contagem de cabeçalhos e, por coluna ausente,
    // variantes que diferem do nome obrigatório apenas por acento ou caixa.
    private static string MensagemDeAusentes(List<string> ausentes, IReadOnlyList<string?> cabecalhos)
    {
        var itens = ausentes.Select(coluna =>
        {
            var parecidos = cabecalhos
                .Where(c => c is not null && Dobrar(c) == Dobrar(coluna))
                .Distinct()
                .ToList();
            return parecidos.Count == 0 ? coluna : $"{coluna} (parecidos: {string.Join(", ", parecidos)})";
        });
        var encontrados = cabecalhos.Count(c => !string.IsNullOrEmpty(c));
        return $"Colunas obrigatórias ausentes: {string.Join(", ", itens)}. Cabeçalhos encontrados: {encontrados}.";
    }

    // Sem acento e sem diferença de caixa.
    private static string Dobrar(string texto)
    {
        var sb = new StringBuilder();
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString().ToUpperInvariant();
    }
}
