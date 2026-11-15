using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Sp156.Mcp;

/// <summary>Números e datas do banco que entram nos textos de <see cref="Limitacoes"/>.</summary>
public sealed record MedidasDoBanco(
    DateOnly DataAberturaMinima,
    DateOnly DataAberturaMaxima,
    DateOnly DataFinalizacaoMaxima,
    long TotalLinhas,
    long LinhasDuplicadas);

/// <summary>
/// O campo <c>limitacoes</c> de todas as ferramentas (spec M2, §6.4, decisão 14): os textos são constantes
/// fixas; os números e as datas são calculados do banco.
/// </summary>
public static class Limitacoes
{
    public const string TextoCobertura =
        "Os dados do SP156 se referem exclusivamente às solicitações registradas pela população. A Prefeitura pode executar um serviço sem ter recebido a solicitação correspondente; por isso o volume de pedidos não mede a quantidade de serviços executados nem os problemas existentes na cidade.";

    /// <summary>{0}: menor <c>data_abertura</c>; {1}: maior <c>data_abertura</c>.</summary>
    public const string TextoPeriodo =
        "Este conjunto cobre apenas as solicitações abertas de {0} a {1}. Não permite concluir nada sobre outros períodos.";

    /// <summary>{0}: percentual com duas casas e vírgula; {1}: linhas duplicadas; {2}: total de linhas.</summary>
    public const string TextoDuplicatas =
        "A unidade é a linha registrada. Os dados não têm ID de protocolo e {0}% das linhas ({1} de {2}) são idênticas a outra linha em todas as colunas guardadas; as contagens são de linhas registradas, não de problemas ou de pessoas distintas.";

    /// <summary>{0}: maior <c>data_finalizacao</c>.</summary>
    public const string TextoStatus =
        "O status é um retrato de data desconhecida, não anterior a {0} (data da última finalização no arquivo); a data de extração não foi medida.";

    private const string FormatoData = "yyyy-MM-dd";

    // Duplicatas = total menos as combinações distintas das 10 colunas. DISTINCT trata nulos como iguais.
    private const string Consulta = """
        SELECT min(data_abertura), max(data_abertura), max(data_finalizacao), count(*),
               (SELECT count(*) FROM (SELECT DISTINCT data_abertura, data_finalizacao, meio_comunicacao, tema, assunto,
                                                      servico, status, orgao, prefeitura_operacional, distrito FROM pedidos))
        FROM pedidos
        """;

    /// <summary>Uma consulta sobre o banco inteiro, sem filtros.</summary>
    public static MedidasDoBanco Medir(SqliteConnection conexao)
    {
        ArgumentNullException.ThrowIfNull(conexao);

        using var comando = conexao.CreateCommand();
        comando.CommandText = Consulta;
        using var leitor = comando.ExecuteReader();
        leitor.Read();

        var total = leitor.GetInt64(3);
        return new MedidasDoBanco(
            Data(leitor.GetString(0)),
            Data(leitor.GetString(1)),
            Data(leitor.GetString(2)),
            total,
            total - leitor.GetInt64(4));
    }

    /// <summary>Os 4 textos comuns, na ordem da spec: cobertura, período, duplicatas e status.</summary>
    public static IReadOnlyList<string> Comuns(MedidasDoBanco medidas)
    {
        ArgumentNullException.ThrowIfNull(medidas);

        var percentual = Math.Round(100m * medidas.LinhasDuplicadas / medidas.TotalLinhas, 2, MidpointRounding.AwayFromZero);
        var invariante = CultureInfo.InvariantCulture;

        return
        [
            TextoCobertura,
            string.Format(invariante, TextoPeriodo, Texto(medidas.DataAberturaMinima), Texto(medidas.DataAberturaMaxima)),
            string.Format(invariante, TextoDuplicatas,
                percentual.ToString("0.00", invariante).Replace('.', ','), medidas.LinhasDuplicadas, medidas.TotalLinhas),
            string.Format(invariante, TextoStatus, Texto(medidas.DataFinalizacaoMaxima)),
        ];
    }

    private static DateOnly Data(string texto) => DateOnly.ParseExact(texto, FormatoData, CultureInfo.InvariantCulture);

    private static string Texto(DateOnly data) => data.ToString(FormatoData, CultureInfo.InvariantCulture);
}
