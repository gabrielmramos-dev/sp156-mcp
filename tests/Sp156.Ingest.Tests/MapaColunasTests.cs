using System.Text;
using Sp156.Ingest;

namespace Sp156.Ingest.Tests;

public class MapaColunasTests
{
    private static readonly string[] CabecalhoDoArquivo =
    {
        "Data de Abertura", "Data de Finalização", "Meio de Comunicação", "Tema", "Assunto",
        "Serviço", "Status", "Órgão", "Logradouro", "Número", "CEP", "Latitude", "Longitude",
        "Prefeitura Operacional", "Distrito",
    };

    [Fact]
    public void Cabecalho_na_ordem_do_arquivo_mapeia_cada_coluna_para_sua_posicao()
    {
        var mapa = MapaColunas.Criar(CabecalhoDoArquivo);

        foreach (var coluna in MapaColunas.Obrigatorias)
            Assert.Equal(Array.IndexOf(CabecalhoDoArquivo, coluna), mapa.Posicao(coluna));
    }

    [Fact]
    public void Colunas_reordenadas_mapeiam_para_as_novas_posicoes()
    {
        var invertido = CabecalhoDoArquivo.Reverse().ToArray();

        var mapa = MapaColunas.Criar(invertido);

        foreach (var coluna in MapaColunas.Obrigatorias)
            Assert.Equal(Array.IndexOf(invertido, coluna), mapa.Posicao(coluna));
    }

    [Fact]
    public void Colunas_extras_e_de_endereco_sao_ignoradas()
    {
        var comExtra = CabecalhoDoArquivo.Append("Coluna Nova").ToArray();

        var mapa = MapaColunas.Criar(comExtra);

        Assert.Equal(10, mapa.Quantidade);
    }

    [Fact]
    public void Cabecalho_obrigatorio_ausente_lanca_excecao_listando_ausentes_e_encontrados()
    {
        var semStatusEDistrito = CabecalhoDoArquivo.Where(c => c is not ("Status" or "Distrito")).ToArray();

        var ex = Assert.Throws<ColunaObrigatoriaAusenteException>(() => MapaColunas.Criar(semStatusEDistrito));

        Assert.Equal(new[] { "Status", "Distrito" }, ex.Ausentes);
        Assert.Contains("Status", ex.Message);
        Assert.Contains("Distrito", ex.Message);
        Assert.Contains("Cabeçalhos encontrados: 13", ex.Message); // só a contagem
    }

    [Fact]
    public void Mensagem_nao_ecoa_o_texto_dos_cabecalhos_encontrados()
    {
        // Arquivo cuja 1ª linha é dado, não cabeçalho.
        var primeiraLinhaDeDado = new[] { "05510001", "Rua Fictícia 1", "-23.580001" };

        var ex = Assert.Throws<ColunaObrigatoriaAusenteException>(() => MapaColunas.Criar(primeiraLinhaDeDado));

        Assert.DoesNotContain("05510001", ex.Message);
        Assert.DoesNotContain("Fictícia", ex.Message);
        Assert.DoesNotContain("-23.580001", ex.Message);
        Assert.Contains("Cabeçalhos encontrados: 3", ex.Message);
    }

    [Fact]
    public void Mensagem_sugere_cabecalhos_parecidos_sem_acento_ou_maiuscula()
    {
        var variantes = CabecalhoDoArquivo
            .Select(c => c switch { "Status" => "status", "Órgão" => "ORGAO", _ => c })
            .ToArray();

        var ex = Assert.Throws<ColunaObrigatoriaAusenteException>(() => MapaColunas.Criar(variantes));

        Assert.Contains("Status (parecidos: status)", ex.Message);
        Assert.Contains("Órgão (parecidos: ORGAO)", ex.Message);
    }

    [Fact]
    public void Cabecalho_obrigatorio_duplicado_lanca_excecao()
    {
        var duplicado = CabecalhoDoArquivo.Append("Tema").ToArray();

        var ex = Assert.Throws<InvalidDataException>(() => MapaColunas.Criar(duplicado));

        Assert.Contains("Tema", ex.Message);
        Assert.Contains("duplicado", ex.Message);
    }

    [Fact]
    public void Cabecalho_duplicado_so_por_forma_de_normalizacao_tambem_lanca_excecao()
    {
        var duplicado = CabecalhoDoArquivo.Append("Serviço".Normalize(NormalizationForm.FormD)).ToArray();

        Assert.Throws<InvalidDataException>(() => MapaColunas.Criar(duplicado));
    }

    [Fact]
    public void Coluna_extra_duplicada_nao_e_problema()
    {
        var duplicado = CabecalhoDoArquivo.Append("Logradouro").ToArray();

        var mapa = MapaColunas.Criar(duplicado);

        Assert.Equal(10, mapa.Quantidade);
    }

    [Fact]
    public void Comparacao_diferencia_maiusculas_de_minusculas()
    {
        var minusculo = CabecalhoDoArquivo.Select(c => c == "Status" ? "status" : c).ToArray();

        var ex = Assert.Throws<ColunaObrigatoriaAusenteException>(() => MapaColunas.Criar(minusculo));

        Assert.Equal(new[] { "Status" }, ex.Ausentes);
    }

    [Fact]
    public void Cabecalho_decomposto_nfd_equivale_ao_composto_nfc()
    {
        var nfd = CabecalhoDoArquivo.Select(c => c.Normalize(NormalizationForm.FormD)).ToArray();
        Assert.NotEqual(CabecalhoDoArquivo[1], nfd[1]); // garante que o teste exercita NFD

        var mapa = MapaColunas.Criar(nfd);

        foreach (var coluna in MapaColunas.Obrigatorias)
            Assert.Equal(Array.IndexOf(CabecalhoDoArquivo, coluna), mapa.Posicao(coluna));
    }
}
