using Microsoft.Data.Sqlite;
using Sp156.Ingest;

namespace Sp156.Cli;

/// <summary>
/// Linha de comando. Códigos de saída: 0 sucesso, 1 erro esperado (arquivo, formato ou dado),
/// 2 uso incorreto. As mensagens nunca trazem conteúdo de célula (ADR 0002): só caminhos de arquivo,
/// nomes de coluna e número de linha.
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
            catch (Exception ex) when (ex is ColunaObrigatoriaAusenteException or LinhaInvalidaException or InvalidDataException)
            {
                erro.WriteLine($"Erro: {ex.Message}");
                return 1;
            }
            catch (Exception ex) when (ex is IOException or SqliteException)
            {
                erro.WriteLine($"Erro ao gravar '{destino}': {ex.Message}");
                return 1;
            }
        }
    }
}
