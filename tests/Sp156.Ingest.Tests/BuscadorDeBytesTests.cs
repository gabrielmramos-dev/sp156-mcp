using System.Text;
using static Sp156.Ingest.Tests.BuscadorDeBytes;

namespace Sp156.Ingest.Tests;

public class BuscadorDeBytesTests
{
    // Fluxo sintético de 'x' com o texto (em UTF-8) gravado a partir de 'inicio'.
    private static MemoryStream Fluxo(int tamanho, string texto, long inicio)
    {
        var bytes = new byte[tamanho];
        Array.Fill(bytes, (byte)'x');
        Encoding.UTF8.GetBytes(texto).CopyTo(bytes, inicio);
        return new MemoryStream(bytes);
    }

    private static HashSet<(string Coluna, string Valor)> Buscar(Stream fluxo, params (string Coluna, string Valor)[] padroes) =>
        Procurar(fluxo, padroes);

    // --- (a) Ocorrência que cruza a fronteira entre dois blocos de 4 MiB -----------------------

    [Fact]
    public void Ocorrencia_que_cruza_a_fronteira_de_dois_blocos_de_4_MiB_e_encontrada()
    {
        const string valor = "Rua Fictícia 123";
        using var fluxo = Fluxo(2 * BlocoPadrao, valor, inicio: BlocoPadrao - 6); // 6 bytes no 1º bloco, o resto no 2º

        var acertos = Buscar(fluxo, ("Logradouro", valor));

        Assert.Equal(new[] { ("Logradouro", valor) }, acertos);
    }

    [Fact]
    public void Ocorrencia_cuja_fronteira_cai_no_meio_de_um_caractere_de_2_bytes_e_encontrada()
    {
        const string valor = "Avenida São João";
        var bytesAteOA = Encoding.UTF8.GetByteCount("Avenida S"); // 'ã' ocupa os 2 bytes seguintes
        using var fluxo = Fluxo(2 * BlocoPadrao, valor, inicio: BlocoPadrao - (bytesAteOA + 1)); // corta 'ã' ao meio

        var acertos = Buscar(fluxo, ("Logradouro", valor));

        Assert.Contains(("Logradouro", valor), acertos);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    public void Ocorrencia_e_encontrada_em_qualquer_posicao_com_blocos_pequenos(int bloco)
    {
        const string valor = "São João"; // 10 bytes em UTF-8, com 2 caracteres de 2 bytes
        for (var inicio = 0; inicio <= 64 - Encoding.UTF8.GetByteCount(valor); inicio++)
        {
            using var fluxo = Fluxo(64, valor, inicio);

            var acertos = Procurar(fluxo, new[] { ("Logradouro", valor) }, bloco);

            Assert.True(acertos.Contains(("Logradouro", valor)), $"não achou com bloco {bloco} e início {inicio}");
        }
    }

    // --- (b) Padrão com acento ----------------------------------------------------------------

    [Fact]
    public void Padrao_com_acento_e_encontrado()
    {
        using var fluxo = Fluxo(1000, "Avenida São João", inicio: 100);

        var acertos = Buscar(fluxo, ("Logradouro", "Avenida São João"));

        Assert.Equal(new[] { ("Logradouro", "Avenida São João") }, acertos);
    }

    [Fact]
    public void Cedilha_sozinha_e_encontrada_e_a_versao_sem_acento_nao_casa_com_o_texto_acentuado()
    {
        using var fluxo = Fluxo(1000, "Praça", inicio: 10);

        var acertos = Buscar(fluxo, ("A", "ç"), ("B", "Praca"));

        Assert.Equal(new[] { ("A", "ç") }, acertos);
    }

    // --- (c) Padrão que não existe -------------------------------------------------------------

    [Fact]
    public void Padrao_que_nao_existe_nao_devolve_acerto()
    {
        using var fluxo = Fluxo(2 * BlocoPadrao, "Avenida São João", inicio: BlocoPadrao - 6);

        var acertos = Buscar(fluxo, ("Logradouro", "Rua Inexistente 999"), ("CEP", "99999999"));

        Assert.Empty(acertos);
    }

    // --- Casos de borda e atribuição de coluna -------------------------------------------------

    [Fact]
    public void Acerto_no_inicio_e_no_fim_do_arquivo_e_cada_acerto_traz_a_sua_coluna()
    {
        var bytes = new byte[100];
        Array.Fill(bytes, (byte)'x');
        Encoding.UTF8.GetBytes("INICIO").CopyTo(bytes, 0);
        Encoding.UTF8.GetBytes("FIM").CopyTo(bytes, 97);
        using var fluxo = new MemoryStream(bytes);

        var acertos = Buscar(fluxo, ("CEP", "INICIO"), ("Latitude", "FIM"), ("Longitude", "AUSENTE"));

        Assert.Equal(new[] { ("CEP", "INICIO"), ("Latitude", "FIM") }.Order(), acertos.Order());
    }

    [Fact]
    public void Fluxo_vazio_e_sem_padroes_nao_devolvem_acerto()
    {
        Assert.Empty(Buscar(new MemoryStream(), ("Logradouro", "qualquer")));
        Assert.Empty(Buscar(Fluxo(100, "abc", 0)));
    }
}
