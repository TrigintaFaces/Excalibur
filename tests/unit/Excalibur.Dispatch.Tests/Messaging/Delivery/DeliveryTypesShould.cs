using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DeliveryTypesShould
{
	// --- MessageFlags ---

	[Fact]
	public void MessageFlags_HaveCorrectValues()
	{
		((byte)MessageFlags.None).ShouldBe((byte)0);
		((byte)MessageFlags.Compressed).ShouldBe((byte)1);
		((byte)MessageFlags.Encrypted).ShouldBe((byte)2);
		((byte)MessageFlags.Persistent).ShouldBe((byte)4);
		((byte)MessageFlags.HighPriority).ShouldBe((byte)8);
		((byte)MessageFlags.Validated).ShouldBe((byte)16);
	}

	[Fact]
	public void MessageFlags_SupportCombination()
	{
		var flags = MessageFlags.Compressed | MessageFlags.Encrypted;

		flags.HasFlag(MessageFlags.Compressed).ShouldBeTrue();
		flags.HasFlag(MessageFlags.Encrypted).ShouldBeTrue();
		flags.HasFlag(MessageFlags.Persistent).ShouldBeFalse();
	}

	// --- MessageVersionMetadata ---

	[Fact]
	public void MessageVersionMetadata_DefaultsToZero()
	{
		var metadata = new MessageVersionMetadata();

		metadata.SchemaVersion.ShouldBe(0);
		metadata.SerializerVersion.ShouldBe(0);
		metadata.Version.ShouldBe(0);
	}

	[Fact]
	public void MessageVersionMetadata_SetAllProperties()
	{
		var metadata = new MessageVersionMetadata
		{
			SchemaVersion = 3,
			SerializerVersion = 2,
			Version = 5,
		};

		metadata.SchemaVersion.ShouldBe(3);
		metadata.SerializerVersion.ShouldBe(2);
		metadata.Version.ShouldBe(5);
	}

	// --- MessageEnvelopePoolStats ---

	[Fact]
	public void MessageEnvelopePoolStats_DefaultValues()
	{
		var stats = new MessageEnvelopePoolStats();

		stats.TotalRentals.ShouldBe(0);
		stats.TotalReturns.ShouldBe(0);
		stats.PoolHits.ShouldBe(0);
		stats.PoolMisses.ShouldBe(0);
		stats.HitRate.ShouldBe(0);
		stats.ThreadLocalStats.ShouldNotBeNull();
		stats.ThreadLocalStats.ShouldBeEmpty();
	}

	[Fact]
	public void MessageEnvelopePoolStats_SetAllProperties()
	{
		var stats = new MessageEnvelopePoolStats
		{
			TotalRentals = 1000,
			TotalReturns = 950,
			PoolHits = 800,
			PoolMisses = 200,
			HitRate = 0.80,
		};

		stats.TotalRentals.ShouldBe(1000);
		stats.TotalReturns.ShouldBe(950);
		stats.PoolHits.ShouldBe(800);
		stats.PoolMisses.ShouldBe(200);
		stats.HitRate.ShouldBe(0.80);
	}
}
