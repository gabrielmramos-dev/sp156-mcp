using System.Diagnostics;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Sp156.Ingest;
using Xunit.Abstractions;

namespace Sp156.Ingest.Tests;

public class GravadorSqliteArquivoRealTests
{
    private readonly ITestOutputHelper _saida;

    public GravadorSqliteArquivoRealTests(ITestOutputHelper saida) => _saida = saida;

    [FactArquivoReal]
    public void Grava_o_arquivo_real_e_o_banco_bate_com_os_totais_da_spec()
    {
        var pasta = Directory.CreateTempSubdirectory("sp156-real-").FullName;
        try
        {
            var db = Path.Combine(pasta, "pedidos.db");

            var relogio = Stopwatch.StartNew();
            long gravadas;
            using (var xlsx = File.OpenRead(FactArquivoRealAttribute.Caminho!))
                gravadas = GravadorSqlite.Gravar(LeitorSolicitacoes.Ler(xlsx), db);
            relogio.Stop();

            // Medidas só informativas: não são afirmadas.
            _saida.WriteLine($"Data: {DateTime.Now:yyyy-MM-dd}  Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
            _saida.WriteLine($"Tempo de leitura + gravação: {relogio.Elapsed.TotalSeconds:F1} s");
            _saida.WriteLine($"Tamanho do .db: {new FileInfo(db).Length} bytes");
            _saida.WriteLine($"Linhas gravadas (retorno): {gravadas}");

            long Escalar(SqliteConnection c, string sql)
            {
                using var comando = c.CreateCommand();
                comando.CommandText = sql;
                return Convert.ToInt64(comando.ExecuteScalar(), CultureInfo.InvariantCulture);
            }

            using var conexao = new SqliteConnection($"Data Source={db};Mode=ReadOnly;Pooling=False");
            conexao.Open();
            Assert.Multiple(
                () => Assert.Equal(742904, Escalar(conexao, "SELECT count(*) FROM pedidos")),
                () => Assert.Equal(742904, gravadas),
                () => Assert.Equal(24308, Escalar(conexao, "SELECT count(*) FROM pedidos WHERE data_finalizacao IS NULL")),
                () => Assert.Equal(1, Escalar(conexao, "SELECT count(*) FROM pedidos WHERE meio_comunicacao IS NULL")),
                () => Assert.Equal(715139, Escalar(conexao, "SELECT count(*) FROM pedidos WHERE status = 'Finalizada'")),
                () => Assert.Equal(23504, Escalar(conexao, "SELECT count(*) FROM pedidos WHERE status = 'Aberto'")),
                () => Assert.Equal(3948, Escalar(conexao, "SELECT count(*) FROM pedidos WHERE status = 'Cancelada'")),
                () => Assert.Equal(313, Escalar(conexao, "SELECT count(*) FROM pedidos WHERE status = 'Em Andamento'")));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(pasta, recursive: true);
        }
    }
}
