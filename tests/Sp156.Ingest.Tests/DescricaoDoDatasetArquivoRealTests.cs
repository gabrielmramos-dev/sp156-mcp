using Sp156.Mcp;

namespace Sp156.Ingest.Tests;

/// <summary>
/// <c>descrever_dataset</c> contra o arquivo real (opt-in, SP156_XLSX). Valores de referência da spec M2, §13,
/// calculados com pandas e conferidos por SQL. Um teste só, para o .db (10 a 17 s) ser gerado uma vez.
/// </summary>
public sealed class DescricaoDoDatasetArquivoRealTests : IDisposable
{
    private readonly string _pasta = Directory.CreateTempSubdirectory("sp156-descricao-real-").FullName;

    public void Dispose() => Directory.Delete(_pasta, recursive: true);

    private static Grupo G(string valor, long pedidos) => new(valor, pedidos);

    [FactArquivoReal]
    public void No_arquivo_real_bate_com_os_valores_de_referencia()
    {
        var db = Path.Combine(_pasta, "pedidos.db");
        using (var xlsx = File.OpenRead(FactArquivoRealAttribute.Caminho!))
            GravadorSqlite.Gravar(LeitorSolicitacoes.Ler(xlsx), db);

        var configuracao = new ConfiguracaoDoServidor(db);
        var d = new FerramentasDoDataset(configuracao, new DadosDoBanco(configuracao)).DescreverDataset();

        Assert.Multiple(
            () => Assert.Equal(742904, d.TotalLinhas),
            () => Assert.Equal(new Periodo("2025-01-01", "2025-03-31"), d.Periodo),
            () => Assert.Equal(new[] { G("2025-01", 260541), G("2025-02", 247718), G("2025-03", 234645) }, d.PorMes),
            () => Assert.Equal(
                new[] { G("Finalizada", 715139), G("Aberto", 23504), G("Cancelada", 3948), G("Em Andamento", 313) },
                d.PorStatus),
            () => Assert.Equal(new ContagemPorColuna(28, 204, 707, 4, 517, 38, 96, 6), d.ValoresDistintos),
            () => Assert.Equal(new ContagemPorColuna(27, 182, 563, 4, 430, 36, 96, 6), d.ValoresNoCatalogo),
            () => Assert.Equal(new LinhasSemValor(367648, 374346, 1), d.LinhasSemValor),
            () => Assert.Contains("de 2025-01-01 a 2025-03-31.", d.Limitacoes[1]),
            () => Assert.Contains(" 56,47% das linhas (419529 de 742904) ", d.Limitacoes[2]),
            () => Assert.Contains("não anterior a 2026-08-05 ", d.Limitacoes[3]));
    }
}
