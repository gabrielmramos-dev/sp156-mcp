using Microsoft.Data.Sqlite;

namespace Sp156.Mcp;

/// <summary>
/// Filtros já validados de uma chamada de ferramenta (spec M2, §6.2): os categóricos convertidos nos
/// valores originais do banco pelo <see cref="Catalogo"/> e o intervalo fechado de data de abertura.
/// Combinados com E. Nenhum valor é interpolado no SQL: tudo vai como parâmetro.
/// </summary>
public sealed class Filtros
{
    private const string FormatoData = "yyyy-MM-dd";

    // Coluna (da lista fixa do Catalogo) para os valores originais; na ordem de Catalogo.Colunas.
    private readonly IReadOnlyList<(string Coluna, IReadOnlyList<string> Originais)> _categoricos;
    private readonly DateOnly? _de;
    private readonly DateOnly? _ate;

    private Filtros(IReadOnlyList<(string, IReadOnlyList<string>)> categoricos, DateOnly? de, DateOnly? ate) =>
        (_categoricos, _de, _ate) = (categoricos, de, ate);

    /// <param name="categoricos">Coluna categórica para o valor informado; <c>null</c> = filtro ausente.</param>
    public static Filtros Criar(Catalogo catalogo, IReadOnlyDictionary<string, string?> categoricos, string? dataAberturaDe, string? dataAberturaAte)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(categoricos);

        foreach (var coluna in categoricos.Keys)
            if (!Catalogo.Colunas.Contains(coluna))
                throw new ArgumentException($"'{coluna}' não é uma coluna categórica.", nameof(categoricos));

        var resolvidos = new List<(string, IReadOnlyList<string>)>();
        foreach (var coluna in Catalogo.Colunas)
            if (categoricos.TryGetValue(coluna, out var valor) && valor is not null)
                resolvidos.Add((coluna, catalogo.Resolver(coluna, valor)));

        var de = Validacao.Data("data_abertura_de", dataAberturaDe);
        var ate = Validacao.Data("data_abertura_ate", dataAberturaAte);
        Validacao.Intervalo(de, ate);

        return new Filtros(resolvidos, de, ate);
    }

    /// <summary>Acrescenta os parâmetros ao comando e devolve a cláusula ("WHERE ...") ou vazio, sem filtros.</summary>
    public string Clausula(SqliteCommand comando)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var condicoes = new List<string>();
        foreach (var (coluna, originais) in _categoricos)
        {
            var nomes = new List<string>();
            for (var i = 0; i < originais.Count; i++)
            {
                var nome = $"${coluna}{i}";
                comando.Parameters.AddWithValue(nome, originais[i]);
                nomes.Add(nome);
            }
            // IN nunca casa NULL: linhas sem valor na coluna ficam fora do filtro.
            condicoes.Add($"{coluna} IN ({string.Join(", ", nomes)})");
        }

        // data_abertura é texto yyyy-MM-dd, que ordena como data.
        if (_de is { } de)
        {
            comando.Parameters.AddWithValue("$data_abertura_de", de.ToString(FormatoData, System.Globalization.CultureInfo.InvariantCulture));
            condicoes.Add("data_abertura >= $data_abertura_de");
        }
        if (_ate is { } ate)
        {
            comando.Parameters.AddWithValue("$data_abertura_ate", ate.ToString(FormatoData, System.Globalization.CultureInfo.InvariantCulture));
            condicoes.Add("data_abertura <= $data_abertura_ate");
        }

        return condicoes.Count == 0 ? "" : "WHERE " + string.Join(" AND ", condicoes);
    }
}
