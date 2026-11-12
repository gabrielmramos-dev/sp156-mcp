using Sp156.Ingest;
using static Sp156.Ingest.Tests.PlanilhaDeTeste;

namespace Sp156.Ingest.Tests;

public class PedidosTests
{
    private static List<Pedido> PedidosDaFixture()
    {
        using var xlsx = AbrirFixture();
        return LeitorSolicitacoes.Ler(xlsx).ToList();
    }

    // --- Fixture v2: critérios de aceitação da spec ---------------------------------------------

    [Fact]
    public void Fixture_v2_devolve_30_pedidos()
    {
        Assert.Equal(30, PedidosDaFixture().Count);
    }

    [Fact]
    public void Primeiro_pedido_tem_as_datas_esperadas()
    {
        var pedidos = PedidosDaFixture();
        Assert.NotEmpty(pedidos);

        Assert.Equal(new DateOnly(2025, 1, 6), pedidos[0].DataAbertura);
        Assert.Equal(new DateOnly(2025, 2, 6), pedidos[0].DataFinalizacao);
    }

    [Fact]
    public void As_3_finalizacoes_vazias_da_fixture_viram_null()
    {
        // 2 abertos/em andamento + 1 Finalizada sem data (spec).
        Assert.Equal(3, PedidosDaFixture().Count(p => p.DataFinalizacao is null));
    }

    [Fact]
    public void Meio_de_comunicacao_nulo_aparece_em_1_pedido()
    {
        Assert.Equal(1, PedidosDaFixture().Count(p => p.MeioComunicacao is null));
    }

    [Fact]
    public void Distrito_nulo_aparece_em_11_pedidos()
    {
        Assert.Equal(11, PedidosDaFixture().Count(p => p.Distrito is null));
    }

    [Fact]
    public void Status_Finalizada_Aberto_EmAndamento_Cancelada_somam_27_1_1_1()
    {
        var porStatus = PedidosDaFixture().GroupBy(p => p.Status).ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(27, porStatus.GetValueOrDefault("Finalizada"));
        Assert.Equal(1, porStatus.GetValueOrDefault("Aberto"));
        Assert.Equal(1, porStatus.GetValueOrDefault("Em Andamento"));
        Assert.Equal(1, porStatus.GetValueOrDefault("Cancelada"));
    }

    [Fact]
    public void Duplicatas_exatas_sao_mantidas_30_linhas_e_27_distintas()
    {
        var pedidos = PedidosDaFixture();

        Assert.Equal(30, pedidos.Count);
        Assert.Equal(27, pedidos.Distinct().Count()); // ADR 0003: não deduplicar
    }

    // --- Erros de linha saem na enumeração, com a linha real da planilha ------------------------

    [Fact]
    public void Linha_sem_data_de_abertura_lanca_na_enumeracao_com_o_numero_da_linha_da_planilha()
    {
        using var xlsx = CriarComDatas(Aba,
            LinhaValida(),
            LinhaValida(),
            LinhaValida(("Data de Abertura", null), ("Tema", "so tema")));

        var pedidos = LeitorSolicitacoes.Ler(xlsx); // validação de aba/cabeçalho passa: não lança aqui

        var ex = Assert.Throws<LinhaInvalidaException>(() => pedidos.ToList());
        Assert.Equal(4, ex.Linha); // cabeçalho = 1, 3ª linha de dado = 4
        Assert.Equal("Data de Abertura", ex.Coluna);
        Assert.DoesNotContain("so tema", ex.Message);
    }

    [Fact]
    public void Tema_vazio_lanca_na_enumeracao_sem_ecoar_celulas_da_linha()
    {
        using var xlsx = CriarComDatas(Aba, LinhaValida(("Tema", null)));

        var ex = Assert.Throws<LinhaInvalidaException>(() => LeitorSolicitacoes.Ler(xlsx).ToList());

        Assert.Equal(2, ex.Linha);
        Assert.Equal("Tema", ex.Coluna);
        Assert.DoesNotContain("Fictícia", ex.Message);
        Assert.DoesNotContain("05510001", ex.Message);
    }
}
