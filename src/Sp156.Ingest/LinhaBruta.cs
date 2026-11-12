namespace Sp156.Ingest;

/// <summary>
/// Linha da aba com SOMENTE as 10 colunas mapeadas, sem conversão: número vira <see cref="double"/>
/// (o serial da data), texto fica como veio e célula ausente ou vazia no arquivo vira <c>null</c>.
/// As colunas de endereço e coordenadas não existem aqui (ADR 0002).
/// </summary>
internal sealed record LinhaBruta(
    object? DataAbertura,
    object? DataFinalizacao,
    object? MeioComunicacao,
    object? Tema,
    object? Assunto,
    object? Servico,
    object? Status,
    object? Orgao,
    object? PrefeituraOperacional,
    object? Distrito);
