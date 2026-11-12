using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Sp156.Ingest;

/// <summary>
/// Grava os pedidos em um arquivo SQLite, em uma tabela só. Escreve em "&lt;caminho&gt;.tmp" e renomeia
/// no fim, então o destino nunca fica pela metade. Nenhuma coluna de endereço existe (ADR 0002).
/// </summary>
public static class GravadorSqlite
{
    private const string FormatoData = "yyyy-MM-dd";

    private const string CriarTabela = """
        CREATE TABLE pedidos (
            data_abertura          TEXT NOT NULL,
            data_finalizacao       TEXT,
            meio_comunicacao       TEXT,
            tema                   TEXT NOT NULL,
            assunto                TEXT NOT NULL,
            servico                TEXT NOT NULL,
            status                 TEXT NOT NULL,
            orgao                  TEXT NOT NULL,
            prefeitura_operacional TEXT,
            distrito               TEXT,
            CONSTRAINT ck_data_abertura_formato CHECK (data_abertura GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
            CONSTRAINT ck_data_finalizacao_formato CHECK (data_finalizacao IS NULL OR data_finalizacao GLOB '[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]'),
            CONSTRAINT ck_obrigatorios_nao_vazios CHECK (tema <> '' AND assunto <> '' AND servico <> '' AND status <> '' AND orgao <> ''),
            CONSTRAINT ck_opcionais_nulo_ou_preenchido CHECK ((meio_comunicacao IS NULL OR meio_comunicacao <> '') AND (prefeitura_operacional IS NULL OR prefeitura_operacional <> '') AND (distrito IS NULL OR distrito <> ''))
        )
        """;

    private const string Inserir = """
        INSERT INTO pedidos (data_abertura, data_finalizacao, meio_comunicacao, tema, assunto, servico, status, orgao, prefeitura_operacional, distrito)
        VALUES ($data_abertura, $data_finalizacao, $meio_comunicacao, $tema, $assunto, $servico, $status, $orgao, $prefeitura_operacional, $distrito)
        """;

    /// <summary>
    /// Devolve a quantidade de linhas gravadas. Lança <see cref="IOException"/> se o destino (ou um
    /// ".tmp" de uma gravação anterior) já existir, sem tocar neles. Se a gravação falhar, o ".tmp" é apagado.
    /// </summary>
    public static long Gravar(IEnumerable<Pedido> pedidos, string caminho)
    {
        ArgumentNullException.ThrowIfNull(pedidos);
        ArgumentException.ThrowIfNullOrWhiteSpace(caminho);

        var temporario = caminho + ".tmp";
        if (File.Exists(caminho)) throw new IOException("O arquivo de destino já existe.");
        if (File.Exists(temporario)) throw new IOException("Existe um arquivo .tmp de uma gravação anterior.");

        try
        {
            var linhas = GravarNoTemporario(pedidos, temporario);
            File.Move(temporario, caminho, overwrite: false);
            return linhas;
        }
        catch
        {
            ApagarTemporario(temporario);
            throw;
        }
    }

    private static long GravarNoTemporario(IEnumerable<Pedido> pedidos, string temporario)
    {
        var conexaoTexto = new SqliteConnectionStringBuilder
        {
            DataSource = temporario,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString();

        using var conexao = new SqliteConnection(conexaoTexto);
        conexao.Open();

        using (var criar = conexao.CreateCommand())
        {
            criar.CommandText = CriarTabela;
            criar.ExecuteNonQuery();
        }

        long linhas = 0;
        using (var transacao = conexao.BeginTransaction())
        using (var comando = conexao.CreateCommand())
        {
            comando.Transaction = transacao;
            comando.CommandText = Inserir;
            var dataAbertura = comando.Parameters.Add("$data_abertura", SqliteType.Text);
            var dataFinalizacao = comando.Parameters.Add("$data_finalizacao", SqliteType.Text);
            var meio = comando.Parameters.Add("$meio_comunicacao", SqliteType.Text);
            var tema = comando.Parameters.Add("$tema", SqliteType.Text);
            var assunto = comando.Parameters.Add("$assunto", SqliteType.Text);
            var servico = comando.Parameters.Add("$servico", SqliteType.Text);
            var status = comando.Parameters.Add("$status", SqliteType.Text);
            var orgao = comando.Parameters.Add("$orgao", SqliteType.Text);
            var prefeitura = comando.Parameters.Add("$prefeitura_operacional", SqliteType.Text);
            var distrito = comando.Parameters.Add("$distrito", SqliteType.Text);
            comando.Prepare();

            foreach (var p in pedidos)
            {
                dataAbertura.Value = p.DataAbertura.ToString(FormatoData, CultureInfo.InvariantCulture);
                dataFinalizacao.Value = p.DataFinalizacao?.ToString(FormatoData, CultureInfo.InvariantCulture) ?? (object)DBNull.Value;
                meio.Value = p.MeioComunicacao ?? (object)DBNull.Value;
                tema.Value = p.Tema ?? (object)DBNull.Value;
                assunto.Value = p.Assunto ?? (object)DBNull.Value;
                servico.Value = p.Servico ?? (object)DBNull.Value;
                status.Value = p.Status ?? (object)DBNull.Value;
                orgao.Value = p.Orgao ?? (object)DBNull.Value;
                prefeitura.Value = p.PrefeituraOperacional ?? (object)DBNull.Value;
                distrito.Value = p.Distrito ?? (object)DBNull.Value;
                comando.ExecuteNonQuery();
                linhas++;
            }

            transacao.Commit();
        }

        return linhas;
    }

    // Melhor esforço: se a limpeza falhar, a exceção original é a que importa.
    private static void ApagarTemporario(string temporario)
    {
        foreach (var arquivo in new[] { temporario, temporario + "-journal", temporario + "-wal", temporario + "-shm" })
        {
            try
            {
                File.Delete(arquivo);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
