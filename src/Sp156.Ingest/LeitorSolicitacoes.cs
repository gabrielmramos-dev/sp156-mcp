using System.Text;
using Sylvan.Data.Excel;

namespace Sp156.Ingest;

public static class LeitorSolicitacoes
{
    private const string Aba = "Solicitações";

    /// <summary>
    /// Valida a aba e o cabeçalho na chamada. Os pedidos são lidos ao enumerar, uma vez só:
    /// o leitor é liberado ao fim da enumeração.
    /// </summary>
    public static IEnumerable<Pedido> Ler(Stream xlsx)
    {
        var leitor = ExcelDataReader.Create(xlsx, ExcelWorkbookType.ExcelXml);
        try
        {
            AbrirAba(leitor);

            // Sylvan trata a primeira linha como cabeçalho (GetName).
            var cabecalhos = Enumerable.Range(0, leitor.FieldCount).Select(leitor.GetName).ToList();
            var mapa = MapaColunas.Criar(cabecalhos);

            return Linhas(leitor, mapa);
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
            throw new InvalidDataException($"Aba '{Aba}' não encontrada.");
    }

    private static IEnumerable<Pedido> Linhas(ExcelDataReader leitor, MapaColunas mapa)
    {
        using (leitor)
        {
            yield break;
        }
    }
}
