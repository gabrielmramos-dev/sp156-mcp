using Sp156.Mcp;
using static Sp156.Ingest.Tests.PlanilhaDeTeste;

namespace Sp156.Ingest.Tests;

public sealed class DadosDoBancoTests : IDisposable
{
    private readonly string _pasta = Directory.CreateTempSubdirectory("sp156-dados-").FullName;

    private string Db => Path.Combine(_pasta, "pedidos.db");

    public void Dispose() => Directory.Delete(_pasta, recursive: true);

    [Fact]
    public void Nao_abre_o_banco_ao_ser_criado()
    {
        // Caminho inexistente: criar não pode falhar, porque o servidor é registrado antes de qualquer chamada.
        var dados = new DadosDoBanco(new ConfiguracaoDoServidor(Path.Combine(_pasta, "nao-existe.db")));

        Assert.NotNull(dados);
    }

    [Fact]
    public void Catalogo_e_medidas_sao_lidos_uma_vez_e_guardados()
    {
        using (var xlsx = AbrirFixture())
            GravadorSqlite.Gravar(LeitorSolicitacoes.Ler(xlsx), Db);
        var dados = new DadosDoBanco(new ConfiguracaoDoServidor(Db));

        var medidas = dados.Medidas;
        var catalogo = dados.Catalogo;
        File.Delete(Db); // Se lesse de novo, falharia.

        Assert.Multiple(
            () => Assert.Equal(30, medidas.TotalLinhas),
            () => Assert.Same(medidas, dados.Medidas),
            () => Assert.Same(catalogo, dados.Catalogo));
    }
}
