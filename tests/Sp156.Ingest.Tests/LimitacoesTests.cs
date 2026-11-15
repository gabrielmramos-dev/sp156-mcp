using Sp156.Mcp;
using static Sp156.Ingest.Tests.PlanilhaDeTeste;

namespace Sp156.Ingest.Tests;

public sealed class LimitacoesTests : IDisposable
{
    private readonly string _pasta = Directory.CreateTempSubdirectory("sp156-limitacoes-").FullName;

    private string Db => Path.Combine(_pasta, "pedidos.db");

    public void Dispose() => Directory.Delete(_pasta, recursive: true);

    private MedidasDoBanco MedirFixture()
    {
        using (var xlsx = AbrirFixture())
            GravadorSqlite.Gravar(LeitorSolicitacoes.Ler(xlsx), Db);
        using var conexao = BancoSomenteLeitura.Abrir(Db);
        return Limitacoes.Medir(conexao);
    }

    // Pedidos sintéticos distintos entre si: só a data de abertura muda.
    private static Pedido P(int dia) =>
        new(new DateOnly(2025, 1, 1).AddDays(dia), new DateOnly(2025, 6, 1), "Meio", "Tema", "Assunto", "Serviço", "Finalizada", "Órgão", null, null);

    [Fact]
    public void Os_textos_fixos_sao_exatamente_os_da_spec()
    {
        Assert.Multiple(
            () => Assert.Equal(
                "Os dados do SP156 se referem exclusivamente às solicitações registradas pela população. A Prefeitura pode executar um serviço sem ter recebido a solicitação correspondente; por isso o volume de pedidos não mede a quantidade de serviços executados nem os problemas existentes na cidade.",
                Limitacoes.TextoCobertura),
            () => Assert.Equal(
                "Este conjunto cobre apenas as solicitações abertas de {0} a {1}. Não permite concluir nada sobre outros períodos.",
                Limitacoes.TextoPeriodo),
            () => Assert.Equal(
                "A unidade é a linha registrada. Os dados não têm ID de protocolo e {0}% das linhas ({1} de {2}) são idênticas a outra linha em todas as colunas guardadas; as contagens são de linhas registradas, não de problemas ou de pessoas distintas.",
                Limitacoes.TextoDuplicatas),
            () => Assert.Equal(
                "O status é um retrato de data desconhecida, não anterior a {0} (data da última finalização no arquivo); a data de extração não foi medida.",
                Limitacoes.TextoStatus));
    }

    [Fact]
    public void Na_fixture_as_medidas_vem_do_banco()
    {
        var medidas = MedirFixture();

        Assert.Equal(
            new MedidasDoBanco(new DateOnly(2025, 1, 6), new DateOnly(2025, 3, 18), new DateOnly(2025, 3, 28), 30, 3),
            medidas);
    }

    [Fact]
    public void Na_fixture_os_quatro_textos_comuns_saem_preenchidos_e_em_ordem()
    {
        var textos = Limitacoes.Comuns(MedirFixture());

        Assert.Equal(
            new[]
            {
                Limitacoes.TextoCobertura,
                "Este conjunto cobre apenas as solicitações abertas de 2025-01-06 a 2025-03-18. Não permite concluir nada sobre outros períodos.",
                "A unidade é a linha registrada. Os dados não têm ID de protocolo e 10,00% das linhas (3 de 30) são idênticas a outra linha em todas as colunas guardadas; as contagens são de linhas registradas, não de problemas ou de pessoas distintas.",
                "O status é um retrato de data desconhecida, não anterior a 2025-03-28 (data da última finalização no arquivo); a data de extração não foi medida.",
            },
            textos);
    }

    [Fact]
    public void Linhas_que_so_diferem_por_nulo_contra_valor_nao_sao_duplicatas_e_nulos_iguais_sao()
    {
        var comDistrito = P(0) with { Distrito = "Sé" };
        GravadorSqlite.Gravar([P(0), P(0), comDistrito], Db);
        using var conexao = BancoSomenteLeitura.Abrir(Db);

        Assert.Equal(1, Limitacoes.Medir(conexao).LinhasDuplicadas);
    }

    [Fact]
    public void Percentual_tem_duas_casas_virgula_e_arredonda_para_longe_de_zero()
    {
        // 1 duplicata em 32 linhas = 3,125%: para longe de zero dá 3,13 (para o par daria 3,12).
        var pedidos = Enumerable.Range(0, 31).Select(P).Append(P(0));
        GravadorSqlite.Gravar(pedidos, Db);
        using var conexao = BancoSomenteLeitura.Abrir(Db);

        var texto = Limitacoes.Comuns(Limitacoes.Medir(conexao))[2];

        Assert.Contains(" 3,13% das linhas (1 de 32) ", texto);
    }

    [Fact]
    public void Numeros_grandes_nao_levam_separador_de_milhar()
    {
        var texto = Limitacoes.Comuns(new MedidasDoBanco(new DateOnly(2025, 1, 1), new DateOnly(2025, 3, 31), new DateOnly(2026, 8, 5), 742904, 419529))[2];

        Assert.Contains(" 56,47% das linhas (419529 de 742904) ", texto);
    }
}
