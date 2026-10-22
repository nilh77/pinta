namespace Pinta.SharedKernel.Tests;

public class TaxIdTests
{
    [Theory]
    [InlineData("123456789")]
    [InlineData("999999990")]
    [InlineData("501442600")]
    [InlineData("451234561")]
    public void C4_3_accepts_valid_nifs(string nif) => Assert.True(TaxId.IsValid(nif), nif);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345678")]
    [InlineData("1234567890")]
    [InlineData("123456780")]
    [InlineData("12345678a")]
    [InlineData("012345678")]
    [InlineData("412345678")]
    public void C4_3_rejects_invalid_nifs(string? nif) => Assert.False(TaxId.IsValid(nif), nif);
}
