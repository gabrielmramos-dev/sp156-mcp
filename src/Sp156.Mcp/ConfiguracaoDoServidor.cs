namespace Sp156.Mcp;

/// <summary>Configuração do servidor: o caminho do .db do Marco 1, que o servidor só lê.</summary>
public sealed record ConfiguracaoDoServidor(string CaminhoDoBanco)
{
    public const string Variavel = "SP156_DB";

    /// <summary>
    /// Resolve o caminho do banco: <c>--db &lt;caminho&gt;</c> vence a variável SP156_DB. Devolve a
    /// configuração ou uma mensagem de erro (nunca as duas).
    /// </summary>
    public static (ConfiguracaoDoServidor? Configuracao, string? Erro) Resolver(string[] args, Func<string, string?> variavel)
    {
        string? caminho = null;

        var indice = Array.IndexOf(args, "--db");
        if (indice >= 0)
        {
            var valor = indice + 1 < args.Length ? args[indice + 1] : null;
            if (string.IsNullOrWhiteSpace(valor) || valor.StartsWith("--", StringComparison.Ordinal))
                return (null, "O argumento --db exige o caminho do banco SQLite. Exemplo: --db pedidos.db");
            caminho = valor;
        }
        else if (variavel(Variavel) is { } daVariavel && !string.IsNullOrWhiteSpace(daVariavel))
        {
            caminho = daVariavel;
        }

        if (caminho is null)
            return (null, $"Informe o banco SQLite com --db <caminho> ou com a variável de ambiente {Variavel}.");

        if (!File.Exists(caminho))
            return (null, $"O banco '{caminho}' não existe.");

        return (new ConfiguracaoDoServidor(caminho), null);
    }
}
