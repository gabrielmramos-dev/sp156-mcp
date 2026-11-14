using Sp156.Mcp;

namespace Sp156.Ingest.Tests;

public class SupressaoTests
{
    private static Grupo G(string valor, long pedidos) => new(valor, pedidos);

    [Fact]
    public void Grupos_com_menos_de_5_viram_uma_linha_outros_no_fim()
    {
        var resultado = Supressao.Aplicar([G("a", 12), G("b", 4), G("c", 5), G("d", 1)]);

        Assert.Equal(new[] { G("a", 12), G("c", 5), G("outros (agrupados)", 5) }, resultado);
    }

    [Fact]
    public void O_total_se_preserva()
    {
        Grupo[] grupos = [G("a", 30), G("b", 2), G("c", 7), G("d", 3), G("e", 4)];

        Assert.Equal(grupos.Sum(g => g.Pedidos), Supressao.Aplicar(grupos).Sum(g => g.Pedidos));
    }

    [Fact]
    public void Sem_grupo_pequeno_nao_ha_linha_outros()
    {
        Assert.Equal(new[] { G("a", 9), G("b", 5) }, Supressao.Aplicar([G("a", 9), G("b", 5)]));
    }

    [Fact]
    public void So_grupos_pequenos_viram_so_a_linha_outros()
    {
        Assert.Equal(new[] { G("outros (agrupados)", 7) }, Supressao.Aplicar([G("a", 4), G("b", 3)]));
    }

    [Fact]
    public void Sem_grupos_nao_ha_linha_nenhuma()
    {
        Assert.Empty(Supressao.Aplicar([]));
    }

    [Fact]
    public void A_ordem_dos_grupos_mantidos_e_a_recebida()
    {
        Assert.Equal(
            new[] { G("z", 5), G("a", 50), G("outros (agrupados)", 1) },
            Supressao.Aplicar([G("z", 5), G("m", 1), G("a", 50)]));
    }
}
