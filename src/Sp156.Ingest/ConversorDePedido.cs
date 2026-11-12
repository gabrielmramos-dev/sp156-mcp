using System.Globalization;
using System.Text;

namespace Sp156.Ingest;

/// <summary>
/// Converte a linha bruta em <see cref="Pedido"/>. Textos recebem NFC e trim, nada além disso
/// (a forma comparável é <see cref="TextoNormalizado.Chave"/>, não guardada). Os erros informam a
/// linha e a coluna, nunca o conteúdo da célula.
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

    // NFC e trim; vazio depois disso vira null. O espaço do meio fica como veio (fidelidade à fonte).
    private static string? Opcional(object? valor)
    {
        var texto = valor switch
        {
            null => null,
            string s => s,
            _ => Convert.ToString(valor, CultureInfo.InvariantCulture),
        };

        texto = texto?.Normalize(NormalizationForm.FormC).Trim();
        return string.IsNullOrEmpty(texto) ? null : texto;
    }

    private static LinhaInvalidaException Vazia(int linha, string coluna) =>
        new(linha, coluna, $"Linha {linha}: a coluna obrigatória '{coluna}' está vazia.");

    private static LinhaInvalidaException DataInvalida(int linha, string coluna) =>
        new(linha, coluna, $"Linha {linha}: a coluna '{coluna}' não tem uma data válida.");
}
