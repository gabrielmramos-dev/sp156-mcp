using Microsoft.Data.Sqlite;
using Sp156.Ingest;

namespace Sp156.Cli;

/// <summary>
/// Linha de comando. Códigos de saída: 0 sucesso, 1 erro (esperado ou inesperado), 2 uso incorreto.
/// As mensagens nunca trazem conteúdo de célula (ADR 0002): só caminhos de arquivo, nomes de coluna,
/// número de linha e, no erro inesperado, o nome do tipo da exceção. A mensagem de uma exceção que
/// não é nossa nunca é impressa, porque uma biblioteca pode colocar nela conteúdo do arquivo.
/// </summary>
public static class Programa
{
    private const string Uso = """
        Uso: ingerir <arquivo.xlsx> <saida.db>

          Lê a aba "Solicitações" do XLSX do SP156 e grava os pedidos em um banco SQLite.
          O arquivo de saída não pode existir.

        Exemplo:
          dotnet run --project src/Sp156.Cli -- ingerir arquivo.xlsx pedidos.db
        """;

    public static int Main(string[] args) => Executar(args, Console.Out, Console.Error);

    public static int Executar(string[] args, TextWriter saida, TextWriter erro)
    {
        if (args.Length != 3 || args[0] != "ingerir")
        {
            if (args.Length > 0 && args[0] != "ingerir")
                erro.WriteLine($"Comando desconhecido: {args[0]}");
            erro.WriteLine(Uso);
            return 2;
        }

        return Ingerir(args[1], args[2], saida, erro);
    }

    private static int Ingerir(string xlsx, string destino, TextWriter saida, TextWriter erro)
    {
        // Antes de abrir o XLSX; o Gravar checa de novo, por causa da corrida.
        if (File.Exists(destino))
        {
            erro.WriteLine($"Erro ao gravar '{destino}': O arquivo de destino já existe.");
            return 1;
        }

        FileStream entrada;
        try
        {
            entrada = new FileStream(xlsx, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            erro.WriteLine($"Erro: arquivo não encontrado: {xlsx}");
            return 1;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            erro.WriteLine($"Erro ao abrir '{xlsx}': {ex.Message}");
            return 1;
        }

        using (entrada)
        {
            try
            {
                var gravadas = GravadorSqlite.Gravar(LeitorSolicitacoes.Ler(entrada), destino);
                saida.WriteLine($"{gravadas} pedidos gravados em {destino}");
                return 0;
            }
            catch (Exception ex) when (ex is ColunaObrigatoriaAusenteException or LinhaInvalidaException)
            {
                erro.WriteLine($"Erro: {ex.Message}");
                return 1;
            }
            catch (InvalidDataException ex) when (MarcaDeErro.EhNossa(ex))
            {
                erro.WriteLine($"Erro: {ex.Message}");
                return 1;
            }
            catch (Exception ex) when (ex is IOException or SqliteException)
            {
                erro.WriteLine($"Erro ao gravar '{destino}': {ex.Message}");
                return 1;
            }
            catch (Exception ex)
            {
                // Sem ex.Message nem stack trace: a mensagem de uma biblioteca pode trazer conteúdo do arquivo.
                erro.WriteLine($"Erro inesperado ao processar '{xlsx}' ({ex.GetType().Name}). O arquivo pode estar corrompido ou não ser um XLSX.");
                return 1;
            }
        }
    }
}
