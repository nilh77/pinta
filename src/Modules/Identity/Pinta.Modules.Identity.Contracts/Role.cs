namespace Pinta.Modules.Identity.Contracts;

/// <summary>User role. Serialized as <c>client</c>, <c>painter</c>, <c>admin</c>.</summary>
public enum Role
{
    Client,
    Painter,
    Admin,
}

/// <summary>Role names as stored by ASP.NET Core Identity and used in authorization checks.</summary>
public static class RoleNames
{
    public const string Client = "client";
    public const string Painter = "painter";
    public const string Admin = "admin";
}
