using System.Globalization;

namespace Sp156.Ingest;

/// <summary>
/// Converte a linha bruta em <see cref="Pedido"/>. Só converte tipos; textos passam como vieram
/// (a normalização é outra etapa). Os erros informam a linha e a coluna, nunca o conteúdo da célula.
/// </summary>
internal static class ConversorDePedido
{
    public static Pedido Converter(LinhaBruta l) => new(
        DataAbertura: Data(l.Linha, "Data de Abertura", l.DataAbertura)
                      ?? throw Vazia(l.Linha, "Data de Abertura"),
        DataFinalizacao: Data(l.Linha, "Data de Finalização", l.DataFinalizacao),
        MeioComunicacao: Opcional(l.MeioComunicacao),
        Tema: Obrigatorio(l.Linha, "Tema", l.Tema),
        Assunto: Obrigatorio(l.Linha, "Assunto", l.Assunto),
        Servico: Obrigatorio(l.Linha, "Serviço", l.Servico),
        Status: Obrigatorio(l.Linha, "Status", l.Status),
        Orgao: Obrigatorio(l.Linha, "Órgão", l.Orgao),
        PrefeituraOperacional: Opcional(l.PrefeituraOperacional),
        Distrito: Opcional(l.Distrito));

    // Nulo ou '' vira null; número é o serial do Excel (a parte fracionária é a hora e se descarta).
    private static DateOnly? Data(int linha, string coluna, object? valor)
    {
        switch (valor)
        {
            case null or "":
                return null;
            case double serial:
                try
                {
                    return DateOnly.FromDateTime(DateTime.FromOADate(serial));
                }
                catch (ArgumentException)
                {
                    throw DataInvalida(linha, coluna);
                }
            default:
                throw DataInvalida(linha, coluna);
        }
    }

    private static string Obrigatorio(int linha, string coluna, object? valor) =>
        Opcional(valor) ?? throw Vazia(linha, coluna);

    private static string? Opcional(object? valor) => valor switch
    {
        null or "" => null,
        string texto => texto,
        _ => Convert.ToString(valor, CultureInfo.InvariantCulture),
    };

    private static LinhaInvalidaException Vazia(int linha, string coluna) =>
        new(linha, coluna, $"Linha {linha}: a coluna obrigatória '{coluna}' está vazia.");

    private static LinhaInvalidaException DataInvalida(int linha, string coluna) =>
        new(linha, coluna, $"Linha {linha}: a coluna '{coluna}' não tem uma data válida.");
}
