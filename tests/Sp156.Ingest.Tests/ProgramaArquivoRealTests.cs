using System.Diagnostics;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Sp156.Cli;
using Xunit.Abstractions;

namespace Sp156.Ingest.Tests;

public class ProgramaArquivoRealTests
{
    private readonly ITestOutputHelper _saida;

    public ProgramaArquivoRealTests(ITestOutputHelper saida) => _saida = saida;

    [FactArquivoReal]
    public void Ingerir_o_arquivo_real_devolve_0_e_grava_742904_linhas()
    {
        var pasta = Directory.CreateTempSubdirectory("sp156-cli-real-").FullName;
        try
        {
            var db = Path.Combine(pasta, "pedidos.db");
            var saida = new StringWriter();
            var erro = new StringWriter();

            var relogio = Stopwatch.StartNew();
            var codigo = Programa.Executar(new[] { "ingerir", FactArquivoRealAttribute.Caminho!, db }, saida, erro);
            relogio.Stop();

            // Medida só informativa: não é afirmada.
            _saida.WriteLine($"Data: {DateTime.Now:yyyy-MM-dd}  Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
            _saida.WriteLine($"Tempo de Programa.Executar: {relogio.Elapsed.TotalSeconds:F1} s");
            _saida.WriteLine($"Código de saída: {codigo}");

            Assert.Equal(0, codigo);
            Assert.Equal("", erro.ToString());
            Assert.True(File.Exists(db), "o banco não foi criado");
            using var conexao = new SqliteConnection($"Data Source={db};Mode=ReadOnly;Pooling=False");
            conexao.Open();
            using var comando = conexao.CreateCommand();
            comando.CommandText = "SELECT count(*) FROM pedidos";
            Assert.Equal(742904, Convert.ToInt64(comando.ExecuteScalar(), CultureInfo.InvariantCulture));
            Assert.Contains("742904 pedidos gravados", saida.ToString());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(pasta, recursive: true);
        }
    }
}
