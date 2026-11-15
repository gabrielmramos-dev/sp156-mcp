using Sp156.Mcp;
using static Sp156.Ingest.Tests.PlanilhaDeTeste;

namespace Sp156.Ingest.Tests;

public sealed class DescricaoDoDatasetTests : IDisposable
{
    private readonly string _pasta = Directory.CreateTempSubdirectory("sp156-descricao-").FullName;

    private string Db => Path.Combine(_pasta, "pedidos.db");

    public void Dispose() => Directory.Delete(_pasta, recursive: true);

    // Pedido sintético: só as colunas informadas variam; as outras têm um valor fixo.
    private static Pedido P(
        int mes = 1, string status = "Finalizada", string servico = "Serviço fixo",
        string? distrito = "Distrito fixo", string? prefeitura = "Prefeitura fixa", string? meio = "Meio fixo") =>
        new(new DateOnly(2025, mes, 10), new DateOnly(2025, 6, 1), meio, "Tema fixo", "Assunto fixo", servico, status, "Órgão fixo", prefeitura, distrito);

    private static IEnumerable<Pedido> Vezes(int n, Pedido pedido) => Enumerable.Repeat(pedido, n);

    private static Grupo G(string valor, long pedidos) => new(valor, pedidos);

    private DescricaoDoDataset Descrever()
    {
        using var conexao = BancoSomenteLeitura.Abrir(Db);
        return DescricaoDoDataset.Descrever(conexao, Catalogo.Carregar(conexao), Limitacoes.Medir(conexao));
    }

    private DescricaoDoDataset DescreverDe(IEnumerable<Pedido> pedidos)
    {
        GravadorSqlite.Gravar(pedidos, Db);
        return Descrever();
    }

    private DescricaoDoDataset DescreverFixture()
    {
        using (var xlsx = AbrirFixture())
            GravadorSqlite.Gravar(LeitorSolicitacoes.Ler(xlsx), Db);
        return Descrever();
    }

    [Fact]
    public void Na_fixture_total_periodo_e_limitacoes_vem_do_banco()
    {
        var d = DescreverFixture();

        Assert.Multiple(
            () => Assert.Equal(30, d.TotalLinhas),
            () => Assert.Equal(new Periodo("2025-01-06", "2025-03-18"), d.Periodo),
            () => Assert.Equal(4, d.Limitacoes.Count),
            () => Assert.Contains("10,00% das linhas (3 de 30)", d.Limitacoes[2]));
    }

    [Fact]
    public void Na_fixture_por_status_tem_Finalizada_27_e_outros_3()
    {
        Assert.Equal(new[] { G("Finalizada", 27), G("outros (agrupados)", 3) }, DescreverFixture().PorStatus);
    }

    [Fact]
    public void Na_fixture_por_mes_tem_os_tres_meses_em_ordem()
    {
        Assert.Equal(new[] { G("2025-01", 11), G("2025-02", 9), G("2025-03", 10) }, DescreverFixture().PorMes);
    }

    [Fact]
    public void Na_fixture_valores_distintos_no_catalogo_e_sem_valor()
    {
        var d = DescreverFixture();

        Assert.Multiple(
            () => Assert.Equal(new ContagemPorColuna(7, 8, 8, 4, 7, 5, 5, 6), d.ValoresDistintos),
            () => Assert.Equal(new ContagemPorColuna(2, 1, 1, 1, 2, 1, 1, 1), d.ValoresNoCatalogo),
            () => Assert.Equal(new LinhasSemValor(11, 11, 1), d.LinhasSemValor));
    }

    [Fact]
    public void Por_status_ordena_por_pedidos_decrescente_e_empate_por_valor_crescente()
    {
        var d = DescreverDe(Vezes(7, P(status: "B")).Concat(Vezes(9, P(status: "C"))).Concat(Vezes(7, P(status: "A"))));

        Assert.Equal(new[] { G("C", 9), G("A", 7), G("B", 7) }, d.PorStatus);
    }

    [Fact]
    public void Empate_usa_ordem_ordinal_do_dotnet_e_nao_a_ordem_do_banco()
    {
        // O SQLite agrupa em ordem de bytes UTF-8 ("Ａ" U+FF21 antes de "😀" U+1F600); a ordem ordinal UTF-16
        // do .NET é a inversa (o par substituto 0xD83D vem antes de 0xFF21). O desempate é o do .NET.
        var d = DescreverDe(Vezes(5, P(status: "Ａ")).Concat(Vezes(5, P(status: "😀"))));

        Assert.Equal(new[] { G("😀", 5), G("Ａ", 5) }, d.PorStatus);
    }

    [Fact]
    public void Por_mes_fica_em_ordem_cronologica_e_suprime_mes_com_menos_de_5()
    {
        var d = DescreverDe(Vezes(6, P(mes: 3)).Concat(Vezes(2, P(mes: 2))).Concat(Vezes(5, P(mes: 1))));

        Assert.Equal(new[] { G("2025-01", 5), G("2025-03", 6), G("outros (agrupados)", 2) }, d.PorMes);
    }

    [Fact]
    public void Valores_raros_contam_em_distintos_mas_nao_no_catalogo_e_nulos_contam_em_sem_valor()
    {
        var d = DescreverDe(
            Vezes(5, P(servico: "Comum"))
                .Concat(Vezes(1, P(servico: "Raro", distrito: null, prefeitura: null)))
                .Concat(Vezes(2, P(servico: "Outro raro", distrito: null, meio: null))));

        Assert.Multiple(
            () => Assert.Equal(3, d.ValoresDistintos.Servico),
            () => Assert.Equal(1, d.ValoresNoCatalogo.Servico),
            () => Assert.Equal(1, d.ValoresDistintos.Distrito),
            () => Assert.Equal(new LinhasSemValor(1, 3, 2), d.LinhasSemValor));
    }
}
