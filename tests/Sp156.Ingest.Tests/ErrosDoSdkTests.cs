using System.ComponentModel;
using System.IO.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Sp156.Mcp;
using Xunit.Abstractions;

namespace Sp156.Ingest.Tests;

/// <summary>Ferramentas só de teste, para medir como o SDK 2.4.0 trata erros (spec M2, §10 e §15).</summary>
[McpServerToolType]
public sealed class FerramentasDeTeste
{
    [McpServerTool(Name = "falha_clara"), Description("Lança McpException.")]
    public static string FalhaClara() => throw new McpException("mensagem clara");

    [McpServerTool(Name = "falha_inesperada"), Description("Lança uma exceção qualquer.")]
    public static string FalhaInesperada() => throw new ArgumentException("segredo-123");

    [McpServerTool(Name = "eco"), Description("Devolve os parâmetros.")]
    public static string Eco(string obrigatorio, int limite = 20) => $"{obrigatorio}|{limite}";
}

public class ErrosDoSdkTests
{
    private readonly ITestOutputHelper _saida;

    public ErrosDoSdkTests(ITestOutputHelper saida) => _saida = saida;

    private static async Task<(McpClient Cliente, IHost Host)> Subir()
    {
        Pipe clienteParaServidor = new(), servidorParaCliente = new();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        ServidorMcp.Registrar(builder.Services, new ConfiguracaoDoServidor("/caminho/qualquer.db"))
            .WithTools<FerramentasDeTeste>()
            .WithStreamServerTransport(clienteParaServidor.Reader.AsStream(), servidorParaCliente.Writer.AsStream());
        var host = builder.Build();
        await host.StartAsync();
        var cliente = await McpClient.CreateAsync(
            new StreamClientTransport(clienteParaServidor.Writer.AsStream(), servidorParaCliente.Reader.AsStream()));
        return (cliente, host);
    }

    // Chama e devolve (IsError, texto) ou, se o SDK lançar no cliente, ("exceção", mensagem).
    private async Task<(string Tipo, string Texto)> Chamar(string ferramenta, Dictionary<string, object?> argumentos)
    {
        var (cliente, host) = await Subir();
        try
        {
            var resultado = await cliente.CallToolAsync(ferramenta, argumentos);
            var texto = string.Join(" | ", resultado.Content.OfType<TextContentBlock>().Select(b => b.Text));
            var tipo = resultado.IsError == true ? "IsError" : "ok";
            _saida.WriteLine($"{ferramenta}: {tipo}: {texto}");
            return (tipo, texto);
        }
        catch (Exception ex)
        {
            _saida.WriteLine($"{ferramenta}: exceção {ex.GetType().Name}: {ex.Message}");
            return ("exceção " + ex.GetType().Name, ex.Message);
        }
        finally
        {
            await cliente.DisposeAsync();
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task McpException_vira_IsError_com_a_mensagem()
    {
        Assert.Equal(("IsError", "An error occurred invoking 'falha_clara': mensagem clara"), await Chamar("falha_clara", []));
    }

    [Fact]
    public async Task Excecao_inesperada_vira_IsError_com_mensagem_generica_sem_o_texto_da_excecao()
    {
        var (tipo, texto) = await Chamar("falha_inesperada", []);

        Assert.Equal("IsError", tipo);
        Assert.Equal("An error occurred invoking 'falha_inesperada'.", texto);
        Assert.DoesNotContain("segredo", texto);
    }

    // Os quatro testes abaixo descrevem o SDK 2.4.0 como ele é (medido na M2-2), não uma escolha nossa.

    [Fact]
    public async Task Parametro_desconhecido_e_ignorado_em_silencio()
    {
        Assert.Equal(("ok", "a|20"), await Chamar("eco", new() { ["obrigatorio"] = "a", ["orgão"] = "x" }));
    }

    [Fact]
    public async Task Parametro_obrigatorio_ausente_da_IsError_generico_sem_dizer_qual()
    {
        Assert.Equal(("IsError", "An error occurred invoking 'eco'."), await Chamar("eco", new() { ["limite"] = 3 }));
    }

    [Fact]
    public async Task Texto_no_lugar_de_inteiro_da_IsError_generico()
    {
        Assert.Equal(("IsError", "An error occurred invoking 'eco'."), await Chamar("eco", new() { ["obrigatorio"] = "a", ["limite"] = "vinte" }));
    }

    [Fact]
    public async Task Numero_no_lugar_de_texto_da_IsError_generico()
    {
        Assert.Equal(("IsError", "An error occurred invoking 'eco'."), await Chamar("eco", new() { ["obrigatorio"] = 5 }));
    }
}
