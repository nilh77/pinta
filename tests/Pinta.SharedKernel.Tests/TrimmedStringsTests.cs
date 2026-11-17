using System.Text.Json;
using Pinta.SharedKernel.Http;

namespace Pinta.SharedKernel.Tests;

public class TrimmedStringsTests
{
    private sealed record SignUp(string Email, string Name, string Password, string? Notes, string ResetToken);

    private static JsonSerializerOptions Options()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        TrimmedStrings.Apply(options);
        return options;
    }

    [Fact]
    public void Request_text_loses_surrounding_spaces_but_passwords_and_tokens_stay_as_sent()
    {
        const string json = """{"email":"  ana@pinta.pt ","name":"\tAna Costa\n","password":"  secret with spaces ","notes":null,"resetToken":" abc "}""";

        var value = JsonSerializer.Deserialize<SignUp>(json, Options())!;

        Assert.Equal(new SignUp("ana@pinta.pt", "Ana Costa", "  secret with spaces ", null, " abc "), value);
    }

    [Fact]
    public void Inner_line_breaks_of_long_text_are_kept()
    {
        var value = JsonSerializer.Deserialize<SignUp>("""{"email":"a","name":"b","password":"c","notes":"  line 1\nline 2  ","resetToken":"t"}""", Options())!;

        Assert.Equal("line 1\nline 2", value.Notes);
    }

    [Fact]
    public void Responses_are_written_unchanged() =>
        Assert.Equal("""{"email":" x ","name":"n","password":"p","notes":null,"resetToken":"t"}""",
            JsonSerializer.Serialize(new SignUp(" x ", "n", "p", null, "t"), Options()));
}
