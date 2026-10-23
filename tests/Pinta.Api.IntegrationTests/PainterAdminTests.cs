using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pinta.Modules.Notifications.Contracts;
using Pinta.Modules.Painters.Data;
using Pinta.Modules.Painters.Features;

namespace Pinta.Api.IntegrationTests;

public static class TestPainterAccounts
{
    public const string Iban = "PT50000201231234567890154";
    public const string OtherIban = "PT50003501239876543210921";

    /// <summary>The admin creates a painter in Sintra; returns the painter id, email and the invitation link query.</summary>
    public static async Task<(Guid PainterId, string Email, Dictionary<string, string> Link)> CreateAsync(
        PintaApiFactory factory, HttpClient admin, string[]? municipalities = null, string? iban = Iban)
    {
        var email = $"painter-{Guid.NewGuid():N}@example.test";
        var response = await admin.PostAsJsonAsync("/api/admin/painters",
            new { name = "Rui Almeida", email, phone = "+351911111111", specialties = new[] { "interior" }, municipalities = municipalities ?? ["Sintra"], iban }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var painterId = (await response.ReadJsonAsync()).GetProperty("id").GetGuid();
        var link = new Uri(Assert.Single(factory.Emails.To<PainterInvitationEmail>(email)).SignUpUrl);
        return (painterId, email, QueryHelpers.ParseQuery(link.Query).ToDictionary(q => q.Key, q => q.Value.ToString()));
    }

    /// <summary>The painter follows the invitation: a signed-in painter client.</summary>
    public static async Task<HttpClient> SignUpAsync(PintaApiFactory factory, Dictionary<string, string> link)
    {
        var client = await ApiClient.CreateAsync(factory);
        var accepted = await client.PostAsJsonAsync("/api/auth/accept-invite", new { userId = link["userId"], token = link["token"], password = "painter-password-1" }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        await client.RefreshAntiforgeryAsync();
        return client;
    }
}

/// <summary>A4.1–A4.4: the admin's painters, with the IBAN encrypted and masked, and the painter's own profile.</summary>
public class PainterAdminTests(PintaApiFactory factory)
{
    [Fact]
    public async Task A4_1_A4_2_the_admin_invites_a_painter_who_signs_up_and_becomes_active()
    {
        var admin = await ApiClient.CreateAdminAsync(factory);
        var (painterId, email, link) = await TestPainterAccounts.CreateAsync(factory, admin);

        var invited = await (await admin.GetAsync($"/api/admin/painters/{painterId}")).ReadJsonAsync();
        Assert.Equal(("invited", false, "PT50 •••• 0154"), (invited.GetProperty("status").GetString(), invited.GetProperty("signedUp").GetBoolean(), invited.GetProperty("ibanMasked").GetString()));
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/admin/painters/{painterId}/invite", new { }, ApiClient.Json)).StatusCode);
        Assert.Equal(2, factory.Emails.To<PainterInvitationEmail>(email).Count);

        await TestPainterAccounts.SignUpAsync(factory, link);

        var active = await (await admin.GetAsync($"/api/admin/painters/{painterId}")).ReadJsonAsync();
        Assert.Equal(("active", true), (active.GetProperty("status").GetString(), active.GetProperty("signedUp").GetBoolean()));
        await ProblemAssert.HasCodeAsync(await admin.PostAsJsonAsync($"/api/admin/painters/{painterId}/invite", new { }, ApiClient.Json), HttpStatusCode.Conflict, "invalid_transition");
        var raw = await (await admin.GetAsync("/api/admin/painters")).Content.ReadAsStringAsync();
        Assert.DoesNotContain(TestPainterAccounts.Iban, raw);
    }

    [Fact]
    public async Task A4_1_the_iban_is_encrypted_at_rest_and_never_returned()
    {
        var admin = await ApiClient.CreateAdminAsync(factory);
        var (painterId, _, _) = await TestPainterAccounts.CreateAsync(factory, admin);

        await using var scope = factory.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<PaintersDbContext>().Painters.AsNoTracking().SingleAsync(p => p.Id == painterId);
        Assert.DoesNotContain("0154", stored.IbanEncrypted!);
        Assert.Equal(TestPainterAccounts.Iban, scope.ServiceProvider.GetRequiredService<IbanProtector>().Unprotect(stored.IbanEncrypted!));
        var detail = await (await admin.GetAsync($"/api/admin/painters/{painterId}")).Content.ReadAsStringAsync();
        Assert.DoesNotContain(TestPainterAccounts.Iban, detail);
    }

    [Fact]
    public async Task A4_1_invalid_data_is_rejected()
    {
        var admin = await ApiClient.CreateAdminAsync(factory);
        var (_, email, _) = await TestPainterAccounts.CreateAsync(factory, admin);

        var taken = await admin.PostAsJsonAsync("/api/admin/painters", new { name = "Outro", email }, ApiClient.Json);
        var badIban = await admin.PostAsJsonAsync("/api/admin/painters", new { name = "Outro", email = TestData.UniqueEmail(), iban = "PT50000201231234567890155" }, ApiClient.Json);
        var badZone = await admin.PostAsJsonAsync("/api/admin/painters", new { name = "Outro", email = TestData.UniqueEmail(), municipalities = new[] { "Porto" } }, ApiClient.Json);

        await ProblemAssert.HasCodeAsync(taken, HttpStatusCode.Conflict, "email_taken");
        Assert.True((await ProblemAssert.HasCodeAsync(badIban, HttpStatusCode.UnprocessableEntity, "validation_failed")).GetProperty("errors").TryGetProperty("iban", out _));
        Assert.True((await ProblemAssert.HasCodeAsync(badZone, HttpStatusCode.UnprocessableEntity, "validation_failed")).GetProperty("errors").TryGetProperty("municipalities", out _));
        Assert.True(Iban.IsValidPortuguese("PT50 0002 0123 1234 5678 9015 4"));
    }

    [Fact]
    public async Task A4_2_a_deactivated_painter_cannot_sign_in_and_comes_back_active()
    {
        var admin = await ApiClient.CreateAdminAsync(factory);
        var (painterId, email, link) = await TestPainterAccounts.CreateAsync(factory, admin);
        await TestPainterAccounts.SignUpAsync(factory, link);

        await admin.PostAsJsonAsync($"/api/admin/painters/{painterId}/deactivate", new { }, ApiClient.Json);
        var blocked = await (await ApiClient.CreateAsync(factory)).LoginAsync(email, "painter-password-1");
        await admin.PostAsJsonAsync($"/api/admin/painters/{painterId}/activate", new { }, ApiClient.Json);
        var allowed = await (await ApiClient.CreateAsync(factory)).LoginAsync(email, "painter-password-1");

        Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.OK), (blocked.StatusCode, allowed.StatusCode));
        Assert.Equal("active", (await (await admin.GetAsync($"/api/admin/painters/{painterId}")).ReadJsonAsync()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task A4_3_documents_with_expiry_are_uploaded_by_the_admin()
    {
        var admin = await ApiClient.CreateAdminAsync(factory);
        var (painterId, _, _) = await TestPainterAccounts.CreateAsync(factory, admin);
        var content = new byte[3000];

        var upload = await (await admin.PostAsJsonAsync($"/api/admin/painters/{painterId}/documents/upload-url", new { contentType = "application/pdf", sizeBytes = content.Length }, ApiClient.Json)).ReadJsonAsync();
        Assert.True((await MediaTests.UploadToStorageAsync(upload, content, "application/pdf")).IsSuccessStatusCode);
        var document = await admin.PostAsJsonAsync($"/api/admin/painters/{painterId}/documents",
            new { fileId = upload.GetProperty("fileId").GetGuid(), kind = "insurance", expiresOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1) }, ApiClient.Json);
        var video = await admin.PostAsJsonAsync($"/api/admin/painters/{painterId}/documents/upload-url", new { contentType = "video/mp4", sizeBytes = 1000 }, ApiClient.Json);

        Assert.Equal(HttpStatusCode.OK, document.StatusCode);
        var doc = await document.ReadJsonAsync();
        Assert.Equal(("insurance", true), (doc.GetProperty("kind").GetString(), doc.GetProperty("expired").GetBoolean()));
        await ProblemAssert.HasCodeAsync(video, HttpStatusCode.UnsupportedMediaType, "unsupported_media_type");
        var detail = await (await admin.GetAsync($"/api/admin/painters/{painterId}")).ReadJsonAsync();
        var url = Assert.Single(detail.GetProperty("documents").EnumerateArray()).GetProperty("file").GetProperty("url").GetString()!;
        using var http = new HttpClient();
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync(url)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/painters/{painterId}/documents/{doc.GetProperty("id").GetGuid()}")).StatusCode);
    }

    [Fact]
    public async Task A4_2_the_painter_completes_the_profile_and_needs_the_password_to_change_the_iban()
    {
        var admin = await ApiClient.CreateAdminAsync(factory);
        var (_, _, link) = await TestPainterAccounts.CreateAsync(factory, admin, iban: null);
        var painter = await TestPainterAccounts.SignUpAsync(factory, link);

        var me = await (await painter.GetAsync("/api/painter/me")).ReadJsonAsync();
        var first = await painter.PutAsJsonAsync("/api/painter/me", new { phone = "+351922222222", municipalities = new[] { "Sintra", "Cascais" }, iban = TestPainterAccounts.Iban }, ApiClient.Json);
        var noPassword = await painter.PutAsJsonAsync("/api/painter/me", new { phone = "+351922222222", municipalities = new[] { "Sintra" }, iban = TestPainterAccounts.OtherIban }, ApiClient.Json);
        var withPassword = await painter.PutAsJsonAsync("/api/painter/me",
            new { phone = "+351922222222", municipalities = new[] { "Sintra" }, iban = TestPainterAccounts.OtherIban, currentPassword = "painter-password-1" }, ApiClient.Json);

        Assert.Contains("Sintra", me.GetProperty("availableMunicipalities").EnumerateArray().Select(m => m.GetString()));
        Assert.Equal("PT50 •••• 0154", (await first.ReadJsonAsync()).GetProperty("ibanMasked").GetString());
        await ProblemAssert.HasCodeAsync(noPassword, HttpStatusCode.UnprocessableEntity, "validation_failed");
        Assert.Equal("PT50 •••• 0921", (await withPassword.ReadJsonAsync()).GetProperty("ibanMasked").GetString());
        await ProblemAssert.HasCodeAsync(await (await ApiClient.CreateAdminAsync(factory)).GetAsync("/api/painter/me"), HttpStatusCode.Forbidden, "forbidden");
    }

    [Fact]
    public async Task Demo_painters_are_seeded()
    {
        var admin = await ApiClient.CreateAdminAsync(factory);

        var painters = (await (await admin.GetAsync("/api/admin/painters")).ReadJsonAsync()).EnumerateArray()
            .Where(p => p.GetProperty("email").GetString()!.EndsWith("@demo.pinta.pt")).ToDictionary(p => p.GetProperty("email").GetString()!);

        Assert.Equal("active", painters["rui@demo.pinta.pt"].GetProperty("status").GetString());
        Assert.Equal("invited", painters["joao@demo.pinta.pt"].GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await (await ApiClient.CreateAsync(factory)).LoginAsync("carla@demo.pinta.pt", TestData.DemoPassword)).StatusCode);
    }
}
