using Sp156.Mcp;

namespace Sp156.Ingest.Tests;

public sealed class ConfiguracaoDoServidorTests : IDisposable
{
    private readonly string _pasta = Directory.CreateTempSubdirectory("sp156-mcp-cfg-").FullName;

    public void Dispose() => Directory.Delete(_pasta, recursive: true);

    private string CriarArquivo(string nome)
    {
        var caminho = Path.Combine(_pasta, nome);
        File.WriteAllText(caminho, "conteúdo qualquer");
        return caminho;
    }

    private static Func<string, string?> Variaveis(params (string Nome, string? Valor)[] pares) =>
        nome => pares.FirstOrDefault(p => p.Nome == nome).Valor;

    [Fact]
    public void Argumento_db_com_arquivo_existente_resolve_o_caminho()
    {
        var banco = CriarArquivo("a.db");

        var (config, erro) = ConfiguracaoDoServidor.Resolver(new[] { "--db", banco }, Variaveis());

        Assert.Null(erro);
        Assert.Equal(banco, config?.CaminhoDoBanco);
    }

    [Fact]
    public void Variavel_SP156_DB_resolve_o_caminho_quando_nao_ha_argumento()
    {
        var banco = CriarArquivo("b.db");

        var (config, erro) = ConfiguracaoDoServidor.Resolver(Array.Empty<string>(), Variaveis(("SP156_DB", banco)));

        Assert.Null(erro);
        Assert.Equal(banco, config?.CaminhoDoBanco);
    }

    [Fact]
    public void Argumento_db_vence_a_variavel_SP156_DB()
    {
        var doArgumento = CriarArquivo("argumento.db");
        var daVariavel = CriarArquivo("variavel.db");

        var (config, erro) = ConfiguracaoDoServidor.Resolver(new[] { "--db", doArgumento }, Variaveis(("SP156_DB", daVariavel)));

        Assert.Null(erro);
        Assert.Equal(doArgumento, config?.CaminhoDoBanco);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sem_argumento_e_sem_variavel_o_erro_explica_como_informar_o_banco(string? variavel)
    {
        var (config, erro) = ConfiguracaoDoServidor.Resolver(Array.Empty<string>(), Variaveis(("SP156_DB", variavel)));

        Assert.Null(config);
        Assert.NotNull(erro);
        Assert.Contains("--db", erro);
        Assert.Contains("SP156_DB", erro);
    }

    [Fact]
    public void Caminho_inexistente_e_erro_e_cita_o_caminho()
    {
        var inexistente = Path.Combine(_pasta, "nao-existe.db");

        var (config, erro) = ConfiguracaoDoServidor.Resolver(new[] { "--db", inexistente }, Variaveis());

        Assert.Null(config);
        Assert.NotNull(erro);
        Assert.Contains(inexistente, erro);
    }

    [Fact]
    public void Argumento_db_sem_valor_e_erro()
    {
        var banco = CriarArquivo("c.db");

        var (config, erro) = ConfiguracaoDoServidor.Resolver(new[] { "--db" }, Variaveis(("SP156_DB", banco)));

        Assert.Null(config);
        Assert.NotNull(erro);
        Assert.Contains("--db", erro);
    }
}
