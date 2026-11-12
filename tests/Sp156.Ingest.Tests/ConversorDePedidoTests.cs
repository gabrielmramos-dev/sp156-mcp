using System.Globalization;
using Sp156.Ingest;

namespace Sp156.Ingest.Tests;

public class ConversorDePedidoTests
{
    // Linha bruta válida; os testes trocam só o que interessa.
    private static LinhaBruta Bruta(int linha = 7) => new(
        linha, 45663d, 45694d, "PORTAL", "Rua e bairro", "Reparos em asfalto", "Tapa-buraco",
        "Finalizada", "SMGI", "BUTANTA", "VILA SONIA");

    private static LinhaBruta Com(string coluna, object? valor, int linha = 7) => coluna switch
    {
        "Data de Abertura" => Bruta(linha) with { DataAbertura = valor },
        "Data de Finalização" => Bruta(linha) with { DataFinalizacao = valor },
        "Meio de Comunicação" => Bruta(linha) with { MeioComunicacao = valor },
        "Tema" => Bruta(linha) with { Tema = valor },
        "Assunto" => Bruta(linha) with { Assunto = valor },
        "Serviço" => Bruta(linha) with { Servico = valor },
        "Status" => Bruta(linha) with { Status = valor },
        "Órgão" => Bruta(linha) with { Orgao = valor },
        "Prefeitura Operacional" => Bruta(linha) with { PrefeituraOperacional = valor },
        "Distrito" => Bruta(linha) with { Distrito = valor },
        _ => throw new ArgumentException(coluna),
    };

    // --- Conversão de tipos --------------------------------------------------------------------

    [Fact]
    public void Serial_numerico_vira_DateOnly_sem_hora()
    {
        var pedido = ConversorDePedido.Converter(Bruta());

        Assert.Equal(new DateOnly(2025, 1, 6), pedido.DataAbertura);
        Assert.Equal(new DateOnly(2025, 2, 6), pedido.DataFinalizacao);
    }

    [Fact]
    public void Parte_fracionaria_do_serial_e_a_hora_e_e_descartada()
    {
        var pedido = ConversorDePedido.Converter(Com("Data de Abertura", 45663.75));

        Assert.Equal(new DateOnly(2025, 1, 6), pedido.DataAbertura);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Data_de_finalizacao_nula_ou_vazia_vira_null(object? valor)
    {
        Assert.Null(ConversorDePedido.Converter(Com("Data de Finalização", valor)).DataFinalizacao);
    }

    [Theory]
    [InlineData("Meio de Comunicação")]
    [InlineData("Prefeitura Operacional")]
    [InlineData("Distrito")]
    public void Colunas_opcionais_nulas_ou_vazias_viram_null(string coluna)
    {
        var nula = ConversorDePedido.Converter(Com(coluna, null));
        var vazia = ConversorDePedido.Converter(Com(coluna, ""));

        Assert.Equal(nula, vazia);
        Assert.Null(coluna switch
        {
            "Meio de Comunicação" => vazia.MeioComunicacao,
            "Prefeitura Operacional" => vazia.PrefeituraOperacional,
            _ => vazia.Distrito,
        });
    }

    [Fact]
    public void Texto_recebe_trim_mas_mantem_o_espaco_duplo_do_meio()
    {
        // Fidelidade à fonte: só as bordas mudam. A forma comparável é TextoNormalizado.Chave.
        var pedido = ConversorDePedido.Converter(Bruta() with { Assunto = "  Reparos em  Asfalto \t", Orgao = "INTEGRAÇÃO-SMGI/SGZ" });

        Assert.Equal("Reparos em  Asfalto", pedido.Assunto);
        Assert.Equal("INTEGRAÇÃO-SMGI/SGZ", pedido.Orgao); // acento e caixa intactos
        Assert.Equal("PORTAL", pedido.MeioComunicacao);
        Assert.Equal("Finalizada", pedido.Status);
    }

    [Fact]
    public void Texto_decomposto_nfd_vira_nfc()
    {
        var nfd = "Servic\u0327o";
        Assert.NotEqual("Serviço", nfd); // garante que o teste exercita NFD

        var pedido = ConversorDePedido.Converter(Bruta() with { Servico = nfd, Distrito = "Vila Sa\u0302nia" });

        Assert.Equal("Serviço", pedido.Servico);
        Assert.Equal("Vila Sânia", pedido.Distrito);
    }

    [Fact]
    public void Coluna_opcional_so_com_espacos_vira_null()
    {
        var pedido = ConversorDePedido.Converter(Bruta() with { Distrito = "   ", MeioComunicacao = "\t " });

        Assert.Null(pedido.Distrito);
        Assert.Null(pedido.MeioComunicacao);
    }

    [Fact]
    public void Valor_numerico_em_coluna_de_texto_vira_texto_com_cultura_invariante()
    {
        // Cultura atual com vírgula decimal: só InvariantCulture dá "1234.5".
        var virgula = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        virgula.NumberFormat.NumberDecimalSeparator = ",";
        var anterior = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = virgula;
        try
        {
            var pedido = ConversorDePedido.Converter(Bruta() with { Tema = 1234.5, Distrito = 1234.5 });

            Assert.Equal("1234.5", pedido.Tema);
            Assert.Equal("1234.5", pedido.Distrito);
        }
        finally
        {
            CultureInfo.CurrentCulture = anterior;
        }
    }

    // --- Colunas obrigatórias ------------------------------------------------------------------

    public static TheoryData<string, object?> ObrigatoriasAusentes()
    {
        var dados = new TheoryData<string, object?>();
        foreach (var coluna in new[] { "Data de Abertura", "Tema", "Assunto", "Serviço", "Status", "Órgão" })
        {
            dados.Add(coluna, null);
            dados.Add(coluna, "");
        }
        return dados;
    }

    [Theory]
    [MemberData(nameof(ObrigatoriasAusentes))]
    public void Obrigatoria_nula_ou_vazia_lanca_excecao_com_linha_e_coluna(string coluna, object? valor)
    {
        var ex = Assert.Throws<LinhaInvalidaException>(() => ConversorDePedido.Converter(Com(coluna, valor, linha: 7)));

        Assert.Equal(7, ex.Linha);
        Assert.Equal(coluna, ex.Coluna);
        Assert.Contains("7", ex.Message);
        Assert.Contains(coluna, ex.Message);
    }

    [Theory]
    [InlineData("Tema")]
    [InlineData("Assunto")]
    [InlineData("Serviço")]
    [InlineData("Status")]
    [InlineData("Órgão")]
    public void Texto_obrigatorio_que_fica_vazio_apos_o_trim_lanca_excecao_sem_ecoar_o_valor(string coluna)
    {
        var ex = Assert.Throws<LinhaInvalidaException>(() => ConversorDePedido.Converter(Com(coluna, " \t  ", linha: 12)));

        Assert.Equal(12, ex.Linha);
        Assert.Equal(coluna, ex.Coluna);
        Assert.Contains("12", ex.Message);
        Assert.Contains(coluna, ex.Message);
        Assert.DoesNotContain("\t", ex.Message);
    }

    // --- Data inválida -------------------------------------------------------------------------

    [Theory]
    [InlineData("Data de Abertura")]
    [InlineData("Data de Finalização")]
    public void Texto_em_coluna_de_data_lanca_excecao_sem_ecoar_o_valor(string coluna)
    {
        var ex = Assert.Throws<LinhaInvalidaException>(() => ConversorDePedido.Converter(Com(coluna, "texto-secreto-123", linha: 9)));

        Assert.Equal(9, ex.Linha);
        Assert.Equal(coluna, ex.Coluna);
        Assert.DoesNotContain("texto-secreto-123", ex.Message);
    }

    [Fact]
    public void Texto_so_com_espacos_na_data_de_finalizacao_nao_e_vazio_e_lanca_excecao()
    {
        // Só nulo e '' viram null; espaços são texto numa coluna de data.
        var ex = Assert.Throws<LinhaInvalidaException>(() => ConversorDePedido.Converter(Com("Data de Finalização", "   ")));

        Assert.Equal("Data de Finalização", ex.Coluna);
    }

    [Fact]
    public void Serial_fora_do_intervalo_lanca_excecao_com_linha_e_coluna_sem_ecoar_o_valor()
    {
        var ex = Assert.Throws<LinhaInvalidaException>(() => ConversorDePedido.Converter(Com("Data de Abertura", 98765432101d, linha: 4)));

        Assert.Equal(4, ex.Linha);
        Assert.Equal("Data de Abertura", ex.Coluna);
        Assert.DoesNotContain("98765432101", ex.Message);
    }
}
