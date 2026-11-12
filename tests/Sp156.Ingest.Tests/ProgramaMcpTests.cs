using System.Diagnostics;
using System.Text;
using Sp156.Mcp;
using static Sp156.Ingest.Tests.PlanilhaDeTeste;

namespace Sp156.Ingest.Tests;

public sealed class ProgramaMcpTests : IDisposable
{
    private readonly string _pasta = Directory.CreateTempSubdirectory("sp156-mcp-prog-").FullName;

    public void Dispose() => Directory.Delete(_pasta, recursive: true);

    private string GravarFixture()
    {
        var db = Path.Combine(_pasta, "pedidos.db");
        using var xlsx = AbrirFixture();
        GravadorSqlite.Gravar(LeitorSolicitacoes.Ler(xlsx), db);
        return db;
    }

    // --- Configuração inválida: código 1, mensagem no stderr, sem iniciar o protocolo --------------

    [Fact]
    public async Task Sem_banco_devolve_1_e_explica_no_stderr()
    {
        var erro = new StringWriter();

        var codigo = await Programa.Executar(Array.Empty<string>(), _ => null, erro);

        Assert.Equal(1, codigo);
        Assert.Contains("--db", erro.ToString());
        Assert.Contains("SP156_DB", erro.ToString());
    }

    [Fact]
    public async Task Banco_inexistente_devolve_1_e_cita_o_caminho()
    {
        var inexistente = Path.Combine(_pasta, "nao-existe.db");
        var erro = new StringWriter();

        var codigo = await Programa.Executar(new[] { "--db", inexistente }, _ => null, erro);

        Assert.Equal(1, codigo);
        Assert.Contains(inexistente, erro.ToString());
    }

    // --- Processo real: o stdout é só do protocolo; os logs vão para o stderr ----------------------

    [Fact]
    public void Processo_real_sem_requisicao_deixa_o_stdout_vazio_loga_no_stderr_e_sai_com_0_ao_fechar_o_stdin()
    {
        var db = GravarFixture();
        var dll = Path.Combine(AppContext.BaseDirectory, "Sp156.Mcp.dll");
        Assert.True(File.Exists(dll), "Sp156.Mcp.dll não foi copiado para a saída dos testes");

        var inicio = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        inicio.ArgumentList.Add(dll);
        inicio.ArgumentList.Add("--db");
        inicio.ArgumentList.Add(db);
        inicio.Environment.Remove("SP156_DB");

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        using var processo = Process.Start(inicio)!;
        try
        {
            processo.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (stdout) stdout.AppendLine(e.Data); };
            processo.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (stderr) stderr.AppendLine(e.Data); };
            processo.BeginOutputReadLine();
            processo.BeginErrorReadLine();

            // Espera o servidor subir (log no stderr) ou o processo terminar.
            var limite = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < limite && !processo.HasExited)
            {
                lock (stderr) if (stderr.ToString().Contains("Application started")) break;
                Thread.Sleep(100);
            }

            processo.StandardInput.Close(); // fim do stdin: o servidor deve encerrar
            var saiu = processo.WaitForExit(30_000);
            if (!saiu) processo.Kill(entireProcessTree: true);
            processo.WaitForExit();

            Assert.True(saiu, "o servidor não encerrou depois de o stdin fechar");
            Assert.Equal(0, processo.ExitCode);
            Assert.Equal("", stdout.ToString()); // o stdout é só do protocolo, e nenhuma requisição foi feita
            Assert.Contains("Application started", stderr.ToString()); // o log do host foi para o stderr
        }
        finally
        {
            if (!processo.HasExited) processo.Kill(entireProcessTree: true);
        }
    }

    // --- Convenções do código do servidor --------------------------------------------------------

    private static string RaizDoRepositorio()
    {
        var pasta = new DirectoryInfo(AppContext.BaseDirectory);
        while (pasta is not null && !File.Exists(Path.Combine(pasta.FullName, "Sp156.sln"))) pasta = pasta.Parent;
        return pasta?.FullName ?? throw new InvalidOperationException("Sp156.sln não encontrada acima de " + AppContext.BaseDirectory);
    }

    [Fact]
    public void Codigo_do_servidor_nao_escreve_no_stdout_nem_toca_o_xlsx_nem_grava_o_banco()
    {
        var fontes = Directory.GetFiles(Path.Combine(RaizDoRepositorio(), "src", "Sp156.Mcp"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToList();
        Assert.NotEmpty(fontes);

        foreach (var fonte in fontes)
        {
            var texto = File.ReadAllText(fonte);
            var nome = Path.GetFileName(fonte);
            foreach (var proibido in new[] { "Console.Out", "Console.Write", "Console.WriteLine", "LeitorSolicitacoes", "GravadorSqlite", "Sylvan" })
                Assert.False(texto.Contains(proibido), $"{nome} usa '{proibido}'");
        }
    }
}
