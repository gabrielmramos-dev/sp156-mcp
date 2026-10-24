using System.Data;
using System.Text;
using Sp156.Ingest;
using Sylvan.Data.Excel;

namespace Sp156.Ingest.Tests;

public class LeitorSolicitacoesTests
{
    private const string Aba = "Solicitações";

    private static readonly string[] CabecalhoCompleto =
    {
        "Data de Abertura", "Data de Finalização", "Meio de Comunicação", "Tema", "Assunto",
        "Serviço", "Status", "Órgão", "Prefeitura Operacional", "Distrito",
    };

    // Planilha mínima criada com o escritor do Sylvan: uma aba com os cabeçalhos dados e uma linha de "x".
    private static MemoryStream Xlsx(string aba, params string[] cabecalhos)
    {
        var tabela = new DataTable();
        foreach (var c in cabecalhos) tabela.Columns.Add(c, typeof(string));
        tabela.Rows.Add(cabecalhos.Select(_ => (object)"x").ToArray());

        var saida = new MemoryStream();
        using (var escritor = ExcelDataWriter.Create(saida, ExcelWorkbookType.ExcelXml))
            escritor.Write(tabela.CreateDataReader(), aba);
        saida.Position = 0;
        return saida;
    }

    [Fact]
    public void Planilha_sem_coluna_obrigatoria_lanca_excecao_na_chamada_de_Ler()
    {
        using var xlsx = Xlsx(Aba, "Data de Abertura", "Tema");

        // Sem enumerar: o contrato é validar o cabeçalho na chamada, não no primeiro MoveNext.
        var ex = Assert.Throws<ColunaObrigatoriaAusenteException>(() => LeitorSolicitacoes.Ler(xlsx));

        Assert.Contains("Status", ex.Ausentes);
    }

    [Fact]
    public void Primeira_linha_de_dado_nao_vaza_na_mensagem()
    {
        using var xlsx = Xlsx(Aba, "05510001", "Rua Fictícia 1");

        var ex = Assert.Throws<ColunaObrigatoriaAusenteException>(() => LeitorSolicitacoes.Ler(xlsx));

        Assert.DoesNotContain("05510001", ex.Message);
        Assert.DoesNotContain("Fictícia", ex.Message);
    }

    [Fact]
    public void Aba_ausente_lanca_InvalidDataException_na_chamada_de_Ler()
    {
        using var xlsx = Xlsx("Outra", CabecalhoCompleto);

        Assert.Throws<InvalidDataException>(() => LeitorSolicitacoes.Ler(xlsx));
    }

    [Fact]
    public void Nome_da_aba_diferencia_maiusculas_de_minusculas()
    {
        using var xlsx = Xlsx("solicitações", CabecalhoCompleto);

        Assert.Throws<InvalidDataException>(() => LeitorSolicitacoes.Ler(xlsx));
    }

    [Fact]
    public void Nome_da_aba_decomposto_nfd_equivale_ao_composto_nfc()
    {
        var nfd = Aba.Normalize(NormalizationForm.FormD);
        Assert.NotEqual(Aba, nfd); // garante que o teste exercita NFD
        using var xlsx = Xlsx(nfd, CabecalhoCompleto);

        var erro = Record.Exception(() => LeitorSolicitacoes.Ler(xlsx).ToList());

        Assert.Null(erro);
    }
}
