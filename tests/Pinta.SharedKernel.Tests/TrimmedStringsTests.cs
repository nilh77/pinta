using Pinta.SharedKernel.Http;

namespace Pinta.SharedKernel.Tests;

public class TrimmedStringsTests
{
    private sealed record Line(string Description, string? Reference);

    private sealed record SignUp(string Email, string Name, string Password, string? Notes, string ResetToken, IReadOnlyList<Line> Lines, Line? Billing);

    [Fact]
    public void Request_text_loses_surrounding_spaces_but_passwords_and_tokens_stay_as_sent()
    {
        var request = new SignUp("  ana@pinta.pt ", "\tAna Costa\n", "  secret with spaces ", null, " abc ", [new(" Tinta ", " TRF-1 ")], new(" NIF ", null));

        TrimmedStrings.Trim(request);

        Assert.Equal(("ana@pinta.pt", "Ana Costa", "  secret with spaces ", " abc "), (request.Email, request.Name, request.Password, request.ResetToken));
        Assert.Equal(new Line("Tinta", "TRF-1"), request.Lines[0]);
        Assert.Equal(new Line("NIF", null), request.Billing);
    }

    [Fact]
    public void Inner_line_breaks_of_long_text_are_kept()
    {
        var request = new SignUp("a", "b", "c", "  line 1\nline 2  ", "t", [], null);

        TrimmedStrings.Trim(request);

        Assert.Equal("line 1\nline 2", request.Notes);
    }
}
