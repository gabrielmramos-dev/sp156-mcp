using System.Text.Json.Serialization;

namespace Sp156.Mcp;

/// <summary>Um grupo de uma saída agrupada (spec M2, §6.5).</summary>
public sealed record Grupo(
    [property: JsonPropertyName("valor")] string Valor,
    [property: JsonPropertyName("pedidos")] long Pedidos);

/// <summary>
/// Supressão de grupos pequenos (decisão 13): grupos com menos de <see cref="MinimoDePedidos"/> pedidos
/// são somados em uma única linha <see cref="RotuloOutros"/>, a última, e os totais se preservam.
/// Não é garantia formal de anonimato.
/// </summary>
public static class Supressao
{
    public const int MinimoDePedidos = 5;
    public const string RotuloOutros = "outros (agrupados)";

    /// <summary>Mantém, na ordem recebida, os grupos com 5 ou mais pedidos e acrescenta "outros (agrupados)" se houver grupo pequeno.</summary>
    public static IReadOnlyList<Grupo> Aplicar(IEnumerable<Grupo> grupos)
    {
        ArgumentNullException.ThrowIfNull(grupos);

        var mantidos = new List<Grupo>();
        long outros = 0;
        var haPequeno = false;
        foreach (var grupo in grupos)
        {
            if (grupo.Pedidos >= MinimoDePedidos)
            {
                mantidos.Add(grupo);
            }
            else
            {
                outros += grupo.Pedidos;
                haPequeno = true;
            }
        }

        if (haPequeno) mantidos.Add(new Grupo(RotuloOutros, outros));
        return mantidos;
    }
}
