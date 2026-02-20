using Excalibur.Dispatch.Metrics;

namespace Excalibur.Dispatch.Tests.Messaging.Metrics;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CounterSnapshotShould
{
	[Fact]
	public void DefaultToNullsAndZeros()
	{
		var snapshot = new CounterSnapshot();

		snapshot.Name.ShouldBeNull();
		snapshot.Value.ShouldBe(0);
		snapshot.Unit.ShouldBeNull();
		snapshot.Timestamp.ShouldBe(default);
	}

	[Fact]
	public void AllowSettingProperties()
	{
		var now = DateTimeOffset.UtcNow;
		var snapshot = new CounterSnapshot
		{
			Name = "requests",
			Value = 42,
			Unit = "count",
			Timestamp = now,
		};

		snapshot.Name.ShouldBe("requests");
		snapshot.Value.ShouldBe(42);
		snapshot.Unit.ShouldBe("count");
		snapshot.Timestamp.ShouldBe(now);
	}

	[Fact]
	public void SupportEquality()
	{
		var now = DateTimeOffset.UtcNow;
		var s1 = new CounterSnapshot { Name = "a", Value = 1, Unit = "b", Timestamp = now };
		var s2 = new CounterSnapshot { Name = "a", Value = 1, Unit = "b", Timestamp = now };
		var s3 = new CounterSnapshot { Name = "x", Value = 1, Unit = "b", Timestamp = now };

		s1.Equals(s2).ShouldBeTrue();
		s1.Equals(s3).ShouldBeFalse();
		(s1 == s2).ShouldBeTrue();
		(s1 != s3).ShouldBeTrue();
	}

	[Fact]
	public void SupportEqualsWithObject()
	{
		var s = new CounterSnapshot { Name = "a", Value = 1 };

		s.Equals((object)new CounterSnapshot { Name = "a", Value = 1 }).ShouldBeTrue();
		s.Equals(null).ShouldBeFalse();
		s.Equals("not a snapshot").ShouldBeFalse();
	}

	[Fact]
	public void SupportGetHashCode()
	{
		var now = DateTimeOffset.UtcNow;
		var s1 = new CounterSnapshot { Name = "a", Value = 1, Unit = "b", Timestamp = now };
		var s2 = new CounterSnapshot { Name = "a", Value = 1, Unit = "b", Timestamp = now };

		s1.GetHashCode().ShouldBe(s2.GetHashCode());
	}
}
