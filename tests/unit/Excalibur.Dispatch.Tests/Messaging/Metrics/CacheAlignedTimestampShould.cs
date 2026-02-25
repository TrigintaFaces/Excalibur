using Excalibur.Dispatch.Metrics;

namespace Excalibur.Dispatch.Tests.Messaging.Metrics;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CacheAlignedTimestampShould
{
	[Fact]
	public void DefaultToZero()
	{
		var ts = new CacheAlignedTimestamp();

		ts.Ticks.ShouldBe(0);
		ts.PerformanceTimestamp.ShouldBe(0);
	}

	[Fact]
	public void CreateNowWithNonZeroValues()
	{
		var ts = CacheAlignedTimestamp.Now();

		ts.Ticks.ShouldBeGreaterThan(0);
		ts.PerformanceTimestamp.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void ComputeDateTimeFromTicks()
	{
		var ts = CacheAlignedTimestamp.Now();

		ts.DateTime.Year.ShouldBeGreaterThanOrEqualTo(2000);
	}

	[Fact]
	public void UpdateNow()
	{
		var ts = new CacheAlignedTimestamp();

		ts.UpdateNow();

		ts.Ticks.ShouldBeGreaterThan(0);
		ts.PerformanceTimestamp.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void UpdateHighResolution()
	{
		var ts = new CacheAlignedTimestamp();

		ts.UpdateHighResolution();

		ts.Ticks.ShouldBeGreaterThan(0);
		ts.PerformanceTimestamp.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void GetElapsedMilliseconds()
	{
		var ts = CacheAlignedTimestamp.Now();

		// Should return a non-negative elapsed time
		var elapsed = ts.GetElapsedMilliseconds();

		elapsed.ShouldBeGreaterThanOrEqualTo(0);
	}

	[Fact]
	public void SupportEquality()
	{
		var ts1 = new CacheAlignedTimestamp();
		var ts2 = new CacheAlignedTimestamp();

		ts1.Equals(ts2).ShouldBeTrue();
		(ts1 == ts2).ShouldBeTrue();
	}

	[Fact]
	public void SupportInequality()
	{
		var ts1 = CacheAlignedTimestamp.Now();
		var ts2 = new CacheAlignedTimestamp();

		ts1.Equals(ts2).ShouldBeFalse();
		(ts1 != ts2).ShouldBeTrue();
	}

	[Fact]
	public void SupportEqualsWithObject()
	{
		var ts = new CacheAlignedTimestamp();

		ts.Equals((object)new CacheAlignedTimestamp()).ShouldBeTrue();
		ts.Equals(null).ShouldBeFalse();
		ts.Equals("not a timestamp").ShouldBeFalse();
	}

	[Fact]
	public void SupportGetHashCode()
	{
		var ts1 = new CacheAlignedTimestamp();
		var ts2 = new CacheAlignedTimestamp();

		ts1.GetHashCode().ShouldBe(ts2.GetHashCode());
	}
}
