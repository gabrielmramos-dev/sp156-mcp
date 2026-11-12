using System.Text;
using Sylvan.Data.Excel;

namespace Sp156.Ingest;

public static class LeitorSolicitacoes
{
    private const string Aba = "Solicitações";

    /// <summary>
    /// Aba e cabeçalho são validados na chamada. Os pedidos são lidos ao enumerar, uma vez só, e os
    /// erros de linha (<see cref="LinhaInvalidaException"/>) saem na enumeração. O leitor é liberado
    /// ao fim da enumeração. O <paramref name="xlsx"/> continua sendo do chamador.
    /// </summary>
    public static IEnumerable<Pedido> Ler(Stream xlsx) =>
        LerLinhasBrutas(xlsx).Select(ConversorDePedido.Converter);

    /// <summary>
    /// Percorre a aba e devolve as linhas com dado, sem converter valores. Mesmas validações e
    /// mesmo ciclo de vida de <see cref="Ler"/>. O <paramref name="xlsx"/> continua sendo do chamador.
    /// </summary>
    internal static IEnumerable<LinhaBruta> LerLinhasBrutas(Stream xlsx)
    {
        var (leitor, mapa) = Abrir(xlsx);
        return LinhasBrutas(leitor, mapa);
    }

    private static (ExcelDataReader Leitor, MapaColunas Mapa) Abrir(Stream xlsx)
    {
        var leitor = ExcelDataReader.Create(xlsx, ExcelWorkbookType.ExcelXml);
        try
        {
            AbrirAba(leitor);

            // Sylvan trata a primeira linha como cabeçalho (GetName).
            var cabecalhos = Enumerable.Range(0, leitor.FieldCount).Select(leitor.GetName).ToList();
            return (leitor, MapaColunas.Criar(cabecalhos));
        }
        catch
        {
            leitor.Dispose();
            throw;
        }
    }

    // Mesma regra dos cabeçalhos: comparação exata, com os dois lados em NFC.
    private static void AbrirAba(ExcelDataReader leitor)
    {
        var alvo = Aba.Normalize(NormalizationForm.FormC);
        var nome = leitor.WorksheetNames
            .FirstOrDefault(n => string.Equals(n.Normalize(NormalizationForm.FormC), alvo, StringComparison.Ordinal));

        if (nome is null || !leitor.TryOpenWorksheet(nome))
            throw MarcaDeErro.Marcar(new InvalidDataException($"Aba '{Aba}' não encontrada."));
    }

    private static IEnumerable<LinhaBruta> LinhasBrutas(ExcelDataReader leitor, MapaColunas mapa)
    {
        using (leitor)
        {
            while (leitor.Read())
            {
                // Só as 10 colunas mapeadas são lidas: as de endereço nunca ganham posição.
                var v = MapaColunas.Obrigatorias.Select(c => Celula(leitor, mapa.Posicao(c))).ToArray();

                if (v.All(EmBranco)) continue;

                yield return new LinhaBruta(leitor.RowNumber, v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7], v[8], v[9]);
            }
        }
    }

    // Sem conversão: número vira double (o serial da data), texto fica como veio. Célula que a
    // linha curta não tem, ou vazia no arquivo, vira null.
    private static object? Celula(ExcelDataReader leitor, int posicao)
    {
        if (posicao >= leitor.RowFieldCount) return null;

        return leitor.GetExcelDataType(posicao) switch
        {
            ExcelDataType.Null => null,
            ExcelDataType.Numeric => leitor.GetDouble(posicao),
            _ => leitor.GetString(posicao),
        };
    }

    private static bool EmBranco(object? valor) => valor is null || (valor is string s && string.IsNullOrWhiteSpace(s));
}
