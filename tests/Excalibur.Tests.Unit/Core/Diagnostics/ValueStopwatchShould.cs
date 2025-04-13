using System.Diagnostics;
using System.Reflection;

using Excalibur.Core.Diagnostics;

using Shouldly;

namespace Excalibur.Tests.Unit.Core.Diagnostics;

public class ValueStopwatchShould
{
	[Fact]
	public void StartNewShouldInitializeStopwatch()
	{
		// Arrange & Act
		var stopwatch = ValueStopwatch.StartNew();

		// Assert
		stopwatch.ShouldNotBe(default(ValueStopwatch));
	}

	[Fact]
	public void StartNewInstance()
	{
		var stopwatch = ValueStopwatch.StartNew();

		stopwatch.Elapsed.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
	}

	[Fact]
	public void CalculateElapsedTime()
	{
		var stopwatch = ValueStopwatch.StartNew();
		Thread.Sleep(50);
		stopwatch.Elapsed.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public void HaveEqualityWorkCorrectly()
	{
		// Arrange
		var stopwatch1 = ValueStopwatch.StartNew();
		Thread.Sleep(1); // Ensure different timestamps
		var stopwatch2 = ValueStopwatch.StartNew();

		// Act & Assert
		(stopwatch1 == stopwatch2).ShouldBeFalse();
		(stopwatch1 != stopwatch2).ShouldBeTrue();
	}

	[Fact]
	public void StartNewShouldReturnDifferentInstances()
	{
		// Arrange & Act
		var stopwatch1 = ValueStopwatch.StartNew();
		Thread.Sleep(1); // Ensure different timestamps
		var stopwatch2 = ValueStopwatch.StartNew();

		// Assert
		stopwatch1.ShouldNotBe(stopwatch2);
	}

	[Fact]
	public void ShouldReturnZeroForElapsedImmediatelyAfterStart()
	{
		// Arrange
		var stopwatch = ValueStopwatch.StartNew();

		// Act & Assert
		stopwatch.Elapsed.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
	}

	[Fact]
	public void ShouldConsiderTwoInstancesEqualIfTheyHaveSameStartTimestamp()
	{
		// Arrange
		var timestamp = Stopwatch.GetTimestamp();
		var stopwatch1 = (ValueStopwatch)typeof(ValueStopwatch).GetConstructor(
			BindingFlags.NonPublic | BindingFlags.Instance,
			null,
			[typeof(long)],
			null)!.Invoke([timestamp]);
		var stopwatch2 = (ValueStopwatch)typeof(ValueStopwatch).GetConstructor(
			BindingFlags.NonPublic | BindingFlags.Instance,
			null,
			[typeof(long)],
			null)!.Invoke([timestamp]);

		// Act & Assert
		stopwatch1.ShouldBe(stopwatch2);
	}

	[Fact]
	public void ShouldNotConsiderTwoInstancesEqualIfTheyHaveDifferentStartTimestamps()
	{
		// Arrange
		var stopwatch1 = ValueStopwatch.StartNew();
		Thread.Sleep(1); // Ensure a different timestamp
		var stopwatch2 = ValueStopwatch.StartNew();

		// Act & Assert
		stopwatch1.ShouldNotBe(stopwatch2);
	}

	[Fact]
	public void GetHashCodeShouldReturnSameValueForEqualInstances()
	{
		// Arrange
		var timestamp = Stopwatch.GetTimestamp();
		var stopwatch1 = (ValueStopwatch)typeof(ValueStopwatch).GetConstructor(
			BindingFlags.NonPublic | BindingFlags.Instance,
			null,
			[typeof(long)],
			null)!.Invoke([timestamp]);
		var stopwatch2 = (ValueStopwatch)typeof(ValueStopwatch).GetConstructor(
			BindingFlags.NonPublic | BindingFlags.Instance,
			null,
			[typeof(long)],
			null)!.Invoke([timestamp]);

		// Act & Assert
		stopwatch1.GetHashCode().ShouldBe(stopwatch2.GetHashCode());
	}

	[Fact]
	public void StartNewShouldInitializeStartTimestamp()
	{
		var stopwatch = ValueStopwatch.StartNew();

		stopwatch.Elapsed.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public void ElapsedShouldIncreaseOverTime()
	{
		var stopwatch = ValueStopwatch.StartNew();

		var first = stopwatch.Elapsed;
		Thread.Sleep(10);
		var second = stopwatch.Elapsed;

		second.ShouldBeGreaterThan(first);
	}

	[Fact]
	public void ElapsedThrowsIfNotStarted()
	{
		var stopwatch = default(ValueStopwatch);

		var exception = Should.Throw<InvalidOperationException>(() =>
		{
			var _ = stopwatch.Elapsed;
		});

		exception.Message.ShouldBe("ValueStopwatch was not started.");
	}

	[Fact]
	public void EqualInstancesShouldBeEqual()
	{
		var timestamp = Stopwatch.GetTimestamp();
		var a = CreateStopwatchWithTimestamp(timestamp);
		var b = CreateStopwatchWithTimestamp(timestamp);

		a.ShouldBe(b);
		(a == b).ShouldBeTrue();
		(a != b).ShouldBeFalse();
	}

	[Fact]
	public void DifferentInstancesShouldNotBeEqual()
	{
		var a = ValueStopwatch.StartNew();
		Thread.Sleep(1);
		var b = ValueStopwatch.StartNew();

		a.ShouldNotBe(b);
		(a == b).ShouldBeFalse();
		(a != b).ShouldBeTrue();
	}

	[Fact]
	public void EqualsObjectShouldReturnTrueForSameValue()
	{
		var timestamp = Stopwatch.GetTimestamp();
		var a = CreateStopwatchWithTimestamp(timestamp);
		var b = (object)CreateStopwatchWithTimestamp(timestamp);

		a.Equals(b).ShouldBeTrue();
	}

	[Fact]
	public void EqualsObjectShouldReturnFalseForDifferentType()
	{
		var a = ValueStopwatch.StartNew();
		a.Equals("not-a-stopwatch").ShouldBeFalse();
	}

	[Fact]
	public void GetHashCodeShouldBeBasedOnStartTimestamp()
	{
		var timestamp = Stopwatch.GetTimestamp();
		var a = CreateStopwatchWithTimestamp(timestamp);
		var b = CreateStopwatchWithTimestamp(timestamp);

		a.GetHashCode().ShouldBe(b.GetHashCode());
	}

	private static ValueStopwatch CreateStopwatchWithTimestamp(long timestamp)
	{
		var ctor = typeof(ValueStopwatch).GetConstructor(
			BindingFlags.NonPublic | BindingFlags.Instance,
			binder: null,
			types: [typeof(long)],
			modifiers: null
		);

		return (ValueStopwatch)ctor!.Invoke([timestamp]);
	}
}
