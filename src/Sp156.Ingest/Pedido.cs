namespace Sp156.Ingest;

public sealed record Pedido(
    DateOnly DataAbertura,
    DateOnly? DataFinalizacao,
    string? MeioComunicacao,
    string Tema,
    string Assunto,
    string Servico,
    string Status,
    string Orgao,
    string? PrefeituraOperacional,
    string? Distrito);
