using System.Data;
using Sylvan.Data.Excel;

namespace Sp156.Ingest.Tests;

internal static class PlanilhaDeTeste
{
    public const string Aba = "Solicitações";

    // Cabeçalho do arquivo real, com as 5 colunas de endereço.
    public static readonly string[] CabecalhoDoArquivo =
    {
        "Data de Abertura", "Data de Finalização", "Meio de Comunicação", "Tema", "Assunto",
        "Serviço", "Status", "Órgão", "Logradouro", "Número", "CEP", "Latitude", "Longitude",
        "Prefeitura Operacional", "Distrito",
    };

    public static FileStream AbrirFixture() =>
        File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sp156_fixture.xlsx"));

    // Linha alinhada ao cabeçalho: tudo nulo, menos as colunas informadas.
    public static string?[] Linha(params (string Coluna, string? Valor)[] valores)
    {
        var linha = new string?[CabecalhoDoArquivo.Length];
        foreach (var (coluna, valor) in valores)
            linha[Array.IndexOf(CabecalhoDoArquivo, coluna)] = valor;
        return linha;
    }

    // Planilha criada com o escritor do Sylvan: uma aba, o cabeçalho dado e as linhas dadas.
    public static MemoryStream Criar(string aba, string[] cabecalhos, params string?[][] linhas)
    {
        var tabela = new DataTable();
        foreach (var c in cabecalhos) tabela.Columns.Add(c, typeof(string));
        foreach (var l in linhas) tabela.Rows.Add(l.Select(v => (object?)v ?? DBNull.Value).ToArray());

        var saida = new MemoryStream();
        using (var escritor = ExcelDataWriter.Create(saida, ExcelWorkbookType.ExcelXml))
            escritor.Write(tabela.CreateDataReader(), aba);
        saida.Position = 0;
        return saida;
    }

    // Planilha com as duas colunas de data numéricas (double = serial do Excel) e o resto texto.
    // Cada linha vem alinhada a CabecalhoDoArquivo.
    public static MemoryStream CriarComDatas(string aba, params object?[][] linhas)
    {
        var tabela = new DataTable();
        foreach (var c in CabecalhoDoArquivo)
            tabela.Columns.Add(c, c is "Data de Abertura" or "Data de Finalização" ? typeof(double) : typeof(string));
        foreach (var l in linhas) tabela.Rows.Add(l.Select(v => v ?? DBNull.Value).ToArray());

        var saida = new MemoryStream();
        using (var escritor = ExcelDataWriter.Create(saida, ExcelWorkbookType.ExcelXml))
            escritor.Write(tabela.CreateDataReader(), aba);
        saida.Position = 0;
        return saida;
    }

    // Linha completa e válida (datas como serial); os pares informados substituem as colunas.
    public static object?[] LinhaValida(params (string Coluna, object? Valor)[] trocas)
    {
        var linha = new object?[]
        {
            45663d, 45694d, "PORTAL", "Rua e bairro", "Reparos em asfalto", "Tapa-buraco", "Finalizada",
            "SMGI", "Rua Fictícia 1", "101", "05510001", "-23.580001", "-46.730001", "BUTANTA", "VILA SONIA",
        };
        foreach (var (coluna, valor) in trocas)
            linha[Array.IndexOf(CabecalhoDoArquivo, coluna)] = valor;
        return linha;
    }

    // Valores de Logradouro, CEP, Latitude e Longitude da fixture (7+ caracteres, para não casar por acaso).
    public static HashSet<string> ValoresDeEnderecoDaFixture()
    {
        var valores = new HashSet<string>();
        using var xlsx = AbrirFixture();
        using var leitor = ExcelDataReader.Create(xlsx, ExcelWorkbookType.ExcelXml);
        Assert.True(leitor.TryOpenWorksheet(Aba));
        var posicoes = new[] { "Logradouro", "CEP", "Latitude", "Longitude" }
            .Select(nome => Enumerable.Range(0, leitor.FieldCount).First(i => leitor.GetName(i) == nome))
            .ToArray();
        while (leitor.Read())
            foreach (var i in posicoes)
                if (i < leitor.RowFieldCount && leitor.GetString(i).Length >= 7)
                    valores.Add(leitor.GetString(i));
        return valores;
    }

    // Planilha com o cabeçalho completo em que a coluna "Tema" aparece duas vezes. O DataTable não aceita
    // nomes repetidos, então cria "Tema#2" e troca o texto dentro do XLSX.
    public static MemoryStream CriarComCabecalhoDuplicado()
    {
        var cabecalhos = CabecalhoDoArquivo.Append("Tema#2").ToArray();
        using var original = Criar(Aba, cabecalhos, cabecalhos.Select(_ => (string?)"x").ToArray());

        var saida = new MemoryStream();
        using (var de = new System.IO.Compression.ZipArchive(original, System.IO.Compression.ZipArchiveMode.Read))
        using (var para = new System.IO.Compression.ZipArchive(saida, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entrada in de.Entries)
            {
                using var origem = entrada.Open();
                using var leitor = new StreamReader(origem);
                var conteudo = leitor.ReadToEnd().Replace("Tema#2", "Tema");
                using var destino = new StreamWriter(para.CreateEntry(entrada.FullName).Open(), new System.Text.UTF8Encoding(false));
                destino.Write(conteudo);
            }
        }
        saida.Position = 0;
        return saida;
    }
}
