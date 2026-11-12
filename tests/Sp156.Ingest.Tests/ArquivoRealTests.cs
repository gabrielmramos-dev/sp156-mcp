using System.Diagnostics;
using System.Runtime.InteropServices;
using Sp156.Ingest;
using Xunit.Abstractions;

namespace Sp156.Ingest.Tests;

public class ArquivoRealTests
{
    private readonly ITestOutputHelper _saida;

    public ArquivoRealTests(ITestOutputHelper saida) => _saida = saida;

    [FactArquivoReal]
    public void Ler_percorre_o_arquivo_real_inteiro_e_bate_com_os_totais_da_spec()
    {
        long total = 0, finalizacaoNula = 0, meioNulo = 0;
        var porStatus = new Dictionary<string, long>();

        var memoriaAntes = GC.GetTotalMemory(forceFullCollection: true);
        var relogio = Stopwatch.StartNew();

        using (var xlsx = File.OpenRead(FactArquivoRealAttribute.Caminho!))
        {
            try
            {
                // Contadores durante a enumeração: nada é guardado em lista.
                foreach (var pedido in LeitorSolicitacoes.Ler(xlsx))
                {
                    total++;
                    if (pedido.DataFinalizacao is null) finalizacaoNula++;
                    if (pedido.MeioComunicacao is null) meioNulo++;
                    porStatus[pedido.Status] = porStatus.GetValueOrDefault(pedido.Status) + 1;
                }
            }
            catch (LinhaInvalidaException ex)
            {
                Assert.Fail($"LinhaInvalidaException na linha {ex.Linha}, coluna '{ex.Coluna}'.");
            }
        }

        relogio.Stop();
        var memoriaDepois = GC.GetTotalMemory(forceFullCollection: false);
        var picoDoProcesso = Process.GetCurrentProcess().PeakWorkingSet64;

        // Medidas só informativas: não são afirmadas.
        _saida.WriteLine($"Data: {DateTime.Now:yyyy-MM-dd}  Máquina: {RuntimeInformation.OSDescription}, {Environment.ProcessorCount} threads");
        _saida.WriteLine($"Runtime: {RuntimeInformation.FrameworkDescription}  Configuração: {Configuracao}");
        _saida.WriteLine($"Tempo total da enumeração: {relogio.Elapsed.TotalSeconds:F1} s");
        _saida.WriteLine($"Pico de working set do processo de teste: {picoDoProcesso / 1048576} MiB");
        _saida.WriteLine($"GC.GetTotalMemory antes (com GC forçado): {memoriaAntes / 1048576.0:F1} MiB; depois (sem GC forçado): {memoriaDepois / 1048576.0:F1} MiB");
        _saida.WriteLine($"Pedidos: {total}  DataFinalizacao nula: {finalizacaoNula}  Meio nulo: {meioNulo}");
        foreach (var (status, quantidade) in porStatus.OrderByDescending(s => s.Value))
            _saida.WriteLine($"  Status {status}: {quantidade}");

        Assert.Multiple(
            () => Assert.Equal(742904, total),
            () => Assert.Equal(24308, finalizacaoNula),
            () => Assert.Equal(1, meioNulo),
            () => Assert.Equal(715139, porStatus.GetValueOrDefault("Finalizada")),
            () => Assert.Equal(23504, porStatus.GetValueOrDefault("Aberto")),
            () => Assert.Equal(3948, porStatus.GetValueOrDefault("Cancelada")),
            () => Assert.Equal(313, porStatus.GetValueOrDefault("Em Andamento")));
    }

    private const string Configuracao =
#if DEBUG
        "Debug";
#else
        "Release";
#endif
}
