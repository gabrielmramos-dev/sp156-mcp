namespace Sp156.Ingest;

/// <summary>
/// Marca, em <see cref="Exception.Data"/>, as exceções que o próprio Sp156.Ingest lança, para o CLI
/// distinguir "nossa" (mensagem segura, pode ser impressa) de uma exceção do mesmo tipo vinda de uma
/// biblioteca (a mensagem dela pode trazer conteúdo do arquivo). Fica interna ao produto.
/// </summary>
internal static class MarcaDeErro
{
    private const string Chave = "Sp156.Ingest.ErroProprio";

    public static T Marcar<T>(T excecao) where T : Exception
    {
        excecao.Data[Chave] = true;
        return excecao;
    }

    public static bool EhNossa(Exception excecao) => excecao.Data[Chave] is true;
}
