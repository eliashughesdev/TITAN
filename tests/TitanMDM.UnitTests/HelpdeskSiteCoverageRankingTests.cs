namespace TitanMDM.UnitTests;

public sealed class HelpdeskSiteCoverageRankingTests
{
    [Theory]
    [InlineData(
        true,
        true,
        true,
        0)]
    [InlineData(
        true,
        true,
        false,
        1)]
    [InlineData(
        false,
        true,
        true,
        2)]
    [InlineData(
        false,
        true,
        false,
        3)]
    public void CoverageSpecificityOrder(
        bool exactLocation,
        bool sameSite,
        bool categoryMatch,
        int expected)
    {
        var rank =
            GetRank(
                exactLocation,
                sameSite,
                categoryMatch);

        Assert.Equal(
            expected,
            rank);
    }

    [Fact]
    public void ExactLocationAndCategoryWins()
    {
        var ranks =
            new[]
            {
                GetRank(
                    false,
                    true,
                    false),

                GetRank(
                    false,
                    true,
                    true),

                GetRank(
                    true,
                    true,
                    false),

                GetRank(
                    true,
                    true,
                    true)
            };

        Assert.Equal(
            0,
            ranks.Min());
    }

    private static int GetRank(
        bool exactLocation,
        bool sameSite,
        bool categoryMatch)
    {
        if (!sameSite)
        {
            return int.MaxValue;
        }

        if (
            exactLocation
            &&
            categoryMatch)
        {
            return 0;
        }

        if (exactLocation)
        {
            return 1;
        }

        if (categoryMatch)
        {
            return 2;
        }

        return 3;
    }
}