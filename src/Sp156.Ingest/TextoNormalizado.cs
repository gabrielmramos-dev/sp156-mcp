using System.Globalization;
using System.Text;

namespace Sp156.Ingest;

public static class TextoNormalizado
{
    /// <summary>
    /// Forma comparável de um texto, para busca e agrupamento: NFC, sem espaços nas bordas, espaços
    /// internos colapsados em um, sem acentos e em minúsculas. Não é aplicada ao dado guardado.
    /// </summary>
    public static string Chave(string texto)
    {
        var sb = new StringBuilder(texto.Length);
        var espacoPendente = false;

        // NFC + trim primeiro; NFD depois, só para separar as marcas de acento e descartá-las.
        foreach (var c in texto.Normalize(NormalizationForm.FormC).Trim().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;

            if (char.IsWhiteSpace(c))
            {
                espacoPendente = true;
                continue;
            }

            if (espacoPendente) sb.Append(' ');
            espacoPendente = false;
            sb.Append(c);
        }

        return sb.ToString().ToLowerInvariant();
    }
}
