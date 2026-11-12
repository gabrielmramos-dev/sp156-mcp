using Sp156.Ingest;
using static Sp156.Ingest.Tests.PlanilhaDeTeste;

namespace Sp156.Ingest.Tests;

public class MarcaDeErroTests
{
    [Fact]
    public void Marcar_devolve_a_mesma_excecao_ja_marcada()
    {
        var original = new InvalidDataException("mensagem");

        var marcada = MarcaDeErro.Marcar(original);

        Assert.Same(original, marcada);
        Assert.True(MarcaDeErro.EhNossa(marcada));
        Assert.Equal("mensagem", marcada.Message);
    }

    [Fact]
    public void InvalidDataException_comum_sem_marca_nao_e_nossa()
    {
        Assert.False(MarcaDeErro.EhNossa(new InvalidDataException("End of Central Directory record could not be found.")));
        Assert.False(MarcaDeErro.EhNossa(new InvalidOperationException("qualquer")));
    }

    [Fact]
    public void Aba_ausente_lanca_InvalidDataException_marcada()
    {
        using var xlsx = Criar("Outra", CabecalhoDoArquivo, Linha(("Tema", "x")));

        var ex = Assert.Throws<InvalidDataException>(() => LeitorSolicitacoes.Ler(xlsx));

        Assert.True(MarcaDeErro.EhNossa(ex), "a exceção de aba ausente deveria estar marcada");
        Assert.Contains("Solicitações", ex.Message); // tipo e mensagem não mudam
    }

    [Fact]
    public void Cabecalho_obrigatorio_duplicado_lanca_InvalidDataException_marcada()
    {
        var duplicado = CabecalhoDoArquivo.Append("Tema").ToArray();

        var ex = Assert.Throws<InvalidDataException>(() => MapaColunas.Criar(duplicado));

        Assert.True(MarcaDeErro.EhNossa(ex), "a exceção de cabeçalho duplicado deveria estar marcada");
        Assert.Contains("duplicado", ex.Message);
    }

    [Fact]
    public void Cabecalho_duplicado_vindo_de_um_xlsx_tambem_chega_marcado()
    {
        using var xlsx = CriarComCabecalhoDuplicado();

        var ex = Assert.Throws<InvalidDataException>(() => LeitorSolicitacoes.Ler(xlsx));

        Assert.True(MarcaDeErro.EhNossa(ex));
    }
}
