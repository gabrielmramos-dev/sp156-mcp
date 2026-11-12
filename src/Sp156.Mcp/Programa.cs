using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Sp156.Mcp;

/// <summary>
/// Servidor MCP sobre stdio. Códigos de saída: 0 ao encerrar (por exemplo, quando o stdin fecha) e 1 se a
/// configuração for inválida. O stdout é do protocolo; mensagens e logs vão para o stderr.
/// </summary>
public static class Programa
{
    public static Task<int> Main(string[] args) => Executar(args, Environment.GetEnvironmentVariable, Console.Error);

    public static async Task<int> Executar(string[] args, Func<string, string?> variavel, TextWriter erro, CancellationToken parar = default)
    {
        var (configuracao, mensagem) = ConfiguracaoDoServidor.Resolver(args, variavel);
        if (configuracao is null)
        {
            erro.WriteLine(mensagem);
            return 1;
        }

        var builder = Host.CreateApplicationBuilder(args);
        ServidorMcp.ConfigurarLogs(builder.Logging);
        ServidorMcp.Registrar(builder.Services, configuracao).WithStdioServerTransport();

        await builder.Build().RunAsync(parar);
        return 0;
    }
}
