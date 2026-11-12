using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using Sylvan.Data.Excel;
using Sp156.Ingest;
using static Sp156.Ingest.Tests.PlanilhaDeTeste;

namespace Sp156.Ingest.Tests;

public sealed class GravadorSqliteTests : IDisposable
{
    private readonly string _pasta = Directory.CreateTempSubdirectory("sp156-tests-").FullName;

    private string Db => Path.Combine(_pasta, "pedidos.db");

    public void Dispose() => Directory.Delete(_pasta, recursive: true);

    private static Pedido Novo(string assunto = "Reparos em asfalto", string tema = "Rua e bairro") => new(
        new DateOnly(2025, 1, 6), null, "PORTAL", tema, assunto, "Tapa-buraco", "Aberto", "SMGI", "BUTANTA", null);

    private long Gravar(IEnumerable<Pedido> pedidos) => GravadorSqlite.Gravar(pedidos, Db);

    private long GravarFixture()
    {
        using var xlsx = AbrirFixture();
        return Gravar(LeitorSolicitacoes.Ler(xlsx));
    }

    private List<string> EntradasDaPasta() =>
        Directory.GetFileSystemEntries(_pasta).Select(p => Path.GetFileName(p)!).Order().ToList();

    private SqliteConnection Abrir(string? caminho = null)
    {
        caminho ??= Db;
        Assert.True(File.Exists(caminho), "o banco não foi criado");
        var conexao = new SqliteConnection($"Data Source={caminho};Mode=ReadOnly;Pooling=False");
        conexao.Open();
        return conexao;
    }

    private long Escalar(string sql)
    {
        using var conexao = Abrir();
        using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        return Convert.ToInt64(comando.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    // --- 1. Fixture v2 -> banco ----------------------------------------------------------------

    [Fact]
    public void Fixture_v2_grava_30_linhas_sendo_27_distintas_e_devolve_a_contagem()
    {
        var gravadas = GravarFixture();

        Assert.True(File.Exists(Db), "o banco não foi criado");
        Assert.Equal(30, gravadas);
        Assert.Equal(30, Escalar("SELECT count(*) FROM pedidos"));
        Assert.Equal(27, Escalar("SELECT count(*) FROM (SELECT DISTINCT * FROM pedidos)")); // ADR 0003: 3 duplicatas mantidas
    }

    [Fact]
    public void Primeira_linha_volta_com_as_datas_esperadas()
    {
        GravarFixture();

        using var conexao = Abrir();
        using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT data_abertura, data_finalizacao FROM pedidos ORDER BY rowid LIMIT 1";
        using var leitor = comando.ExecuteReader();

        Assert.True(leitor.Read());
        Assert.Equal("2025-01-06", leitor.GetString(0));
        Assert.Equal("2025-02-06", leitor.GetString(1));
    }

    [Fact]
    public void Ausencias_voltam_como_NULL_e_nunca_como_vazio()
    {
        GravarFixture();

        Assert.Equal(3, Escalar("SELECT count(*) FROM pedidos WHERE data_finalizacao IS NULL"));
        Assert.Equal(1, Escalar("SELECT count(*) FROM pedidos WHERE meio_comunicacao IS NULL"));
        Assert.Equal(11, Escalar("SELECT count(*) FROM pedidos WHERE distrito IS NULL"));
        Assert.Equal(0, Escalar("SELECT count(*) FROM pedidos WHERE data_finalizacao = '' OR meio_comunicacao = '' OR prefeitura_operacional = '' OR distrito = ''"));
    }

    [Fact]
    public void Status_somam_27_1_1_1()
    {
        GravarFixture();

        Assert.Equal(27, Escalar("SELECT count(*) FROM pedidos WHERE status = 'Finalizada'"));
        Assert.Equal(1, Escalar("SELECT count(*) FROM pedidos WHERE status = 'Aberto'"));
        Assert.Equal(1, Escalar("SELECT count(*) FROM pedidos WHERE status = 'Em Andamento'"));
        Assert.Equal(1, Escalar("SELECT count(*) FROM pedidos WHERE status = 'Cancelada'"));
    }

    [Fact]
    public void Os_pedidos_lidos_de_volta_do_banco_sao_iguais_aos_gravados_na_mesma_ordem()
    {
        List<Pedido> esperados;
        using (var xlsx = AbrirFixture()) esperados = LeitorSolicitacoes.Ler(xlsx).ToList();
        Gravar(esperados);

        var lidos = new List<Pedido>();
        using (var conexao = Abrir())
        using (var comando = conexao.CreateCommand())
        {
            comando.CommandText = "SELECT data_abertura, data_finalizacao, meio_comunicacao, tema, assunto, servico, status, orgao, prefeitura_operacional, distrito FROM pedidos ORDER BY rowid";
            using var leitor = comando.ExecuteReader();
            string? Texto(int i) => leitor.IsDBNull(i) ? null : leitor.GetString(i);
            DateOnly? Data(int i) => leitor.IsDBNull(i) ? null : DateOnly.ParseExact(leitor.GetString(i), "yyyy-MM-dd", CultureInfo.InvariantCulture);
            while (leitor.Read())
                lidos.Add(new Pedido(Data(0)!.Value, Data(1), Texto(2), Texto(3)!, Texto(4)!, Texto(5)!, Texto(6)!, Texto(7)!, Texto(8), Texto(9)));
        }

        Assert.Equal(esperados.Count, lidos.Count);
        Assert.Equal(esperados, lidos);
    }

    // --- 2. Esquema ----------------------------------------------------------------------------

    [Fact]
    public void Esquema_tem_so_as_10_colunas_e_nenhuma_de_endereco()
    {
        GravarFixture();

        var colunas = new List<(string Nome, string Tipo, bool NaoNulo)>();
        using (var conexao = Abrir())
        using (var comando = conexao.CreateCommand())
        {
            comando.CommandText = "PRAGMA table_info(pedidos)";
            using var leitor = comando.ExecuteReader();
            while (leitor.Read()) colunas.Add((leitor.GetString(1), leitor.GetString(2), leitor.GetInt64(3) == 1));
        }

        Assert.Equal(new[]
        {
            "data_abertura", "data_finalizacao", "meio_comunicacao", "tema", "assunto",
            "servico", "status", "orgao", "prefeitura_operacional", "distrito",
        }, colunas.Select(c => c.Nome));
        Assert.All(colunas, c => Assert.Equal("TEXT", c.Tipo));
        Assert.Equal(new[] { "data_abertura", "tema", "assunto", "servico", "status", "orgao" },
            colunas.Where(c => c.NaoNulo).Select(c => c.Nome));
        Assert.DoesNotContain(colunas, c => c.Nome is "logradouro" or "numero" or "cep" or "latitude" or "longitude");
    }

    // --- 3. Privacidade nos bytes do .db -------------------------------------------------------

    [Fact]
    public void Bytes_do_db_nao_contem_endereco_nem_coordenada_da_fixture()
    {
        GravarFixture();
        Assert.True(File.Exists(Db), "o banco não foi criado");
        var bytes = File.ReadAllBytes(Db);

        static bool Contem(byte[] bytes, string texto) => bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(texto)) >= 0;

        // Exemplos literais.
        foreach (var proibido in new[] { "Fictícia", "Praça", "08382635", "-23.58", "-46.73" })
            Assert.False(Contem(bytes, proibido), $"o .db contém '{proibido}'");

        // Todos os valores de Logradouro, CEP, Latitude e Longitude da fixture (de 7+ caracteres,
        // para não casar por acaso com "0" ou com números curtos).
        var valores = ValoresDeEnderecoDaFixture();
        Assert.True(valores.Count > 20, "a fixture deveria ter valores de endereço para conferir");
        foreach (var valor in valores)
            Assert.False(Contem(bytes, valor), "o .db contém um valor de endereço da fixture");
    }

    private static HashSet<string> ValoresDeEnderecoDaFixture()
    {
        var valores = new HashSet<string>();
        using var xlsx = AbrirFixture();
        using var leitor = ExcelDataReader.Create(xlsx, ExcelWorkbookType.ExcelXml);
        Assert.True(leitor.TryOpenWorksheet(Aba));
        var posicoes = new[] { "Logradouro", "CEP", "Latitude", "Longitude" }
            .Select(nome => Enumerable.Range(0, leitor.FieldCount).First(i => leitor.GetName(i) == nome))
            .ToArray();
        while (leitor.Read())
            foreach (var i in posicoes)
                if (i < leitor.RowFieldCount && leitor.GetString(i).Length >= 7)
                    valores.Add(leitor.GetString(i));
        return valores;
    }

    // --- 4. Destino existente e falha no meio --------------------------------------------------

    [Fact]
    public void Destino_existente_lanca_IOException_e_nao_e_tocado()
    {
        var original = Encoding.UTF8.GetBytes("conteudo que nao pode mudar");
        File.WriteAllBytes(Db, original);

        Assert.Throws<IOException>(() => Gravar(new[] { Novo() }));

        Assert.Equal(original, File.ReadAllBytes(Db));
        Assert.Equal(new[] { "pedidos.db" }, EntradasDaPasta());
    }

    [Fact]
    public void Destino_que_aparece_antes_do_rename_lanca_IOException_e_apaga_o_tmp()
    {
        var original = Encoding.UTF8.GetBytes("apareceu durante a gravacao");

        IEnumerable<Pedido> ComDestinoQueAparece()
        {
            yield return Novo();
            File.WriteAllBytes(Db, original); // depois da checagem inicial, antes do rename
            yield return Novo();
        }

        Assert.Throws<IOException>(() => Gravar(ComDestinoQueAparece()));

        Assert.Equal(original, File.ReadAllBytes(Db));
        Assert.Equal(new[] { "pedidos.db" }, EntradasDaPasta());
    }

    [Fact]
    public void Tmp_de_gravacao_anterior_lanca_IOException_sem_ser_tocado()
    {
        var anterior = Encoding.UTF8.GetBytes("sobra de uma gravacao anterior");
        File.WriteAllBytes(Db + ".tmp", anterior);

        Assert.Throws<IOException>(() => Gravar(new[] { Novo() }));

        Assert.Equal(anterior, File.ReadAllBytes(Db + ".tmp"));
        Assert.False(File.Exists(Db));
    }

    [Fact]
    public void Mensagem_do_tmp_orfao_cita_o_caminho_e_diz_que_pode_ser_apagado_sem_trazer_celulas()
    {
        File.WriteAllBytes(Db + ".tmp", Encoding.UTF8.GetBytes("sobra"));

        var ex = Assert.Throws<IOException>(() => Gravar(new[] { Novo(assunto: "A-SEGREDO") }));

        Assert.Contains(Db + ".tmp", ex.Message);
        Assert.Contains("pode ser apagado", ex.Message);
        Assert.Contains("nenhuma outra gravação", ex.Message);
        Assert.DoesNotContain("SEGREDO", ex.Message);
    }

    [Fact]
    public void Pedido_que_viola_o_esquema_lanca_SqliteException_sem_ecoar_celulas_e_nao_deixa_arquivos()
    {
        var pedidos = new[]
        {
            Novo(assunto: "A-SEGREDO-1"),
            Novo(assunto: "B-SEGREDO-2", tema: null!), // NOT NULL
        };

        var ex = Assert.Throws<SqliteException>(() => Gravar(pedidos));

        Assert.DoesNotContain("SEGREDO", ex.Message);
        Assert.Empty(EntradasDaPasta());
    }

    [Fact]
    public void Enumeracao_que_falha_no_meio_nao_deixa_arquivos()
    {
        IEnumerable<Pedido> Falha()
        {
            yield return Novo();
            throw new InvalidOperationException("falha simulada");
        }

        Assert.Throws<InvalidOperationException>(() => Gravar(Falha()));

        Assert.Empty(EntradasDaPasta());
    }

    // --- 5. Só o .db fica ----------------------------------------------------------------------

    [Fact]
    public void Depois_da_gravacao_a_pasta_tem_so_o_db()
    {
        GravarFixture();

        Assert.Equal(new[] { "pedidos.db" }, EntradasDaPasta());
    }

    // --- 7. Cada CHECK morde, sem ecoar o valor ------------------------------------------------

    private static readonly Dictionary<string, string> LinhaValida = new()
    {
        ["data_abertura"] = "'2025-01-06'",
        ["data_finalizacao"] = "NULL",
        ["meio_comunicacao"] = "'M-SEGREDO'",
        ["tema"] = "'T-SEGREDO'",
        ["assunto"] = "'A-SEGREDO'",
        ["servico"] = "'S-SEGREDO'",
        ["status"] = "'Finalizada'",
        ["orgao"] = "'O-SEGREDO'",
        ["prefeitura_operacional"] = "'P-SEGREDO'",
        ["distrito"] = "'D-SEGREDO'",
    };

    private SqliteCommand Insercao(SqliteConnection conexao, params (string Coluna, string ValorSql)[] trocas)
    {
        var valores = new Dictionary<string, string>(LinhaValida);
        foreach (var (coluna, valorSql) in trocas) valores[coluna] = valorSql;

        var comando = conexao.CreateCommand();
        comando.CommandText = $"INSERT INTO pedidos ({string.Join(", ", valores.Keys)}) VALUES ({string.Join(", ", valores.Values)})";
        return comando;
    }

    private SqliteConnection AbrirParaEscrita()
    {
        Gravar(new[] { Novo() });
        Assert.True(File.Exists(Db), "o banco não foi criado");
        var conexao = new SqliteConnection($"Data Source={Db};Mode=ReadWrite;Pooling=False");
        conexao.Open();
        return conexao;
    }

    [Fact]
    public void Linha_valida_inserida_por_SQL_cru_e_aceita()
    {
        using var conexao = AbrirParaEscrita();
        using var comando = Insercao(conexao);

        Assert.Equal(1, comando.ExecuteNonQuery());
    }

    [Theory]
    [InlineData("data_abertura", "'2025/01/06'", "ck_data_abertura_formato")]
    [InlineData("data_abertura", "'2025-1-6'", "ck_data_abertura_formato")]
    [InlineData("data_abertura", "'2025-01-06T10:00'", "ck_data_abertura_formato")]
    [InlineData("data_finalizacao", "'ontem-feira'", "ck_data_finalizacao_formato")]
    [InlineData("data_finalizacao", "''", "ck_data_finalizacao_formato")]
    [InlineData("tema", "''", "ck_obrigatorios_nao_vazios")]
    [InlineData("assunto", "''", "ck_obrigatorios_nao_vazios")]
    [InlineData("servico", "''", "ck_obrigatorios_nao_vazios")]
    [InlineData("status", "''", "ck_obrigatorios_nao_vazios")]
    [InlineData("orgao", "''", "ck_obrigatorios_nao_vazios")]
    [InlineData("meio_comunicacao", "''", "ck_opcionais_nulo_ou_preenchido")]
    [InlineData("prefeitura_operacional", "''", "ck_opcionais_nulo_ou_preenchido")]
    [InlineData("distrito", "''", "ck_opcionais_nulo_ou_preenchido")]
    public void Violacao_de_CHECK_lanca_SqliteException_com_o_nome_da_restricao_sem_o_valor(string coluna, string valorSql, string restricao)
    {
        using var conexao = AbrirParaEscrita();
        using var comando = Insercao(conexao, (coluna, valorSql));

        var ex = Assert.Throws<SqliteException>(() => comando.ExecuteNonQuery());

        Assert.Contains(restricao, ex.Message);
        Assert.DoesNotContain("SEGREDO", ex.Message);
        var valorInserido = valorSql.Trim('\'');
        if (valorInserido.Length > 0) // '' não tem o que ecoar
            Assert.DoesNotContain(valorInserido, ex.Message.Replace(restricao, ""), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("data_abertura")]
    [InlineData("tema")]
    [InlineData("assunto")]
    [InlineData("servico")]
    [InlineData("status")]
    [InlineData("orgao")]
    public void Obrigatorio_NULL_viola_NOT_NULL_com_o_nome_da_coluna_sem_o_valor(string coluna)
    {
        using var conexao = AbrirParaEscrita();
        using var comando = Insercao(conexao, (coluna, "NULL"));

        var ex = Assert.Throws<SqliteException>(() => comando.ExecuteNonQuery());

        Assert.Contains($"pedidos.{coluna}", ex.Message);
        Assert.DoesNotContain("SEGREDO", ex.Message);
    }
}
