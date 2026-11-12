namespace Sp156.Ingest.Tests;

[Collection("Variavel de ambiente SP156_XLSX")]
public class FactArquivoRealAttributeTests
{
    private static string CaminhoDaFixture =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "sp156_fixture.xlsx");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sem_variavel_o_motivo_do_skip_orienta_a_defini_la(string? caminho)
    {
        var motivo = FactArquivoRealAttribute.MotivoDoSkip(caminho);

        Assert.NotNull(motivo);
        Assert.Contains("SP156_XLSX", motivo);
    }

    [Fact]
    public void Arquivo_inexistente_gera_skip()
    {
        var motivo = FactArquivoRealAttribute.MotivoDoSkip(Path.Combine(AppContext.BaseDirectory, "nao-existe.xlsx"));

        Assert.NotNull(motivo);
        Assert.Contains("SP156_XLSX", motivo);
    }

    [Fact]
    public void Arquivo_existente_nao_gera_skip()
    {
        Assert.Null(FactArquivoRealAttribute.MotivoDoSkip(CaminhoDaFixture));
    }

    [Fact]
    public void Atributo_define_Skip_quando_a_variavel_nao_existe_e_nao_define_quando_aponta_para_arquivo()
    {
        var anterior = Environment.GetEnvironmentVariable(FactArquivoRealAttribute.Variavel);
        try
        {
            Environment.SetEnvironmentVariable(FactArquivoRealAttribute.Variavel, null);
            Assert.NotNull(new FactArquivoRealAttribute().Skip);

            Environment.SetEnvironmentVariable(FactArquivoRealAttribute.Variavel, CaminhoDaFixture);
            Assert.Null(new FactArquivoRealAttribute().Skip);
        }
        finally
        {
            Environment.SetEnvironmentVariable(FactArquivoRealAttribute.Variavel, anterior);
        }
    }
}
