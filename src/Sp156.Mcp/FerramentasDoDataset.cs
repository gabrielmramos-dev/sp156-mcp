using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Sp156.Mcp;

/// <summary>Ferramentas MCP. Uma conexão somente leitura por chamada; o que é do banco inteiro vem de <see cref="DadosDoBanco"/>.</summary>
[McpServerToolType]
public sealed class FerramentasDoDataset(ConfiguracaoDoServidor configuracao, DadosDoBanco dados)
{
    [McpServerTool(Name = "descrever_dataset", UseStructuredContent = true)]
    [Description("Descreve o conjunto de dados carregado: total de linhas, período, contagem por status e por mês, número de valores distintos por coluna categórica e as limitações. Use antes de fazer perguntas, para saber o que o conjunto cobre.")]
    public DescricaoDoDataset DescreverDataset()
    {
        using var conexao = BancoSomenteLeitura.Abrir(configuracao.CaminhoDoBanco);
        return DescricaoDoDataset.Descrever(conexao, dados.Catalogo, dados.Medidas);
    }
}
