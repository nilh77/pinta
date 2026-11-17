using Pinta.Modules.Notifications.Features.Push;

namespace Pinta.Api.IntegrationTests;

/// <summary>The VAPID contact the push services accept, however Push__Subject was filled in Railway.</summary>
public class PushOptionsTests
{
    [Theory]
    [InlineData("hola@pinta.pt", "mailto:hola@pinta.pt")]
    [InlineData(" mailto: hola@pinta.pt ", "mailto:hola@pinta.pt")]
    [InlineData("\"mailto:hola@pinta.pt\"", "mailto:hola@pinta.pt")]
    [InlineData("https://pinta.pt/contacto", "https://pinta.pt/contacto")]
    [InlineData("Pinta", PushOptions.DefaultSubject)]
    [InlineData("", PushOptions.DefaultSubject)]
    [InlineData(null, PushOptions.DefaultSubject)]
    public void The_subject_always_reaches_the_push_service_as_a_mailto_or_https_uri(string? configured, string expected) =>
        Assert.Equal(expected, PushOptions.NormalizeSubject(configured));
}
