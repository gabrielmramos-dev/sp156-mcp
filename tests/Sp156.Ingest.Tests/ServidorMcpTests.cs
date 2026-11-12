using System.IO.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Sp156.Mcp;

namespace Sp156.Ingest.Tests;

public class ServidorMcpTests
{
    private static readonly ConfiguracaoDoServidor Config = new("/caminho/qualquer.db");

    // Servidor real (Host + DI) ligado a um cliente por Pipes em memória, como na documentação do SDK.
    private static async Task<(McpClient Cliente, IHost Host)> Subir()
    {
        Pipe clienteParaServidor = new(), servidorParaCliente = new();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        ServidorMcp.Registrar(builder.Services, Config)
            .WithStreamServerTransport(clienteParaServidor.Reader.AsStream(), servidorParaCliente.Writer.AsStream());
        var host = builder.Build();
        await host.StartAsync();
        var cliente = await McpClient.CreateAsync(
            new StreamClientTransport(clienteParaServidor.Writer.AsStream(), servidorParaCliente.Reader.AsStream()));
        return (cliente, host);
    }

    [Fact]
    public async Task Servidor_se_apresenta_como_sp156_mcp_com_versao()
    {
        var (cliente, host) = await Subir();
        try
        {
            Assert.NotNull(cliente.ServerInfo);
            Assert.Equal("sp156-mcp", cliente.ServerInfo.Name);
            Assert.False(string.IsNullOrWhiteSpace(cliente.ServerInfo.Version));
        }
        finally
        {
            await cliente.DisposeAsync();
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Configuracao_fica_registrada_no_contêiner_de_servicos()
    {
        var (cliente, host) = await Subir();
        try
        {
            Assert.Equal(Config, host.Services.GetService<ConfiguracaoDoServidor>());
        }
        finally
        {
            await cliente.DisposeAsync();
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Sem_ferramentas_o_servidor_nao_anuncia_a_capacidade_e_tools_list_nao_esta_disponivel()
    {
        // Comportamento medido no SDK 2.4.0: sem nenhuma ferramenta registrada, a capacidade "tools" não existe.
        var (cliente, host) = await Subir();
        try
        {
            Assert.Null(cliente.ServerCapabilities.Tools);
            var ex = await Assert.ThrowsAsync<McpProtocolException>(() => cliente.ListToolsAsync().AsTask());
            Assert.Contains("tools/list", ex.Message);
        }
        finally
        {
            await cliente.DisposeAsync();
            await host.StopAsync();
        }
    }
}
