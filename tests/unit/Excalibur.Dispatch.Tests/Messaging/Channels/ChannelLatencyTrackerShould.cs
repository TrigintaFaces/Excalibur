using Excalibur.Dispatch.Channels.Diagnostics;

namespace Excalibur.Dispatch.Tests.Messaging.Channels;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ChannelLatencyTrackerShould
{
	[Fact]
	public void ThrowOnNullChannelId()
	{
		Should.Throw<ArgumentNullException>(() => new ChannelLatencyTracker(null!));
	}

	[Fact]
	public void ReturnZeroStatisticsWhenEmpty()
	{
		var tracker = new ChannelLatencyTracker("test-channel");

		var (avg, p95, p99) = tracker.GetStatistics();

		avg.ShouldBe(0);
		p95.ShouldBe(0);
		p99.ShouldBe(0);
	}

	[Fact]
	public void RecordAndRetrieveLatency()
	{
		var tracker = new ChannelLatencyTracker("test-channel");

		tracker.RecordLatency(100);
		tracker.RecordLatency(200);
		tracker.RecordLatency(300);

		var (avg, p95, p99) = tracker.GetStatistics();

		avg.ShouldBe(200);
		p95.ShouldBeGreaterThan(0);
		p99.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void HandleManySamples()
	{
		var tracker = new ChannelLatencyTracker("test-channel", sampleSize: 10);

		for (var i = 1; i <= 20; i++)
		{
			tracker.RecordLatency(i * 10);
		}

		var (avg, _, _) = tracker.GetStatistics();

		// After wrapping, the ring buffer contains samples 11-20 (values 110-200)
		avg.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void CreateWithCustomSampleSize()
	{
		var tracker = new ChannelLatencyTracker("test-channel", sampleSize: 50);

		for (var i = 0; i < 50; i++)
		{
			tracker.RecordLatency(i);
		}

		var (avg, _, _) = tracker.GetStatistics();
		avg.ShouldBeGreaterThan(0);
	}
}
