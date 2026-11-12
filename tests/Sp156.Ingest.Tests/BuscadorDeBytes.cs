using System.Buffers;
using System.Text;

namespace Sp156.Ingest.Tests;

/// <summary>
/// Procura valores, como bytes UTF-8, num arquivo lido em blocos com sobreposição, sem carregá-lo inteiro.
/// Cada byte vira um char (Latin-1), o que mantém a busca byte a byte e deixa usar SearchValues para
/// várias cadeias de uma vez. Código só de teste.
/// </summary>
internal static class BuscadorDeBytes
{
    public const int BlocoPadrao = 4 * 1024 * 1024;

    public static HashSet<(string Coluna, string Valor)> Procurar(
        Stream arquivo, IReadOnlyList<(string Coluna, string Valor)> padroes, int tamanhoDoBloco = BlocoPadrao)
    {
        var acertos = new HashSet<(string Coluna, string Valor)>();
        var chaves = padroes
            .Where(p => p.Valor.Length > 0)
            .Select(p => (p.Coluna, p.Valor, Chave: Encoding.Latin1.GetString(Encoding.UTF8.GetBytes(p.Valor))))
            .ToList();
        if (chaves.Count == 0) return acertos;

        var busca = SearchValues.Create(chaves.Select(c => c.Chave).Distinct().ToArray(), StringComparison.Ordinal);
        var maior = chaves.Max(c => c.Chave.Length);

        var bytes = new byte[tamanhoDoBloco];
        var chars = new char[tamanhoDoBloco + maior];
        var sobra = 0;

        int lidos;
        while ((lidos = arquivo.Read(bytes, 0, bytes.Length)) > 0)
        {
            Encoding.Latin1.GetChars(bytes, 0, lidos, chars, sobra);
            var total = sobra + lidos;
            var trecho = chars.AsSpan(0, total);

            var inicio = 0;
            while (inicio < total)
            {
                var i = trecho[inicio..].IndexOfAny(busca);
                if (i < 0) break;
                var posicao = inicio + i;
                foreach (var (coluna, valor, chave) in chaves)
                    if (trecho[posicao..].StartsWith(chave, StringComparison.Ordinal))
                        acertos.Add((coluna, valor));
                inicio = posicao + 1;
            }

            // Guarda o fim do bloco para achar o que cruza a fronteira com o próximo.
            sobra = Math.Min(maior - 1, total);
            trecho[^sobra..].CopyTo(chars);
        }

        return acertos;
    }
}
