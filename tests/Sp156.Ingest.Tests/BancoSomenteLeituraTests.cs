using Microsoft.Data.Sqlite;
using Sp156.Mcp;
using static Sp156.Ingest.Tests.PlanilhaDeTeste;

namespace Sp156.Ingest.Tests;

public sealed class BancoSomenteLeituraTests : IDisposable
{
    private readonly string _pasta = Directory.CreateTempSubdirectory("sp156-mcp-ro-").FullName;

    private string Db => Path.Combine(_pasta, "pedidos.db");

    public void Dispose() => Directory.Delete(_pasta, recursive: true);

    private void GravarFixture()
    {
        using var xlsx = AbrirFixture();
        GravadorSqlite.Gravar(LeitorSolicitacoes.Ler(xlsx), Db);
    }

    [Fact]
    public void Leitura_funciona()
    {
        GravarFixture();

        using var conexao = BancoSomenteLeitura.Abrir(Db);
        using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT count(*) FROM pedidos";

        Assert.Equal(30L, comando.ExecuteScalar());
    }

    [Fact]
    public void Escrita_e_recusada_pelo_banco()
    {
        GravarFixture();
        using var conexao = BancoSomenteLeitura.Abrir(Db);
        using var comando = conexao.CreateCommand();
        comando.CommandText = "DELETE FROM pedidos";

        var ex = Assert.Throws<SqliteException>(() => comando.ExecuteNonQuery());

        Assert.Equal(8, ex.SqliteErrorCode); // SQLITE_READONLY
    }

    [Fact]
    public void Arquivo_do_banco_fica_igual_depois_de_abrir_e_ler()
    {
        GravarFixture();
        var antes = File.ReadAllBytes(Db);

        using (var conexao = BancoSomenteLeitura.Abrir(Db))
        using (var comando = conexao.CreateCommand())
        {
            comando.CommandText = "SELECT count(*) FROM pedidos";
            comando.ExecuteScalar();
        }

        Assert.Equal(antes, File.ReadAllBytes(Db));
        Assert.Equal(new[] { "pedidos.db" }, Directory.GetFileSystemEntries(_pasta).Select(p => Path.GetFileName(p)!).Order());
    }
}
