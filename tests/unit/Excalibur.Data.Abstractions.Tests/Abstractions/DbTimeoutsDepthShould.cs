namespace Excalibur.Data.Abstractions.Tests.Abstractions;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DbTimeoutsDepthShould
{
    [Fact]
    public void RegularTimeout_Is60Seconds()
    {
        DbTimeouts.RegularTimeoutSeconds.ShouldBe(60);
    }

    [Fact]
    public void LongRunningTimeout_Is600Seconds()
    {
        DbTimeouts.LongRunningTimeoutSeconds.ShouldBe(600);
    }

    [Fact]
    public void ExtraLongRunningTimeout_Is1200Seconds()
    {
        DbTimeouts.ExtraLongRunningTimeoutSeconds.ShouldBe(1200);
    }

    [Fact]
    public void Timeouts_AreInAscendingOrder()
    {
        DbTimeouts.RegularTimeoutSeconds
            .ShouldBeLessThan(DbTimeouts.LongRunningTimeoutSeconds);

        DbTimeouts.LongRunningTimeoutSeconds
            .ShouldBeLessThan(DbTimeouts.ExtraLongRunningTimeoutSeconds);
    }

    [Fact]
    public void AllTimeouts_ArePositive()
    {
        DbTimeouts.RegularTimeoutSeconds.ShouldBeGreaterThan(0);
        DbTimeouts.LongRunningTimeoutSeconds.ShouldBeGreaterThan(0);
        DbTimeouts.ExtraLongRunningTimeoutSeconds.ShouldBeGreaterThan(0);
    }
}
