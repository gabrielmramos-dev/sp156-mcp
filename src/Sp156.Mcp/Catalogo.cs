using Microsoft.Data.Sqlite;

namespace Sp156.Mcp;

/// <summary>
/// Valores das colunas categóricas que podem ser usados em filtros: para cada coluna, o mapa da
/// <see cref="Sp156.Ingest.TextoNormalizado.Chave"/> para os valores originais do banco. Só entram
/// valores não nulos com pelo menos <see cref="MinimoDeLinhas"/> linhas (spec M2, §6.3).
/// </summary>
public sealed class Catalogo
{
    public const int MinimoDeLinhas = 5;

    public static readonly IReadOnlyList<string> Colunas =
    [
        "tema", "assunto", "servico", "status", "orgao", "prefeitura_operacional", "distrito", "meio_comunicacao",
    ];

    private readonly Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> _mapas;

    private Catalogo(Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> mapas) => _mapas = mapas;

    /// <summary>Lê o banco inteiro, sem filtros: um GROUP BY por coluna.</summary>
    public static Catalogo Carregar(SqliteConnection conexao)
    {
        ArgumentNullException.ThrowIfNull(conexao);

        var mapas = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>();
        foreach (var coluna in Colunas)
        {
            var porChave = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            using var comando = conexao.CreateCommand();
            // O nome da coluna vem da lista fixa acima, nunca da entrada; o mínimo vai como parâmetro.
            comando.CommandText = $"SELECT {coluna} FROM pedidos WHERE {coluna} IS NOT NULL GROUP BY {coluna} HAVING count(*) >= $minimo";
            comando.Parameters.AddWithValue("$minimo", MinimoDeLinhas);

            using var leitor = comando.ExecuteReader();
            while (leitor.Read())
            {
                var original = leitor.GetString(0);
                var chave = Sp156.Ingest.TextoNormalizado.Chave(original);
                if (!porChave.TryGetValue(chave, out var originais)) porChave[chave] = originais = [];
                originais.Add(original);
            }

            mapas[coluna] = porChave.ToDictionary(
                par => par.Key,
                par => (IReadOnlyList<string>)par.Value.Order(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        }

        return new Catalogo(mapas);
    }

    /// <summary>Mapa Chave para os valores originais (em ordem ordinal) de uma coluna categórica.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Mapa(string coluna) =>
        _mapas.TryGetValue(coluna, out var mapa)
            ? mapa
            : throw new ArgumentException($"'{coluna}' não é uma coluna categórica.", nameof(coluna));

    /// <summary>
    /// Converte o valor de um filtro nos valores originais do banco. Lança <see cref="ModelContextProtocol.McpException"/>
    /// se o valor for vazio ou não estiver no catálogo.
    /// </summary>
    public IReadOnlyList<string> Resolver(string coluna, string valor)
    {
        ArgumentNullException.ThrowIfNull(valor);
        var mapa = Mapa(coluna);

        if (string.IsNullOrWhiteSpace(valor))
            throw new ModelContextProtocol.McpException($"'{coluna}' não pode ser vazio.");

        var chave = Sp156.Ingest.TextoNormalizado.Chave(valor);
        if (mapa.TryGetValue(chave, out var originais)) return originais;

        // Só valores do catálogo (5 ou mais linhas) são sugeridos: um valor raro dá o mesmo erro de um inexistente.
        var proximos = mapa
            .SelectMany(par => par.Value.Select(original => (Original: original, Distancia: Distancia(chave, par.Key))))
            .OrderBy(c => c.Distancia)
            .ThenBy(c => c.Original, StringComparer.Ordinal)
            .Take(MaximoDeSugestoes)
            .Select(c => $"'{c.Original}'")
            .ToArray();

        var lista = proximos.Length == 0 ? "nenhum" : string.Join(", ", proximos);
        throw new ModelContextProtocol.McpException(
            $"Valor desconhecido para '{coluna}': '{Cortar(valor)}'. Valores válidos mais próximos: {lista}.");
    }

    private const int MaximoDeSugestoes = 5;
    private const int MaximoDeCaracteresDaEntrada = 100;

    private static string Cortar(string valor)
    {
        if (valor.Length <= MaximoDeCaracteresDaEntrada) return valor;
        // Não corta um par substituto ao meio.
        var fim = char.IsHighSurrogate(valor[MaximoDeCaracteresDaEntrada - 1]) ? MaximoDeCaracteresDaEntrada - 1 : MaximoDeCaracteresDaEntrada;
        return valor[..fim];
    }

    /// <summary>Distância de edição (Levenshtein) entre duas Chaves.</summary>
    private static int Distancia(string a, string b)
    {
        var anterior = new int[b.Length + 1];
        var atual = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) anterior[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            atual[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var custo = a[i - 1] == b[j - 1] ? 0 : 1;
                atual[j] = Math.Min(Math.Min(atual[j - 1] + 1, anterior[j] + 1), anterior[j - 1] + custo);
            }
            (anterior, atual) = (atual, anterior);
        }

        return anterior[b.Length];
    }

    /// <summary>Quantidade de valores originais no catálogo, somando todas as colunas.</summary>
    public int TotalDeValores => _mapas.Values.Sum(mapa => mapa.Values.Sum(originais => originais.Count));
}
