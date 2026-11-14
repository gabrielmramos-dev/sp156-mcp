using System.Globalization;
using Microsoft.Data.Sqlite;
using Sp156.Mcp;
using static Sp156.Ingest.Tests.PlanilhaDeTeste;

namespace Sp156.Ingest.Tests;

public sealed class CatalogoTests : IDisposable
{
    private readonly string _pasta = Directory.CreateTempSubdirectory("sp156-catalogo-").FullName;

    private string Db => Path.Combine(_pasta, "pedidos.db");

    public void Dispose() => Directory.Delete(_pasta, recursive: true);

    // Pedido sintético: só as colunas informadas variam; as outras têm um valor fixo.
    private static Pedido P(
        string tema = "Tema fixo", string servico = "Serviço fixo", string status = "Finalizada",
        string? distrito = "Distrito fixo", string? meio = "Meio fixo") =>
        new(new DateOnly(2025, 1, 1), null, meio, tema, "Assunto fixo", servico, status, "Órgão fixo", "Prefeitura fixa", distrito);

    private static IEnumerable<Pedido> Vezes(int n, Pedido pedido) => Enumerable.Repeat(pedido, n);

    private Catalogo CarregarDe(IEnumerable<Pedido> pedidos)
    {
        GravadorSqlite.Gravar(pedidos, Db);
        using var conexao = BancoSomenteLeitura.Abrir(Db);
        return Catalogo.Carregar(conexao);
    }

    private static string[] Chaves(IReadOnlyDictionary<string, IReadOnlyList<string>> mapa) => mapa.Keys.Order(StringComparer.Ordinal).ToArray();

    [Fact]
    public void As_oito_colunas_categoricas_sao_as_da_spec()
    {
        Assert.Equal(
            new[] { "tema", "assunto", "servico", "status", "orgao", "prefeitura_operacional", "distrito", "meio_comunicacao" },
            Catalogo.Colunas);
    }

    [Fact]
    public void Valor_com_5_ou_mais_linhas_entra_e_com_menos_fica_fora()
    {
        var catalogo = CarregarDe(
            Vezes(12, P(servico: "Coleta de lixo"))
                .Concat(Vezes(5, P(servico: "Poda de árvore")))
                .Concat(Vezes(4, P(servico: "Tapa-buraco")))
                .Concat(Vezes(1, P(servico: "Limpeza de boca de lobo"))));

        var mapa = catalogo.Mapa("servico");

        Assert.Equal(new[] { "coleta de lixo", "poda de arvore" }, Chaves(mapa));
        Assert.Equal(new[] { "Poda de árvore" }, mapa["poda de arvore"]);
    }

    [Fact]
    public void Valores_nulos_ficam_fora()
    {
        var catalogo = CarregarDe(Vezes(6, P(distrito: null, meio: null)).Concat(Vezes(5, P(distrito: "Sé", meio: "Telefone"))));

        Assert.Equal(new[] { "se" }, Chaves(catalogo.Mapa("distrito")));
        Assert.Equal(new[] { "telefone" }, Chaves(catalogo.Mapa("meio_comunicacao")));
    }

    [Fact]
    public void Grafias_com_a_mesma_chave_ficam_na_mesma_entrada_e_cada_uma_precisa_de_5_linhas()
    {
        var catalogo = CarregarDe(
            Vezes(5, P(tema: "Lixo e limpeza"))
                .Concat(Vezes(5, P(tema: "LIXO E LIMPEZA")))
                .Concat(Vezes(1, P(tema: "lixo e  limpeza"))));

        var mapa = catalogo.Mapa("tema");

        Assert.Equal(new[] { "lixo e limpeza" }, Chaves(mapa));
        Assert.Equal(new[] { "LIXO E LIMPEZA", "Lixo e limpeza" }, mapa["lixo e limpeza"]);
    }

    [Fact]
    public void Total_de_valores_soma_os_originais_de_todas_as_colunas()
    {
        // 5 pedidos iguais: cada uma das 8 colunas tem 1 valor; o tema tem 2 grafias da mesma Chave.
        var catalogo = CarregarDe(Vezes(5, P(tema: "Lixo")).Concat(Vezes(5, P(tema: "LIXO"))));

        Assert.Equal(9, catalogo.TotalDeValores);
    }

    [Fact]
    public void Na_fixture_so_Finalizada_tem_5_ou_mais_linhas_de_status()
    {
        using (var xlsx = AbrirFixture())
            GravadorSqlite.Gravar(LeitorSolicitacoes.Ler(xlsx), Db);
        using var conexao = BancoSomenteLeitura.Abrir(Db);

        var mapa = Catalogo.Carregar(conexao).Mapa("status");

        Assert.Equal(new[] { "finalizada" }, Chaves(mapa));
        Assert.Equal(new[] { "Finalizada" }, mapa["finalizada"]);
    }

    [FactArquivoReal]
    public void No_arquivo_real_1344_valores_entram_256_ficam_fora_e_nao_ha_colisao()
    {
        using (var xlsx = File.OpenRead(FactArquivoRealAttribute.Caminho!))
            GravadorSqlite.Gravar(LeitorSolicitacoes.Ler(xlsx), Db);
        using var conexao = BancoSomenteLeitura.Abrir(Db);

        var catalogo = Catalogo.Carregar(conexao);

        long distintos = 0;
        foreach (var coluna in Catalogo.Colunas)
        {
            using var comando = conexao.CreateCommand();
            comando.CommandText = $"SELECT count(DISTINCT {coluna}) FROM pedidos";
            distintos += Convert.ToInt64(comando.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        var colisoes = Catalogo.Colunas.Sum(c => catalogo.Mapa(c).Values.Count(originais => originais.Count > 1));

        Assert.Multiple(
            () => Assert.Equal(1344, catalogo.TotalDeValores),
            () => Assert.Equal(256, distintos - catalogo.TotalDeValores),
            () => Assert.Equal(0, colisoes));
    }
}
