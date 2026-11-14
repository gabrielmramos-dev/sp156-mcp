namespace Sp156.Mcp;

/// <summary>
/// Validação dos parâmetros das ferramentas que não dependem do banco (spec M2, §6.2, §8 e §10).
/// Entrada inválida vira <see cref="ModelContextProtocol.McpException"/> com mensagem em português.
/// </summary>
public static class Validacao
{
    public static readonly IReadOnlyList<string> Agrupamentos = [.. Catalogo.Colunas, "mes"];

    /// <summary>Data em yyyy-MM-dd estrito; <c>null</c> quando o parâmetro não foi informado.</summary>
    public static DateOnly? Data(string nome, string? texto)
    {
        if (texto is null) return null;

        // Formato exato, sem espaços nas bordas (DateTimeStyles.None): "2025-2-1" e "2025-02-30" são recusados.
        if (DateOnly.TryParseExact(texto, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var data))
            return data;

        throw new ModelContextProtocol.McpException($"{nome} deve estar no formato yyyy-MM-dd (por exemplo, 2025-02-01).");
    }

    /// <summary>O intervalo é fechado; <paramref name="de"/> não pode ser posterior a <paramref name="ate"/>.</summary>
    public static void Intervalo(DateOnly? de, DateOnly? ate)
    {
        if (de > ate)
            throw new ModelContextProtocol.McpException("data_abertura_de não pode ser posterior a data_abertura_ate.");
    }

    public const int LimiteMinimo = 1;
    public const int LimiteMaximo = 100;

    public static int Limite(int limite) =>
        limite is >= LimiteMinimo and <= LimiteMaximo
            ? limite
            : throw new ModelContextProtocol.McpException($"limite deve estar entre {LimiteMinimo} e {LimiteMaximo}.");

    /// <summary>Nome canônico do agrupamento (aceita a Chave do nome); <c>null</c> quando não informado.</summary>
    public static string? AgruparPor(string? valor)
    {
        if (valor is null) return null;

        var chave = Sp156.Ingest.TextoNormalizado.Chave(valor);
        return Agrupamentos.FirstOrDefault(nome => nome == chave)
            ?? throw new ModelContextProtocol.McpException($"agrupar_por deve ser um de: {string.Join(", ", Agrupamentos)}.");
    }
}
