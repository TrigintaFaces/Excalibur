using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventStoreMessageShould
{
	[Fact]
	public void CreateWithRequiredProperties()
	{
		var now = DateTimeOffset.UtcNow;
		var msg = new EventStoreMessage<string>
		{
			AggregateId = "agg-1",
			OccurredOn = now,
			EventId = "evt-1",
			EventType = "OrderCreated",
			EventBody = "{\"id\":1}",
			EventMetadata = "{\"version\":1}",
		};

		msg.AggregateId.ShouldBe("agg-1");
		msg.OccurredOn.ShouldBe(now);
		msg.EventId.ShouldBe("evt-1");
		msg.EventType.ShouldBe("OrderCreated");
		msg.EventBody.ShouldBe("{\"id\":1}");
		msg.EventMetadata.ShouldBe("{\"version\":1}");
	}

	[Fact]
	public void HaveDefaultOptionalProperties()
	{
		var msg = new EventStoreMessage<string>
		{
			AggregateId = "agg-1",
			OccurredOn = DateTimeOffset.UtcNow,
			EventId = "evt-1",
			EventType = "test",
			EventBody = "{}",
			EventMetadata = "{}",
		};

		msg.Attempts.ShouldBe(0);
		msg.DispatcherId.ShouldBeNull();
		msg.DispatchedOn.ShouldBeNull();
		msg.DispatcherTimeout.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingOptionalProperties()
	{
		var dispatchedOn = DateTimeOffset.UtcNow;
		var timeout = DateTimeOffset.UtcNow.AddMinutes(5);
		var msg = new EventStoreMessage<string>
		{
			AggregateId = "agg-1",
			OccurredOn = DateTimeOffset.UtcNow,
			EventId = "evt-1",
			EventType = "test",
			EventBody = "{}",
			EventMetadata = "{}",
			Attempts = 3,
			DispatcherId = "dispatcher-1",
			DispatchedOn = dispatchedOn,
			DispatcherTimeout = timeout,
		};

		msg.Attempts.ShouldBe(3);
		msg.DispatcherId.ShouldBe("dispatcher-1");
		msg.DispatchedOn.ShouldBe(dispatchedOn);
		msg.DispatcherTimeout.ShouldBe(timeout);
	}

	[Fact]
	public void SupportIntegerAggregateKey()
	{
		var msg = new EventStoreMessage<int>
		{
			AggregateId = 42,
			OccurredOn = DateTimeOffset.UtcNow,
			EventId = "evt-1",
			EventType = "test",
			EventBody = "{}",
			EventMetadata = "{}",
		};

		msg.AggregateId.ShouldBe(42);
	}

	[Fact]
	public void SupportGuidAggregateKey()
	{
		var guid = Guid.NewGuid();
		var msg = new EventStoreMessage<Guid>
		{
			AggregateId = guid,
			OccurredOn = DateTimeOffset.UtcNow,
			EventId = "evt-1",
			EventType = "test",
			EventBody = "{}",
			EventMetadata = "{}",
		};

		msg.AggregateId.ShouldBe(guid);
	}

	[Fact]
	public void FromEventStoreMessage_ThrowNotSupported()
	{
		var source = new EventStoreMessage<string>
		{
			AggregateId = "agg-1",
			OccurredOn = DateTimeOffset.UtcNow,
			EventId = "evt-1",
			EventType = "test",
			EventBody = "{}",
			EventMetadata = "{}",
		};

		Should.Throw<NotSupportedException>(() =>
			EventStoreMessage<int>.FromEventStoreMessage(source));
	}
}
