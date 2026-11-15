using System.IO.Pipelines;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Sp156.Mcp;
using static Sp156.Ingest.Tests.PlanilhaDeTeste;

namespace Sp156.Ingest.Tests;

/// <summary><c>descrever_dataset</c> pelo transporte em memória, sobre o banco da fixture (spec M2, §7 e §12a).</summary>
public sealed class DescreverDatasetFerramentaTests : IAsyncLifetime
{
    private readonly string _pasta = Directory.CreateTempSubdirectory("sp156-ferramenta-").FullName;
    private McpClient _cliente = null!;
    private IHost _host = null!;

    private string Db => Path.Combine(_pasta, "pedidos.db");

    public async Task InitializeAsync()
    {
        using (var xlsx = AbrirFixture())
            GravadorSqlite.Gravar(LeitorSolicitacoes.Ler(xlsx), Db);

        Pipe clienteParaServidor = new(), servidorParaCliente = new();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        ServidorMcp.Registrar(builder.Services, new ConfiguracaoDoServidor(Db))
            .WithStreamServerTransport(clienteParaServidor.Reader.AsStream(), servidorParaCliente.Writer.AsStream());
        _host = builder.Build();
        await _host.StartAsync();
        _cliente = await McpClient.CreateAsync(
            new StreamClientTransport(clienteParaServidor.Writer.AsStream(), servidorParaCliente.Reader.AsStream()));
    }

    public async Task DisposeAsync()
    {
        await _cliente.DisposeAsync();
        await _host.StopAsync();
        _host.Dispose();
        Directory.Delete(_pasta, recursive: true);
    }

    private async Task<JsonElement> Chamar()
    {
        var resultado = await _cliente.CallToolAsync("descrever_dataset", new Dictionary<string, object?>());
        Assert.NotEqual(true, resultado.IsError);
        Assert.NotNull(resultado.StructuredContent);
        return resultado.StructuredContent.Value;
    }

    private static long Long(JsonElement e, string nome) => e.GetProperty(nome).GetInt64();

    [Fact]
    public async Task Aparece_na_lista_com_a_descricao_da_spec_sem_parametros_e_com_esquema_de_saida()
    {
        var ferramenta = Assert.Single(await _cliente.ListToolsAsync(), f => f.Name == "descrever_dataset");

        Assert.Multiple(
            () => Assert.Equal(
                "Descreve o conjunto de dados carregado: total de linhas, período, contagem por status e por mês, número de valores distintos por coluna categórica e as limitações. Use antes de fazer perguntas, para saber o que o conjunto cobre.",
                ferramenta.Description),
            () => Assert.False(
                ferramenta.JsonSchema.TryGetProperty("properties", out var propriedades) && propriedades.EnumerateObject().Any(),
                "descrever_dataset não deve ter parâmetros"),
            () => Assert.NotNull(ferramenta.ProtocolTool.OutputSchema));
    }

    [Fact]
    public async Task Devolve_exatamente_as_propriedades_da_spec_em_snake_case()
    {
        var json = await Chamar();

        Assert.Equal(
            new[] { "limitacoes", "linhas_sem_valor", "periodo", "por_mes", "por_status", "total_linhas", "valores_distintos", "valores_no_catalogo" },
            json.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Na_fixture_devolve_os_numeros_do_banco()
    {
        var json = await Chamar();
        var porStatus = json.GetProperty("por_status").EnumerateArray()
            .Select(g => (g.GetProperty("valor").GetString(), Long(g, "pedidos"))).ToArray();

        Assert.Multiple(
            () => Assert.Equal(30, Long(json, "total_linhas")),
            () => Assert.Equal("2025-01-06", json.GetProperty("periodo").GetProperty("data_abertura_minima").GetString()),
            () => Assert.Equal("2025-03-18", json.GetProperty("periodo").GetProperty("data_abertura_maxima").GetString()),
            () => Assert.Equal(new[] { ("Finalizada", 27L), ("outros (agrupados)", 3L) }, porStatus),
            () => Assert.Equal(3, json.GetProperty("por_mes").GetArrayLength()),
            () => Assert.Equal(8, Long(json.GetProperty("valores_distintos"), "servico")),
            () => Assert.Equal(1, Long(json.GetProperty("valores_no_catalogo"), "status")),
            () => Assert.Equal(11, Long(json.GetProperty("linhas_sem_valor"), "distrito")));
    }

    [Fact]
    public async Task Limitacoes_trazem_os_quatro_textos_comuns()
    {
        var limitacoes = (await Chamar()).GetProperty("limitacoes").EnumerateArray().Select(t => t.GetString()).ToArray();

        Assert.Multiple(
            () => Assert.Equal(4, limitacoes.Length),
            () => Assert.Equal(Limitacoes.TextoCobertura, limitacoes.ElementAtOrDefault(0)),
            () => Assert.Contains("de 2025-01-06 a 2025-03-18", limitacoes.ElementAtOrDefault(1) ?? ""),
            () => Assert.Contains("10,00% das linhas (3 de 30)", limitacoes.ElementAtOrDefault(2) ?? ""),
            () => Assert.Contains("não anterior a 2025-03-28", limitacoes.ElementAtOrDefault(3) ?? ""));
    }
}
