using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Sp156.Mcp;

public static class ServidorMcp
{
    public const string Nome = "sp156-mcp";

    /// <summary>O stdout é o canal do protocolo: todos os logs vão para o stderr.</summary>
    public static void ConfigurarLogs(ILoggingBuilder logs) =>
        logs.AddConsole(opcoes => opcoes.LogToStandardErrorThreshold = LogLevel.Trace);

    /// <summary>Registra o servidor sem escolher o transporte (stdio no Main, fluxos em memória nos testes).</summary>
    public static IMcpServerBuilder Registrar(IServiceCollection servicos, ConfiguracaoDoServidor configuracao)
    {
        servicos.AddSingleton(configuracao);
        servicos.AddSingleton<DadosDoBanco>();
        return servicos.AddMcpServer(opcoes => opcoes.ServerInfo = new Implementation
        {
            Name = Nome,
            Version = typeof(ServidorMcp).Assembly.GetName().Version?.ToString() ?? "0.0.0",
        }).WithTools<FerramentasDoDataset>();
    }
}
