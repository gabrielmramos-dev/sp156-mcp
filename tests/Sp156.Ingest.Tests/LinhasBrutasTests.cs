using System.IO.Compression;
using System.Text.RegularExpressions;
using Sp156.Ingest;
using static Sp156.Ingest.Tests.PlanilhaDeTeste;

namespace Sp156.Ingest.Tests;

public class LinhasBrutasTests
{
    private static List<LinhaBruta> LinhasDaFixture()
    {
        using var xlsx = AbrirFixture();
        return LeitorSolicitacoes.LerLinhasBrutas(xlsx).ToList();
    }

    // --- Linhas reais e linhas em branco -------------------------------------------------------

    [Fact]
    public void Premissa_da_fixture_a_aba_declara_36_linhas_mas_so_30_tem_dado()
    {
        using var zip = new ZipArchive(AbrirFixture(), ZipArchiveMode.Read);
        using var leitor = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var xml = leitor.ReadToEnd();

        Assert.Contains("<dimension ref=\"A1:O36\"", xml);
        Assert.Equal(36, Regex.Matches(xml, "<row ").Count); // cabeçalho + 30 com dado + 5 vazias
    }

    [Fact]
    public void Fixture_v2_devolve_30_linhas_e_ignora_as_5_vazias_do_fim()
    {
        Assert.Equal(30, LinhasDaFixture().Count);
    }

    [Fact]
    public void Linha_so_com_espacos_e_ignorada()
    {
        using var xlsx = Criar(Aba, CabecalhoDoArquivo,
            Linha(("Tema", "primeira"), ("Status", "Aberto")),
            CabecalhoDoArquivo.Select(_ => (string?)"   ").ToArray(),
            Linha(("Tema", "segunda"), ("Status", "Aberto")));

        var linhas = LeitorSolicitacoes.LerLinhasBrutas(xlsx).ToList();

        Assert.Equal(new object?[] { "primeira", "segunda" }, linhas.Select(l => l.Tema));
    }

    // --- Linha curta ---------------------------------------------------------------------------

    [Fact]
    public void Linha_curta_da_fixture_nao_lanca_e_completa_as_celulas_ausentes_com_nulo()
    {
        // Linhas 29 a 31 da planilha (as 3 últimas com dado) só têm 13 das 15 células.
        var ultimas = LinhasDaFixture().Skip(27).ToList();

        Assert.Equal(3, ultimas.Count);
        Assert.All(ultimas, l =>
        {
            Assert.Null(l.PrefeituraOperacional);
            Assert.Null(l.Distrito);
            Assert.Equal("Finalizada", l.Status);
        });
    }

    // --- Valor como veio, sem converter --------------------------------------------------------

    [Fact]
    public void Data_numerica_fica_como_serial_e_nao_como_data()
    {
        var linhas = LinhasDaFixture();
        Assert.NotEmpty(linhas);
        var primeira = linhas[0];

        Assert.IsType<double>(primeira.DataAbertura);
        Assert.Equal(45663d, primeira.DataAbertura); // 2025-01-06 como serial do Excel
    }

    [Fact]
    public void Texto_vazio_fica_vazio_e_celula_ausente_fica_nula_sem_nenhuma_conversao()
    {
        var linhas = LinhasDaFixture();

        // Data de Finalização: 27 seriais e 3 textos vazios (a conversão '' -> null é da tarefa 4).
        Assert.Equal(27, linhas.Count(l => l.DataFinalizacao is double));
        Assert.Equal(3, linhas.Count(l => l.DataFinalizacao is ""));
        // Meio de Comunicação: a única célula realmente ausente.
        Assert.Equal(1, linhas.Count(l => l.MeioComunicacao is null));
    }

    // --- Dono do Stream: o chamador ----------------------------------------------------------

    [Fact]
    public void Stream_do_chamador_continua_aberto_depois_de_enumerar_tudo()
    {
        using var xlsx = AbrirFixture();

        var linhas = LeitorSolicitacoes.LerLinhasBrutas(xlsx).ToList();

        Assert.Equal(30, linhas.Count); // enumerou até o fim
        Assert.True(xlsx.CanRead, "o leitor fechou o Stream do chamador");
    }

    [Fact]
    public void Stream_do_chamador_continua_aberto_quando_o_cabecalho_e_invalido()
    {
        using var xlsx = Criar(Aba, new[] { "Data de Abertura", "Tema" }, new string?[] { "x", "y" });

        Assert.Throws<ColunaObrigatoriaAusenteException>(() => LeitorSolicitacoes.LerLinhasBrutas(xlsx));

        Assert.True(xlsx.CanRead, "o leitor fechou o Stream do chamador");
    }

    // --- Privacidade (ADR 0002) ----------------------------------------------------------------

    [Fact]
    public void Linha_bruta_so_tem_as_10_colunas_mapeadas()
    {
        var nomes = typeof(LinhaBruta).GetProperties()
            .Where(p => p.Name != "EqualityContract")
            .Select(p => p.Name)
            .ToArray();

        Assert.Equal(new[]
        {
            "Linha", "DataAbertura", "DataFinalizacao", "MeioComunicacao", "Tema", "Assunto",
            "Servico", "Status", "Orgao", "PrefeituraOperacional", "Distrito",
        }, nomes);
        Assert.DoesNotContain(nomes, n => n is "Logradouro" or "Numero" or "CEP" or "Latitude" or "Longitude");
    }

    [Fact]
    public void Nenhum_valor_de_endereco_ou_coordenada_da_fixture_aparece_nas_linhas()
    {
        var linhas = LinhasDaFixture();
        Assert.NotEmpty(linhas); // sem isto o teste passaria sem ler nada

        var celulas = linhas
            .SelectMany(l => typeof(LinhaBruta).GetProperties().Select(p => p.GetValue(l)))
            .OfType<string>()
            .ToList();

        Assert.DoesNotContain(celulas, c => c.Contains("Fictícia"));
        Assert.DoesNotContain(celulas, c => Regex.IsMatch(c, @"^-?\d{2}\.\d{4,}$")); // formato de lat/long
    }

    // --- Erros continuam na chamada, sem eco de célula -----------------------------------------

    [Fact]
    public void Cabecalho_obrigatorio_ausente_lanca_na_chamada_sem_enumerar()
    {
        using var xlsx = Criar(Aba, new[] { "Data de Abertura", "Tema" }, new string?[] { "x", "y" });

        var ex = Assert.Throws<ColunaObrigatoriaAusenteException>(() => LeitorSolicitacoes.LerLinhasBrutas(xlsx));

        Assert.Contains("Status", ex.Ausentes);
    }

    [Fact]
    public void Aba_ausente_lanca_na_chamada_sem_enumerar()
    {
        using var xlsx = Criar("Outra", CabecalhoDoArquivo, Linha(("Tema", "x")));

        Assert.Throws<InvalidDataException>(() => LeitorSolicitacoes.LerLinhasBrutas(xlsx));
    }

    [Fact]
    public void Mensagem_de_erro_nao_contem_valor_de_celula()
    {
        // 1ª linha é dado (CEP e endereço), não cabeçalho; a linha seguinte também é dado.
        using var xlsx = Criar(Aba, new[] { "05510001", "Rua Fictícia 1" }, new string?[] { "Tapa-buraco", "-23.580001" });

        var ex = Assert.Throws<ColunaObrigatoriaAusenteException>(() => LeitorSolicitacoes.LerLinhasBrutas(xlsx));

        Assert.DoesNotContain("05510001", ex.Message);
        Assert.DoesNotContain("Fictícia", ex.Message);
        Assert.DoesNotContain("Tapa-buraco", ex.Message);
        Assert.DoesNotContain("-23.580001", ex.Message);
    }
}
