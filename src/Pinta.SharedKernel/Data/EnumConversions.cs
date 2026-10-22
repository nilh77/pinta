using System.Text.Json;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Pinta.SharedKernel.Data;

public static class EnumConversions
{
    /// <summary>Stores an enum as the same snake_case text the API uses (<c>job_general</c>, <c>pending_email</c>).</summary>
    public static PropertyBuilder<TEnum> HasSnakeCaseConversion<TEnum>(this PropertyBuilder<TEnum> property, int maxLength = 30)
        where TEnum : struct, Enum =>
        property
            .HasConversion(value => JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString()), text => Enum.Parse<TEnum>(text.Replace("_", ""), true))
            .HasMaxLength(maxLength);
}
