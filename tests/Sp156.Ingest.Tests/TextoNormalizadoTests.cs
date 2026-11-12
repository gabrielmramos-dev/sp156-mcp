using System.Text;
using Sp156.Ingest;

namespace Sp156.Ingest.Tests;

public class TextoNormalizadoTests
{
    [Fact]
    public void Espaco_duplo_e_caixa_nao_mudam_a_chave()
    {
        Assert.Equal(TextoNormalizado.Chave("reparos em asfalto"), TextoNormalizado.Chave("Reparos em  Asfalto"));
    }

    [Fact]
    public void Acento_e_caixa_nao_mudam_a_chave()
    {
        Assert.Equal(TextoNormalizado.Chave("integracao-smgi/sgz"), TextoNormalizado.Chave("INTEGRAÇÃO-SMGI/SGZ"));
    }

    [Fact]
    public void Cedilha_decomposta_e_composta_geram_a_mesma_chave()
    {
        var composto = "Serviço";
        var decomposto = "Serviço"; // c + U+0327 (cedilha combinante)
        Assert.NotEqual(composto, decomposto); // garante que o teste exercita a forma decomposta

        Assert.Equal(TextoNormalizado.Chave(composto), TextoNormalizado.Chave(decomposto));
    }

    [Fact]
    public void Chave_tem_o_valor_esperado()
    {
        Assert.Equal("reparos em asfalto, pontes e tuneis",
            TextoNormalizado.Chave("  Reparos \t em  Asfalto, Pontes e Túneis \n"));
    }

    [Fact]
    public void Chave_preserva_pontuacao_e_barras()
    {
        Assert.Equal("integracao-smgi/sgz", TextoNormalizado.Chave("INTEGRAÇÃO-SMGI/SGZ"));
    }

    [Fact]
    public void Chave_de_forma_decomposta_inteira_nao_deixa_marcas()
    {
        var chave = TextoNormalizado.Chave("Órgão de Atenção".Normalize(NormalizationForm.FormD));

        Assert.Equal("orgao de atencao", chave);
    }
}
