using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;

namespace Sp156.Mcp;

public sealed record Periodo(
    [property: JsonPropertyName("data_abertura_minima")] string DataAberturaMinima,
    [property: JsonPropertyName("data_abertura_maxima")] string DataAberturaMaxima);

public sealed record ContagemPorColuna(
    [property: JsonPropertyName("tema")] long Tema,
    [property: JsonPropertyName("assunto")] long Assunto,
    [property: JsonPropertyName("servico")] long Servico,
    [property: JsonPropertyName("status")] long Status,
    [property: JsonPropertyName("orgao")] long Orgao,
    [property: JsonPropertyName("prefeitura_operacional")] long PrefeituraOperacional,
    [property: JsonPropertyName("distrito")] long Distrito,
    [property: JsonPropertyName("meio_comunicacao")] long MeioComunicacao);

public sealed record LinhasSemValor(
    [property: JsonPropertyName("prefeitura_operacional")] long PrefeituraOperacional,
    [property: JsonPropertyName("distrito")] long Distrito,
    [property: JsonPropertyName("meio_comunicacao")] long MeioComunicacao);

/// <summary>Retorno de <c>descrever_dataset</c> (spec M2, §7). Só agregados, nunca linhas individuais.</summary>
public sealed record DescricaoDoDataset(
    [property: JsonPropertyName("total_linhas")] long TotalLinhas,
    [property: JsonPropertyName("periodo")] Periodo Periodo,
    [property: JsonPropertyName("por_status")] IReadOnlyList<Grupo> PorStatus,
    [property: JsonPropertyName("por_mes")] IReadOnlyList<Grupo> PorMes,
    [property: JsonPropertyName("valores_distintos")] ContagemPorColuna ValoresDistintos,
    [property: JsonPropertyName("valores_no_catalogo")] ContagemPorColuna ValoresNoCatalogo,
    [property: JsonPropertyName("linhas_sem_valor")] LinhasSemValor LinhasSemValor,
    [property: JsonPropertyName("limitacoes")] IReadOnlyList<string> Limitacoes)
{
    // Uma varredura só: os distintos das 8 colunas categóricas (count(DISTINCT) ignora nulos) e os nulos das 3 que os admitem.
    private const string ConsultaColunas = """
        SELECT count(DISTINCT tema), count(DISTINCT assunto), count(DISTINCT servico), count(DISTINCT status),
               count(DISTINCT orgao), count(DISTINCT prefeitura_operacional), count(DISTINCT distrito), count(DISTINCT meio_comunicacao),
               count(*) - count(prefeitura_operacional), count(*) - count(distrito), count(*) - count(meio_comunicacao)
        FROM pedidos
        """;

    private const string ConsultaPorStatus = "SELECT status, count(*) FROM pedidos GROUP BY status";

    // data_abertura é texto yyyy-MM-dd: os 7 primeiros caracteres são o mês yyyy-MM.
    private const string ConsultaPorMes = "SELECT substr(data_abertura, 1, 7), count(*) FROM pedidos GROUP BY 1";

    /// <summary>
    /// Descreve o banco inteiro. <paramref name="medidas"/> dá o total, o período e as limitações;
    /// <paramref name="catalogo"/>, quantos valores de cada coluna aceitam filtro.
    /// </summary>
    public static DescricaoDoDataset Descrever(SqliteConnection conexao, Catalogo catalogo, MedidasDoBanco medidas)
    {
        ArgumentNullException.ThrowIfNull(conexao);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(medidas);

        var porStatus = Grupos(conexao, ConsultaPorStatus)
            .OrderByDescending(g => g.Pedidos)
            .ThenBy(g => g.Valor, StringComparer.Ordinal);
        var porMes = Grupos(conexao, ConsultaPorMes).OrderBy(g => g.Valor, StringComparer.Ordinal);

        using var comando = conexao.CreateCommand();
        comando.CommandText = ConsultaColunas;
        using var leitor = comando.ExecuteReader();
        leitor.Read();

        var noCatalogo = Catalogo.Colunas.Select(c => (long)catalogo.Mapa(c).Values.Sum(originais => originais.Count)).ToArray();

        return new DescricaoDoDataset(
            medidas.TotalLinhas,
            new Periodo(Texto(medidas.DataAberturaMinima), Texto(medidas.DataAberturaMaxima)),
            Supressao.Aplicar(porStatus),
            Supressao.Aplicar(porMes),
            new ContagemPorColuna(leitor.GetInt64(0), leitor.GetInt64(1), leitor.GetInt64(2), leitor.GetInt64(3),
                leitor.GetInt64(4), leitor.GetInt64(5), leitor.GetInt64(6), leitor.GetInt64(7)),
            new ContagemPorColuna(noCatalogo[0], noCatalogo[1], noCatalogo[2], noCatalogo[3],
                noCatalogo[4], noCatalogo[5], noCatalogo[6], noCatalogo[7]),
            new LinhasSemValor(leitor.GetInt64(8), leitor.GetInt64(9), leitor.GetInt64(10)),
            Sp156.Mcp.Limitacoes.Comuns(medidas));
    }

    private static List<Grupo> Grupos(SqliteConnection conexao, string consulta)
    {
        using var comando = conexao.CreateCommand();
        comando.CommandText = consulta;
        using var leitor = comando.ExecuteReader();
        var grupos = new List<Grupo>();
        while (leitor.Read()) grupos.Add(new Grupo(leitor.GetString(0), leitor.GetInt64(1)));
        return grupos;
    }

    private static string Texto(DateOnly data) => data.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
