using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using Sp156.Cli;
using static Sp156.Ingest.Tests.PlanilhaDeTeste;

namespace Sp156.Ingest.Tests;

public sealed class ProgramaTests : IDisposable
{
    private readonly string _pasta = Directory.CreateTempSubdirectory("sp156-cli-").FullName;

    private string Db => Path.Combine(_pasta, "pedidos.db");

    private static string Fixture => Path.Combine(AppContext.BaseDirectory, "Fixtures", "sp156_fixture.xlsx");

    public void Dispose() => Directory.Delete(_pasta, recursive: true);

    private static (int Codigo, string Saida, string Erro) Rodar(params string[] args)
    {
        var saida = new StringWriter();
        var erro = new StringWriter();
        var codigo = Programa.Executar(args, saida, erro);
        return (codigo, saida.ToString(), erro.ToString());
    }

    // Nenhuma saída pode trazer endereço, CEP ou coordenada da fixture (ADR 0002).
    private static void SemDadosDeEndereco(string saida, string erro)
    {
        var textos = new[] { saida, erro };
        Assert.All(textos, t =>
        {
            Assert.DoesNotContain("Fictícia", t);
            Assert.DoesNotContain("Praça", t);
        });
        var valores = ValoresDeEnderecoDaFixture();
        Assert.True(valores.Count > 20, "a fixture deveria ter valores de endereço para conferir");
        foreach (var valor in valores)
            Assert.All(textos, t => Assert.DoesNotContain(valor, t));
    }

    private string GravarXlsx(MemoryStream xlsx, string nome = "entrada.xlsx")
    {
        var caminho = Path.Combine(_pasta, nome);
        File.WriteAllBytes(caminho, xlsx.ToArray());
        return caminho;
    }

    private List<string> EntradasDaPasta() =>
        Directory.GetFileSystemEntries(_pasta).Select(p => Path.GetFileName(p)!).Order().ToList();

    private long Linhas(string db)
    {
        Assert.True(File.Exists(db), "o banco não foi criado");
        using var conexao = new SqliteConnection($"Data Source={db};Mode=ReadOnly;Pooling=False");
        conexao.Open();
        using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT count(*) FROM pedidos";
        return Convert.ToInt64(comando.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    // --- 1. Sucesso ----------------------------------------------------------------------------

    [Fact]
    public void Sucesso_devolve_0_grava_30_linhas_e_diz_quantos_pedidos_foram_gravados()
    {
        var (codigo, saida, erro) = Rodar("ingerir", Fixture, Db);

        Assert.Equal(0, codigo);
        Assert.Equal(30, Linhas(Db));
        Assert.Contains($"30 pedidos gravados em {Db}", saida);
        Assert.Equal("", erro);
        SemDadosDeEndereco(saida, erro);
        Assert.Equal(new[] { "pedidos.db" }, EntradasDaPasta());
    }

    // --- 2. Uso incorreto ----------------------------------------------------------------------

    public static TheoryData<string[]> ArgumentosErrados() => new()
    {
        Array.Empty<string>(),
        new[] { "ingerir" },
        new[] { "ingerir", "so-um-argumento.xlsx" },
        new[] { "arquivo.xlsx" },
        new[] { "ingerir", "a.xlsx", "b.db", "sobrando" },
        new[] { "comando-desconhecido", "a.xlsx", "b.db" },
    };

    [Theory]
    [MemberData(nameof(ArgumentosErrados))]
    public void Argumentos_errados_devolvem_2_e_o_texto_de_uso_na_saida_de_erro(string[] args)
    {
        var (codigo, saida, erro) = Rodar(args);

        Assert.Equal(2, codigo);
        Assert.Contains("Uso:", erro);
        Assert.Contains("ingerir <arquivo.xlsx> <saida.db>", erro);
        Assert.Equal("", saida);
        SemDadosDeEndereco(saida, erro);
        Assert.Empty(EntradasDaPasta());
    }

    // --- 3. Erros esperados --------------------------------------------------------------------

    [Fact]
    public void Xlsx_inexistente_devolve_1_com_o_caminho_na_saida_de_erro()
    {
        var inexistente = Path.Combine(_pasta, "nao-existe.xlsx");

        var (codigo, saida, erro) = Rodar("ingerir", inexistente, Db);

        Assert.Equal(1, codigo);
        Assert.Contains(inexistente, erro);
        Assert.Contains("não encontrado", erro);
        Assert.Equal("", saida);
        SemDadosDeEndereco(saida, erro);
        Assert.Empty(EntradasDaPasta());
    }

    [Fact]
    public void Destino_existente_devolve_1_e_o_arquivo_existente_fica_intacto()
    {
        var original = Encoding.UTF8.GetBytes("nao toque neste arquivo");
        File.WriteAllBytes(Db, original);

        var (codigo, saida, erro) = Rodar("ingerir", Fixture, Db);

        Assert.Equal(1, codigo);
        Assert.Contains("já existe", erro);
        Assert.Equal("", saida);
        Assert.Equal(original, File.ReadAllBytes(Db));
        SemDadosDeEndereco(saida, erro);
        Assert.Equal(new[] { "pedidos.db" }, EntradasDaPasta());
    }

    [Fact]
    public void Coluna_ausente_devolve_1_e_a_mensagem_lista_as_colunas_que_faltam()
    {
        var entrada = GravarXlsx(Criar(Aba, new[] { "Data de Abertura", "Tema" }, new string?[] { "05510001", "Rua Fictícia 1" }));

        var (codigo, saida, erro) = Rodar("ingerir", entrada, Db);

        Assert.Equal(1, codigo);
        foreach (var ausente in new[] { "Data de Finalização", "Meio de Comunicação", "Assunto", "Serviço", "Status", "Órgão", "Prefeitura Operacional", "Distrito" })
            Assert.Contains(ausente, erro);
        Assert.Equal("", saida);
        SemDadosDeEndereco(saida, erro);
        Assert.False(File.Exists(Db));
    }

    [Fact]
    public void Aba_ausente_devolve_1()
    {
        var entrada = GravarXlsx(Criar("Outra", CabecalhoDoArquivo, Linha(("Tema", "Rua Fictícia 1"))));

        var (codigo, saida, erro) = Rodar("ingerir", entrada, Db);

        Assert.Equal(1, codigo);
        Assert.Contains("Solicitações", erro);
        SemDadosDeEndereco(saida, erro);
        Assert.False(File.Exists(Db));
    }

    [Fact]
    public void Linha_invalida_devolve_1_com_o_numero_da_linha_sem_conteudo_de_celula_e_sem_deixar_arquivos()
    {
        var entrada = GravarXlsx(CriarComDatas(Aba, LinhaValida(), LinhaValida(("Tema", null), ("Assunto", "A-SEGREDO"))));

        var (codigo, saida, erro) = Rodar("ingerir", entrada, Db);

        Assert.Equal(1, codigo);
        Assert.Contains("Linha 3", erro);
        Assert.Contains("Tema", erro);
        Assert.DoesNotContain("SEGREDO", erro);
        Assert.Equal("", saida);
        SemDadosDeEndereco(saida, erro);
        Assert.Equal(new[] { "entrada.xlsx" }, EntradasDaPasta()); // nem .db nem .tmp
    }

    [Fact]
    public void Tmp_orfao_devolve_1_com_o_caminho_do_tmp_e_nao_o_toca()
    {
        var orfao = Encoding.UTF8.GetBytes("sobra de uma gravacao anterior");
        File.WriteAllBytes(Db + ".tmp", orfao);

        var (codigo, saida, erro) = Rodar("ingerir", Fixture, Db);

        Assert.Equal(1, codigo);
        Assert.Contains(Db + ".tmp", erro);
        Assert.Equal("", saida);
        Assert.Equal(orfao, File.ReadAllBytes(Db + ".tmp"));
        SemDadosDeEndereco(saida, erro);
        Assert.False(File.Exists(Db));
    }

    [Fact]
    public void Pasta_de_saida_inexistente_devolve_1_sem_excecao()
    {
        var destino = Path.Combine(_pasta, "nao-existe", "pedidos.db");

        var (codigo, saida, erro) = Rodar("ingerir", Fixture, destino);

        Assert.Equal(1, codigo);
        Assert.Contains(destino, erro);
        Assert.Equal("", saida);
        SemDadosDeEndereco(saida, erro);
    }
}
