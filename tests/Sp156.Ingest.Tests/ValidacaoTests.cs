using ModelContextProtocol;
using Sp156.Mcp;

namespace Sp156.Ingest.Tests;

public class ValidacaoTests
{
    private static string Erro(Action acao) => Assert.Throws<McpException>(acao).Message;

    [Theory]
    [InlineData("2025-02-01", 2025, 2, 1)]
    [InlineData("2024-02-29", 2024, 2, 29)]
    public void Data_valida_vira_DateOnly(string texto, int ano, int mes, int dia)
    {
        Assert.Equal(new DateOnly(ano, mes, dia), Validacao.Data("data_abertura_de", texto));
    }

    [Fact]
    public void Data_nao_informada_e_null()
    {
        Assert.Null(Validacao.Data("data_abertura_de", null));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("2025-2-1")]
    [InlineData("2025-02-30")]
    [InlineData("2025-13-01")]
    [InlineData("01/02/2025")]
    [InlineData("2025-02-01 ")]
    [InlineData("2025-02-01T00:00")]
    public void Data_malformada_ou_inexistente_e_erro(string texto)
    {
        Assert.Equal(
            "data_abertura_ate deve estar no formato yyyy-MM-dd (por exemplo, 2025-02-01).",
            Erro(() => Validacao.Data("data_abertura_ate", texto)));
    }

    [Fact]
    public void Intervalo_invertido_e_erro()
    {
        Assert.Equal(
            "data_abertura_de não pode ser posterior a data_abertura_ate.",
            Erro(() => Validacao.Intervalo(new DateOnly(2025, 2, 2), new DateOnly(2025, 2, 1))));
    }

    [Fact]
    public void Intervalo_de_um_dia_ou_aberto_e_aceito()
    {
        var dia = new DateOnly(2025, 2, 1);
        Validacao.Intervalo(dia, dia);
        Validacao.Intervalo(dia, null);
        Validacao.Intervalo(null, dia);
        Validacao.Intervalo(null, null);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    [InlineData(100)]
    public void Limite_de_1_a_100_e_aceito(int limite)
    {
        Assert.Equal(limite, Validacao.Limite(limite));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Limite_fora_de_1_a_100_e_erro(int limite)
    {
        Assert.Equal("limite deve estar entre 1 e 100.", Erro(() => Validacao.Limite(limite)));
    }

    [Fact]
    public void Agrupamentos_sao_as_oito_colunas_e_mes()
    {
        Assert.Equal(
            new[] { "tema", "assunto", "servico", "status", "orgao", "prefeitura_operacional", "distrito", "meio_comunicacao", "mes" },
            Validacao.Agrupamentos);
    }

    [Theory]
    [InlineData("mes", "mes")]
    [InlineData("Mes", "mes")]
    [InlineData("mês", "mes")]
    [InlineData(" Distrito ", "distrito")]
    [InlineData("prefeitura_operacional", "prefeitura_operacional")]
    public void Agrupar_por_aceita_o_nome_ou_a_chave_do_nome(string valor, string esperado)
    {
        Assert.Equal(esperado, Validacao.AgruparPor(valor));
    }

    [Fact]
    public void Agrupar_por_nao_informado_e_null()
    {
        Assert.Null(Validacao.AgruparPor(null));
    }

    [Theory]
    [InlineData("bairro")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("prefeitura operacional")]
    public void Agrupar_por_desconhecido_e_erro(string valor)
    {
        Assert.Equal(
            "agrupar_por deve ser um de: tema, assunto, servico, status, orgao, prefeitura_operacional, distrito, meio_comunicacao, mes.",
            Erro(() => Validacao.AgruparPor(valor)));
    }
}
