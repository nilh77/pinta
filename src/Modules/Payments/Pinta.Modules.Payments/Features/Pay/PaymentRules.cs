using Pinta.Modules.Payments.Contracts;

namespace Pinta.Modules.Payments.Features.Pay;

/// <summary>Methods offered by amount (docs/integrations.md, "Reglas en la pantalla de pago").</summary>
public static class PaymentRules
{
    /// <summary>Above this, MB WAY shows the usual 1.000 € daily limit.</summary>
    public const long MbWayWarningCents = 1_000_00;

    /// <summary>Above this, MB WAY is not offered (Stripe's per-transaction maximum).</summary>
    public const long MbWayMaxCents = 5_000_00;

    public static IReadOnlyList<PaymentMethod> AllowedMethods(long amountCents) => amountCents > MbWayMaxCents
        ? [PaymentMethod.Card, PaymentMethod.Multibanco]
        : [PaymentMethod.Card, PaymentMethod.MbWay, PaymentMethod.Multibanco];

    public static bool MbWayWarning(long amountCents) => amountCents > MbWayWarningCents && amountCents <= MbWayMaxCents;

    public static string StripeType(PaymentMethod method) => method switch
    {
        PaymentMethod.Card => "card",
        PaymentMethod.MbWay => "mb_way",
        PaymentMethod.Multibanco => "multibanco",
        _ => throw new ArgumentOutOfRangeException(nameof(method)),
    };

    public static PaymentMethod? FromStripeType(string? type) => type switch
    {
        "card" => PaymentMethod.Card,
        "mb_way" => PaymentMethod.MbWay,
        "multibanco" => PaymentMethod.Multibanco,
        _ => null,
    };
}
