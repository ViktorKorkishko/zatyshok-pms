using Microsoft.EntityFrameworkCore;
using Npgsql;
using Zatyshok.Api.Common.Errors;
using Zatyshok.Domain.Common;

namespace Zatyshok.Tests.Api;

/// <summary>Which exceptions of the services become which HTTP answers.</summary>
public class KnownExceptionFilterTests
{
    private static DbUpdateException Postgres(string sqlState)
        => new("save failed", new PostgresException("error", "ERROR", "ERROR", sqlState));

    [Fact]
    public void NotFoundGives404WithTheMessage()
    {
        Assert.Equal((404, "Готель не знайдено."), KnownExceptionFilter.Map(new NotFoundException("Готель не знайдено.")));
    }

    [Fact]
    public void ForeignHotelGives403()
    {
        Assert.Equal(403, KnownExceptionFilter.Map(new HotelScopeException())?.Status);
    }

    [Fact]
    public void BrokenRuleAndForbiddenTransitionGive409WithTheMessage()
    {
        Assert.Equal((409, "Правило"), KnownExceptionFilter.Map(new DomainException("Правило")));
        Assert.Equal((409, "Перехід"), KnownExceptionFilter.Map(new InvalidTransitionException("Перехід")));
    }

    [Fact]
    public void ConcurrentChangeGives409()
    {
        Assert.Equal(409, KnownExceptionFilter.Map(new DbUpdateConcurrencyException("xmin"))?.Status);
    }

    [Theory]
    [InlineData(PostgresErrorCodes.UniqueViolation)]
    [InlineData(PostgresErrorCodes.ForeignKeyViolation)]
    public void UnforeseenConstraintGives409WithAGenericMessage(string sqlState)
    {
        var mapped = KnownExceptionFilter.Map(Postgres(sqlState));

        Assert.Equal(409, mapped?.Status);
        Assert.Matches("^[А-ЯІЇЄҐ]", mapped?.Detail);
    }

    [Fact]
    public void OtherExceptionsAreLeftToTheErrorHandler()
    {
        // A check constraint means a validator missed a rule: a bug, so 500 and an error in the log.
        Assert.Null(KnownExceptionFilter.Map(Postgres(PostgresErrorCodes.CheckViolation)));
        Assert.Null(KnownExceptionFilter.Map(new InvalidOperationException()));
    }
}
