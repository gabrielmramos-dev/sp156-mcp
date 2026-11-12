namespace Sp156.Ingest;

/// <summary>
/// Linha da aba com SOMENTE as 10 colunas mapeadas, sem conversão: número vira <see cref="double"/>
/// (o serial da data), texto fica como veio e célula ausente ou vazia no arquivo vira <c>null</c>.
/// <see cref="Linha"/> é o número da linha na planilha (como o Excel mostra). As colunas de endereço
/// e coordenadas não existem aqui (ADR 0002).
/// </summary>
internal sealed record LinhaBruta(
    int Linha,
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
