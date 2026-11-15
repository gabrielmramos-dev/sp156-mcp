namespace Sp156.Mcp;

/// <summary>
/// O que é lido do banco inteiro uma vez por processo: o catálogo (§6.3) e as medidas das limitações (§6.4).
/// O .db é somente leitura e não muda durante a vida do processo, então guardar é seguro. A leitura é feita
/// na primeira vez que for preciso, não ao criar: o servidor sobe e lista as ferramentas sem abrir o banco.
/// </summary>
public sealed class DadosDoBanco
{
    private readonly Lazy<Catalogo> _catalogo;
    private readonly Lazy<MedidasDoBanco> _medidas;

    public DadosDoBanco(ConfiguracaoDoServidor configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        // Lazy<T> padrão é thread-safe (ExecutionAndPublication): chamadas simultâneas leem uma vez só.
        _catalogo = new(() =>
        {
            using var conexao = BancoSomenteLeitura.Abrir(configuracao.CaminhoDoBanco);
            return Catalogo.Carregar(conexao);
        });
        _medidas = new(() =>
        {
            using var conexao = BancoSomenteLeitura.Abrir(configuracao.CaminhoDoBanco);
            return Limitacoes.Medir(conexao);
        });
    }

    public Catalogo Catalogo => _catalogo.Value;

    public MedidasDoBanco Medidas => _medidas.Value;
}
