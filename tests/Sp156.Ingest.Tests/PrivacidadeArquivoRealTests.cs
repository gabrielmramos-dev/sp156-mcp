using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Sp156.Cli;
using Sylvan.Data.Excel;
using Xunit.Abstractions;

namespace Sp156.Ingest.Tests;

/// <summary>
/// Confere, por AMOSTRA, que valores de endereço e coordenadas do arquivo real não aparecem nos bytes do
/// .db gerado. Não é prova exaustiva e não diz nada sobre o que a biblioteca decodifica em memória.
/// </summary>
public class PrivacidadeArquivoRealTests
{
    private const int PassoDaAmostra = 4000;   // um valor candidato a cada 4.000
    private const int MaximoPorColuna = 200;
    private const int TamanhoMinimo = 7;       // abaixo disso ("0", "1") casaria por acaso

    private static readonly string[] Colunas = { "Logradouro", "Número", "CEP", "Latitude", "Longitude" };

    private readonly ITestOutputHelper _saida;

    public PrivacidadeArquivoRealTests(ITestOutputHelper saida) => _saida = saida;

    [FactArquivoReal]
    public void Nenhum_valor_de_endereco_da_amostra_aparece_nos_bytes_do_db_do_arquivo_real()
    {
        var pasta = Directory.CreateTempSubdirectory("sp156-privacidade-").FullName;
        try
        {
            var db = Path.Combine(pasta, "pedidos.db");
            var codigo = Programa.Executar(new[] { "ingerir", FactArquivoRealAttribute.Caminho!, db }, new StringWriter(), new StringWriter());
            Assert.Equal(0, codigo);

            var relogioAmostra = Stopwatch.StartNew();
            var amostra = MontarAmostra(FactArquivoRealAttribute.Caminho!);
            relogioAmostra.Stop();

            var relogioVarredura = Stopwatch.StartNew();
            HashSet<(string Coluna, string Valor)> acertos;
            using (var arquivoDb = File.OpenRead(db))
                acertos = BuscadorDeBytes.Procurar(arquivoDb, amostra.SelectMany(par => par.Value.Select(v => (par.Key, v))).ToList());
            relogioVarredura.Stop();

            // Medidas só informativas: não são afirmadas. Nenhum valor de célula é impresso.
            _saida.WriteLine($"Data: {DateTime.Now:yyyy-MM-dd}  Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
            foreach (var coluna in Colunas)
                _saida.WriteLine($"Amostra de {coluna}: {amostra[coluna].Count} valores");
            _saida.WriteLine($"Tempo para montar a amostra: {relogioAmostra.Elapsed.TotalSeconds:F1} s");
            _saida.WriteLine($"Tempo da varredura do .db: {relogioVarredura.Elapsed.TotalSeconds:F1} s");
            _saida.WriteLine($"Tamanho do .db: {new FileInfo(db).Length} bytes");

            Assert.True(amostra.Values.Sum(v => v.Count) > 0, "a amostra ficou vazia");
            var resumo = string.Join(", ", Colunas
                .Select(c => (Coluna: c, Acertos: acertos.Count(a => a.Coluna == c)))
                .Where(x => x.Acertos > 0)
                .Select(x => $"{x.Coluna}: {x.Acertos} de {amostra[x.Coluna].Count}"));
            Assert.True(acertos.Count == 0, $"valores da amostra apareceram nos bytes do .db ({resumo})");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(pasta, recursive: true);
        }
    }

    // Lê o XLSX em streaming, só as colunas de endereço. Pega um valor candidato (não vazio, com 7+
    // caracteres) a cada PassoDaAmostra, até MaximoPorColuna distintos por coluna.
    private static Dictionary<string, List<string>> MontarAmostra(string caminho)
    {
        using var leitor = ExcelDataReader.Create(caminho);
        Assert.True(leitor.TryOpenWorksheet("Solicitações"));
        var posicoes = Colunas.Select(nome => Enumerable.Range(0, leitor.FieldCount).First(i => leitor.GetName(i) == nome)).ToArray();

        var amostra = Colunas.ToDictionary(c => c, _ => new List<string>());
        var vistos = Colunas.ToDictionary(c => c, _ => new HashSet<string>());
        var candidatos = new long[Colunas.Length];

        while (leitor.Read())
        {
            for (var k = 0; k < Colunas.Length; k++)
            {
                if (posicoes[k] >= leitor.RowFieldCount) continue;
                var valor = leitor.GetString(posicoes[k]);
                if (valor.Length < TamanhoMinimo) continue;

                var indice = candidatos[k]++;
                if (indice % PassoDaAmostra == 0 && amostra[Colunas[k]].Count < MaximoPorColuna && vistos[Colunas[k]].Add(valor))
                    amostra[Colunas[k]].Add(valor);
            }
        }

        return amostra;
    }
}
