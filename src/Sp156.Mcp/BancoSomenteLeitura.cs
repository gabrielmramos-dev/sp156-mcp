using Microsoft.Data.Sqlite;

namespace Sp156.Mcp;

/// <summary>Abre o .db sempre como somente leitura: o servidor nunca escreve nele.</summary>
public static class BancoSomenteLeitura
{
    public static SqliteConnection Abrir(string caminho)
    {
        var conexao = new SqliteConnection($"Data Source={caminho};Mode=ReadOnly;Pooling=False");
        conexao.Open();
        return conexao;
    }
}
