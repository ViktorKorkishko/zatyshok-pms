namespace Zatyshok.Domain.Modules.Guests;

/// <summary>Identity document type of a guest (table 12.24). FR-23 gives no list, so the values are an assumption.</summary>
public enum DocumentType
{
    /// <summary>Ukrainian passport booklet.</summary>
    Passport,

    /// <summary>Ukrainian ID card.</summary>
    IdCard,

    ForeignPassport,

    Other,
}
