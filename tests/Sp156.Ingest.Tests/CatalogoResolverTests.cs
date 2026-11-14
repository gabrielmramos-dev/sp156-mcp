using ModelContextProtocol;
using Sp156.Mcp;

namespace Sp156.Ingest.Tests;

public sealed class CatalogoResolverTests : IDisposable
{
    private readonly string _pasta = Directory.CreateTempSubdirectory("sp156-resolver-").FullName;

    public void Dispose() => Directory.Delete(_pasta, recursive: true);

    private static Pedido P(string servico, string tema = "Tema fixo") =>
        new(new DateOnly(2025, 1, 1), null, "Meio fixo", tema, "Assunto fixo", servico, "Finalizada", "Órgão fixo", "Prefeitura fixa", "Distrito fixo");

    // Serviços com 5 linhas cada (entram no catálogo), mais os de menos linhas informados.
    private Catalogo CarregarCom(params (string Servico, int Linhas)[] raros)
    {
        string[] validos = ["bixo", "lixa", "lixos", "luxo", "lixo a", "lixeira", "poda", "Poda de árvore"];
        var pedidos = validos.SelectMany(s => Enumerable.Repeat(P(s), 5))
            .Concat(raros.SelectMany(r => Enumerable.Repeat(P(r.Servico), r.Linhas)))
            .Concat(Enumerable.Repeat(P("Tema dividido", tema: "Lixo e limpeza"), 5))
            .Concat(Enumerable.Repeat(P("Tema dividido", tema: "LIXO E LIMPEZA"), 5));

        var db = Path.Combine(_pasta, $"pedidos-{Guid.NewGuid():N}.db");
        GravadorSqlite.Gravar(pedidos, db);
        using var conexao = BancoSomenteLeitura.Abrir(db);
        return Catalogo.Carregar(conexao);
    }

    private static string Erro(Catalogo catalogo, string coluna, string valor) =>
        Assert.Throws<McpException>(() => catalogo.Resolver(coluna, valor)).Message;

    [Theory]
    [InlineData("Poda de árvore")]
    [InlineData("  poda DE  arvore ")]
    public void Valor_casa_pela_chave_e_devolve_o_original(string valor)
    {
        Assert.Equal(new[] { "Poda de árvore" }, CarregarCom().Resolver("servico", valor));
    }

    [Fact]
    public void Chave_com_duas_grafias_devolve_os_dois_originais()
    {
        Assert.Equal(new[] { "LIXO E LIMPEZA", "Lixo e limpeza" }, CarregarCom().Resolver("tema", "lixo e limpeza"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Valor_vazio_ou_so_com_espacos_e_erro(string valor)
    {
        Assert.Equal("'servico' não pode ser vazio.", Erro(CarregarCom(), "servico", valor));
    }

    [Fact]
    public void Valor_desconhecido_lista_ate_5_mais_proximos_por_distancia_e_ordem_alfabetica()
    {
        // Distância 1: bixo, lixa, lixos, luxo; distância 2: lixo a; distância 4 (fora): lixeira, poda.
        Assert.Equal(
            "Valor desconhecido para 'servico': 'lixo'. Valores válidos mais próximos: 'bixo', 'lixa', 'lixos', 'luxo', 'lixo a'.",
            Erro(CarregarCom(), "servico", "lixo"));
    }

    [Fact]
    public void Valor_com_menos_de_5_linhas_da_o_mesmo_erro_de_um_valor_que_nao_existe()
    {
        var comRaro = Erro(CarregarCom(("lixo", 4)), "servico", "lixo");
        var semRaro = Erro(CarregarCom(), "servico", "lixo");

        Assert.Equal(semRaro, comRaro);
    }

    [Fact]
    public void Valor_informado_aparece_cortado_em_100_caracteres()
    {
        var mensagem = Erro(CarregarCom(), "servico", new string('x', 150));

        Assert.StartsWith($"Valor desconhecido para 'servico': '{new string('x', 100)}'. ", mensagem);
    }

    [Fact]
    public void Coluna_sem_valores_no_catalogo_diz_nenhum()
    {
        // Um único serviço, com 4 linhas: a coluna servico fica sem nenhum valor no catálogo.
        var db = Path.Combine(_pasta, "raros.db");
        GravadorSqlite.Gravar(Enumerable.Repeat(P("raro"), 4), db);
        using var conexao = BancoSomenteLeitura.Abrir(db);

        Assert.Equal(
            "Valor desconhecido para 'servico': 'raro'. Valores válidos mais próximos: nenhum.",
            Erro(Catalogo.Carregar(conexao), "servico", "raro"));
    }
}
