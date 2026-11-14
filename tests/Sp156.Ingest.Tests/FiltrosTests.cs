using System.Globalization;
using Microsoft.Data.Sqlite;
using ModelContextProtocol;
using Sp156.Mcp;

namespace Sp156.Ingest.Tests;

public sealed class FiltrosTests : IDisposable
{
    private readonly string _pasta = Directory.CreateTempSubdirectory("sp156-filtros-").FullName;
    private readonly SqliteConnection _conexao;
    private readonly Catalogo _catalogo;

    private static Pedido P(string servico, string? distrito = "Sé", string tema = "Tema fixo", string data = "2025-02-10") =>
        new(DateOnly.Parse(data, CultureInfo.InvariantCulture), null, "Meio fixo", tema, "Assunto fixo", servico, "Finalizada", "Órgão fixo", "Prefeitura fixa", distrito);

    private static IEnumerable<Pedido> Vezes(int n, Pedido p) => Enumerable.Repeat(p, n);

    public FiltrosTests()
    {
        var pedidos = Vezes(7, P("Coleta de lixo", distrito: "Sé"))
            .Concat(Vezes(5, P("Coleta de lixo", distrito: "Mooca")))
            .Concat(Vezes(6, P("Coleta de lixo", distrito: null)))
            .Concat(Vezes(5, P("Coleta d'água")))
            .Concat(Vezes(5, P("Poda", tema: "Lixo e limpeza")))
            .Concat(Vezes(5, P("Poda", tema: "LIXO E LIMPEZA")))
            // 5 linhas, para "Datado" entrar no catálogo.
            .Concat([P("Datado", data: "2025-01-31"), P("Datado", data: "2025-02-01"), P("Datado", data: "2025-02-15"), P("Datado", data: "2025-02-28"), P("Datado", data: "2025-03-01")]);
        var db = Path.Combine(_pasta, "pedidos.db");
        GravadorSqlite.Gravar(pedidos, db);
        _conexao = BancoSomenteLeitura.Abrir(db);
        _catalogo = Catalogo.Carregar(_conexao);
    }

    public void Dispose()
    {
        _conexao.Dispose();
        Directory.Delete(_pasta, recursive: true);
    }

    private Filtros Criar(string? de = null, string? ate = null, params (string Coluna, string? Valor)[] categoricos) =>
        Filtros.Criar(_catalogo, categoricos.ToDictionary(c => c.Coluna, c => c.Valor), de, ate);

    private (long Contagem, string Sql) Contar(Filtros filtros)
    {
        using var comando = _conexao.CreateCommand();
        comando.CommandText = "SELECT count(*) FROM pedidos " + filtros.Clausula(comando);
        return (Convert.ToInt64(comando.ExecuteScalar(), CultureInfo.InvariantCulture), comando.CommandText);
    }

    [Fact]
    public void Sem_filtros_conta_tudo_e_nao_ha_clausula()
    {
        var (contagem, sql) = Contar(Criar());

        Assert.Equal(38, contagem);
        Assert.Equal("SELECT count(*) FROM pedidos ", sql);
    }

    [Fact]
    public void Filtro_ausente_null_e_ignorado()
    {
        Assert.Equal(38, Contar(Criar(categoricos: ("distrito", null))).Contagem);
    }

    [Fact]
    public void Filtro_categorico_casa_pela_chave()
    {
        Assert.Equal(18, Contar(Criar(categoricos: ("servico", "coleta DE lixo"))).Contagem);
    }

    [Fact]
    public void Filtros_sao_combinados_com_E_e_nulo_nunca_casa()
    {
        Assert.Equal(7, Contar(Criar(categoricos: [("servico", "Coleta de lixo"), ("distrito", "se")])).Contagem);
    }

    [Fact]
    public void Chave_com_duas_grafias_casa_as_duas()
    {
        Assert.Equal(10, Contar(Criar(categoricos: ("tema", "lixo e limpeza"))).Contagem);
    }

    [Fact]
    public void Valor_com_aspas_vai_como_parametro_e_nao_aparece_no_sql()
    {
        var (contagem, sql) = Contar(Criar(categoricos: ("servico", "coleta d'agua")));

        Assert.Equal(5, contagem);
        Assert.DoesNotContain("água", sql);
        Assert.DoesNotContain("'", sql);
    }

    [Theory]
    [InlineData("2025-02-01", "2025-02-28", 3)]
    [InlineData("2025-02-01", "2025-02-01", 1)]
    [InlineData("2025-03-01", null, 1)]
    [InlineData(null, "2025-01-31", 1)]
    public void Intervalo_de_datas_e_fechado(string? de, string? ate, long esperado)
    {
        Assert.Equal(esperado, Contar(Criar(de, ate, ("servico", "datado"))).Contagem);
    }

    [Fact]
    public void Datas_nao_aparecem_no_sql()
    {
        var (_, sql) = Contar(Criar("2025-02-01", "2025-02-28"));

        Assert.DoesNotContain("2025", sql);
    }

    [Fact]
    public void Entrada_invalida_vira_McpException()
    {
        Assert.Multiple(
            () => Assert.Throws<McpException>(() => Criar(categoricos: ("servico", " "))),
            () => Assert.Throws<McpException>(() => Criar(categoricos: ("servico", "x' OR '1'='1"))),
            () => Assert.Throws<McpException>(() => Criar(de: "2025-2-1")),
            () => Assert.Throws<McpException>(() => Criar(de: "2025-02-02", ate: "2025-02-01")));
    }
}
