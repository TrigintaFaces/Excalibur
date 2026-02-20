using Excalibur.Dispatch.Channels;
using Excalibur.Dispatch.Channels.Diagnostics;

namespace Excalibur.Dispatch.Tests.Messaging.Channels;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ChannelTypesShould
{
	// --- ChannelMode ---

	[Fact]
	public void ChannelMode_HaveCorrectValues()
	{
		((int)ChannelMode.Unbounded).ShouldBe(0);
		((int)ChannelMode.Bounded).ShouldBe(1);
	}

	// --- ChannelMessagePumpStatus ---

	[Fact]
	public void ChannelMessagePumpStatus_HaveCorrectValues()
	{
		((int)ChannelMessagePumpStatus.NotStarted).ShouldBe(0);
		((int)ChannelMessagePumpStatus.Starting).ShouldBe(1);
		((int)ChannelMessagePumpStatus.Running).ShouldBe(2);
		((int)ChannelMessagePumpStatus.Stopping).ShouldBe(3);
		((int)ChannelMessagePumpStatus.Stopped).ShouldBe(4);
		((int)ChannelMessagePumpStatus.Faulted).ShouldBe(5);
	}

	// --- Batch<T> ---

	[Fact]
	public void Batch_CreateWithItems()
	{
		var items = new List<string> { "a", "b", "c" };
		var batch = new Batch<string>(items);

		batch.Items.ShouldBe(items);
		batch.Count.ShouldBe(3);
		batch.Timestamp.ShouldNotBe(default);
	}

	[Fact]
	public void Batch_ThrowOnNullItems()
	{
		Should.Throw<ArgumentNullException>(() => new Batch<string>(null!));
	}

	[Fact]
	public void Batch_SameInstanceEquals()
	{
		var items = new List<int> { 1, 2, 3 };
		var b1 = new Batch<int>(items);
		// Same reference and same timestamp => equal only if created at same time
		// Since we can't control timestamp, verify self-equality
		b1.Equals(b1).ShouldBeTrue();
	}

	[Fact]
	public void Batch_DifferentItemsNotEqual()
	{
		var b1 = new Batch<int>(new List<int> { 1 });
		var b2 = new Batch<int>(new List<int> { 2 });

		b1.Equals(b2).ShouldBeFalse();
		(b1 != b2).ShouldBeTrue();
	}

	[Fact]
	public void Batch_EqualsObject()
	{
		var items = new List<int> { 1 };
		var batch = new Batch<int>(items);

		batch.Equals((object)batch).ShouldBeTrue();
		batch.Equals("not a batch").ShouldBeFalse();
	}

	[Fact]
	public void Batch_GetHashCode_ConsistentForSameInstance()
	{
		var items = new List<int> { 1 };
		var batch = new Batch<int>(items);

		batch.GetHashCode().ShouldBe(batch.GetHashCode());
	}

	// --- BatchReadResult<T> ---

	[Fact]
	public void BatchReadResult_CreateWithItems()
	{
		var items = new List<string> { "x", "y" };
		var result = new BatchReadResult<string>(items, true);

		result.Items.ShouldBe(items);
		result.HasItems.ShouldBeTrue();
		result.Count.ShouldBe(2);
	}

	[Fact]
	public void BatchReadResult_CreateEmpty()
	{
		var result = new BatchReadResult<int>([], false);

		result.HasItems.ShouldBeFalse();
		result.Count.ShouldBe(0);
	}

	[Fact]
	public void BatchReadResult_Equality()
	{
		var items = new List<int> { 1, 2 };
		var r1 = new BatchReadResult<int>(items, true);
		var r2 = new BatchReadResult<int>(items, true);

		r1.Equals(r2).ShouldBeTrue();
		(r1 == r2).ShouldBeTrue();
	}

	[Fact]
	public void BatchReadResult_Inequality_DifferentHasItems()
	{
		var items = new List<int> { 1 };
		var r1 = new BatchReadResult<int>(items, true);
		var r2 = new BatchReadResult<int>(items, false);

		r1.Equals(r2).ShouldBeFalse();
		(r1 != r2).ShouldBeTrue();
	}

	[Fact]
	public void BatchReadResult_EqualsObject()
	{
		var items = new List<int> { 1 };
		var result = new BatchReadResult<int>(items, true);

		result.Equals((object)result).ShouldBeTrue();
		result.Equals("not a result").ShouldBeFalse();
	}

	[Fact]
	public void BatchReadResult_GetHashCode_ConsistentForEqual()
	{
		var items = new List<int> { 1 };
		var r1 = new BatchReadResult<int>(items, true);
		var r2 = new BatchReadResult<int>(items, true);

		r1.GetHashCode().ShouldBe(r2.GetHashCode());
	}

	// --- ChannelMetrics ---

	[Fact]
	public void ChannelMetrics_SetProperties()
	{
		var metrics = new ChannelMetrics
		{
			MessagesPerSecond = 1500.5,
			AverageLatencyMs = 2.5,
			P99LatencyMs = 10.0,
		};

		metrics.MessagesPerSecond.ShouldBe(1500.5);
		metrics.AverageLatencyMs.ShouldBe(2.5);
		metrics.P99LatencyMs.ShouldBe(10.0);
	}

	[Fact]
	public void ChannelMetrics_DefaultsToZero()
	{
		var metrics = new ChannelMetrics();

		metrics.MessagesPerSecond.ShouldBe(0);
		metrics.AverageLatencyMs.ShouldBe(0);
		metrics.P99LatencyMs.ShouldBe(0);
	}
}
