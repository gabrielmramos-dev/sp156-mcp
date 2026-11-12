using System.Buffers;
using System.Diagnostics;
using System.Text;
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
    private const int TamanhoDoBloco = 4 * 1024 * 1024;

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
            var acertos = ProcurarNoDb(db, amostra);
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

    // Procura cada valor, como bytes UTF-8, no arquivo inteiro, em blocos com sobreposição. Cada byte vira
    // um char (Latin-1), o que mantém a busca byte a byte e deixa usar SearchValues para várias cadeias.
    private static HashSet<(string Coluna, string Chave)> ProcurarNoDb(string db, Dictionary<string, List<string>> amostra)
    {
        var padroes = amostra
            .SelectMany(par => par.Value.Select(valor => (Coluna: par.Key, Chave: Encoding.Latin1.GetString(Encoding.UTF8.GetBytes(valor)))))
            .ToList();
        var busca = SearchValues.Create(padroes.Select(p => p.Chave).Distinct().ToArray(), StringComparison.Ordinal);
        var maior = padroes.Max(p => p.Chave.Length);

        var acertos = new HashSet<(string, string)>();
        var bytes = new byte[TamanhoDoBloco];
        var chars = new char[TamanhoDoBloco + maior];
        var sobra = 0;

        using var arquivo = File.OpenRead(db);
        int lidos;
        while ((lidos = arquivo.Read(bytes, 0, bytes.Length)) > 0)
        {
            Encoding.Latin1.GetChars(bytes, 0, lidos, chars, sobra);
            var total = sobra + lidos;
            var trecho = chars.AsSpan(0, total);

            var inicio = 0;
            while (inicio < total)
            {
                var i = trecho[inicio..].IndexOfAny(busca);
                if (i < 0) break;
                var posicao = inicio + i;
                foreach (var (coluna, chave) in padroes)
                    if (trecho[posicao..].StartsWith(chave, StringComparison.Ordinal))
                        acertos.Add((coluna, chave));
                inicio = posicao + 1;
            }

            sobra = Math.Min(maior - 1, total);
            trecho[^sobra..].CopyTo(chars);
        }

        return acertos;
    }
}
