using TitanMDM.Infrastructure.Persistence;

namespace TitanMDM.UnitTests.Inventory;

public sealed class ItamDateRulesTests
{
    private readonly ItamDateRules _rules = new("America/Santo_Domingo");

    [Fact]
    public void Today_UsesBusinessTimeZone_NotUtc()
    {
        // 01:30 UTC del 5 = 21:30 del 4 en RD.
        Assert.Equal(new DateOnly(2026, 11, 4), _rules.Today(new DateTimeOffset(2026, 11, 5, 1, 30, 0, TimeSpan.Zero)));
        Assert.Equal(new DateOnly(2026, 11, 5), _rules.Today(new DateTimeOffset(2026, 11, 5, 4, 0, 0, TimeSpan.Zero)));
    }

    [Theory]
    // Formato real de la tabla deliveries.
    [InlineData("2026-11-03T18:40:06.028520+00:00", 2026, 11, 3)]
    // 22:30 RD queda en el dia local, no en el UTC.
    [InlineData("2026-11-05T02:30:00.123456+00:00", 2026, 11, 4)]
    // Legado MongoDB sin zona = UTC.
    [InlineData("2026-11-05T02:30:00.597000", 2026, 11, 4)]
    [InlineData("2026-11-05T02:30:00Z", 2026, 11, 4)]
    // Fechas calendario se toman literales.
    [InlineData("2026-11-04", 2026, 11, 4)]
    [InlineData("04/11/2026", 2026, 11, 4)]
    public void ToBusinessDate_NormalizesAllStoredFormats(string value, int y, int m, int d)
    {
        Assert.Equal(new DateOnly(y, m, d), _rules.ToBusinessDate(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-es-fecha")]
    public void ToBusinessDate_InvalidValues_ReturnNull(string? value)
    {
        Assert.Null(_rules.ToBusinessDate(value));
    }

    [Fact]
    public void SortKey_OrdersChronologically_AcrossFormats()
    {
        var values = new[] { "2026-11-03T18:00:00Z", "2026-11-03T20:00:00.000000+00:00", "2026-11-03T19:00:00" };

        var ordered = values.OrderBy(ItamDateRules.SortKey).ToArray();

        Assert.Equal(new[] { "2026-11-03T18:00:00Z", "2026-11-03T19:00:00", "2026-11-03T20:00:00.000000+00:00" }, ordered);
    }

    [Fact]
    public void UnknownTimeZone_FallsBackToRd()
    {
        var rules = new ItamDateRules("Zona/Inexistente");

        Assert.Equal(TimeSpan.FromHours(-4), rules.TimeZone.GetUtcOffset(DateTime.UtcNow));
    }
}
