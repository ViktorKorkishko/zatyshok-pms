namespace Zatyshok.Domain.Modules.Billing;

/// <summary>Money received or returned (UC-13 A1). The amount is always positive; the kind gives the sign.</summary>
public enum PaymentKind
{
    Payment,
    Refund,
}
