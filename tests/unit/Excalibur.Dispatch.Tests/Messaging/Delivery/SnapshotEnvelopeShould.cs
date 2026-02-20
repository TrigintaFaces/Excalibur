using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SnapshotEnvelopeShould
{
	[Fact]
	public void CreateWithStringKey()
	{
		var envelope = new SnapshotEnvelope<string>
		{
			AggregateId = "agg-1",
			ApplicationState = "{\"balance\":100}",
			SnapshotMetadata = "{\"version\":5}",
		};

		envelope.AggregateId.ShouldBe("agg-1");
		envelope.ApplicationState.ShouldBe("{\"balance\":100}");
		envelope.SnapshotMetadata.ShouldBe("{\"version\":5}");
	}

	[Fact]
	public void CreateWithGuidKey()
	{
		var guid = Guid.NewGuid();
		var envelope = new SnapshotEnvelope<Guid>
		{
			AggregateId = guid,
			ApplicationState = "{}",
			SnapshotMetadata = "{}",
		};

		envelope.AggregateId.ShouldBe(guid);
	}

	[Fact]
	public void CreateWithIntegerKey()
	{
		var envelope = new SnapshotEnvelope<int>
		{
			AggregateId = 42,
			ApplicationState = "{\"count\":10}",
			SnapshotMetadata = "{\"version\":1}",
		};

		envelope.AggregateId.ShouldBe(42);
	}
}
