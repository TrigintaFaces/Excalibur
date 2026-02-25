using Excalibur.A3.Events;
using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Tests.A3.Events;

[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class DomainEventBaseShould
{
	[Fact]
	public void Initialize_with_unique_message_id()
	{
		// Act
		var event1 = new TestDomainEvent();
		var event2 = new TestDomainEvent();

		// Assert
		event1.MessageId.ShouldNotBeNullOrWhiteSpace();
		event2.MessageId.ShouldNotBeNullOrWhiteSpace();
		event1.MessageId.ShouldNotBe(event2.MessageId);
	}

	[Fact]
	public void Initialize_with_unique_id()
	{
		// Act
		var event1 = new TestDomainEvent();
		var event2 = new TestDomainEvent();

		// Assert
		event1.Id.ShouldNotBe(Guid.Empty);
		event2.Id.ShouldNotBe(Guid.Empty);
		event1.Id.ShouldNotBe(event2.Id);
	}

	[Fact]
	public void Initialize_with_timestamp_close_to_now()
	{
		// Act
		var domainEvent = new TestDomainEvent();

		// Assert
		domainEvent.Timestamp.ShouldBeInRange(
			DateTimeOffset.UtcNow.AddSeconds(-5),
			DateTimeOffset.UtcNow.AddSeconds(5));
	}

	[Fact]
	public void Initialize_with_empty_headers()
	{
		// Act
		var domainEvent = new TestDomainEvent();

		// Assert
		domainEvent.Headers.ShouldNotBeNull();
		domainEvent.Headers.Count.ShouldBe(0);
	}

	[Fact]
	public void Initialize_with_default_message_features()
	{
		// Act
		var domainEvent = new TestDomainEvent();

		// Assert
		domainEvent.Features.ShouldNotBeNull();
		domainEvent.Features.ShouldBeOfType<DefaultMessageFeatures>();
	}

	[Fact]
	public void Have_event_kind()
	{
		// Act
		var domainEvent = new TestDomainEvent();

		// Assert
		domainEvent.Kind.ShouldBe(MessageKinds.Event);
	}

	[Fact]
	public void Return_self_as_body()
	{
		// Act
		var domainEvent = new TestDomainEvent();

		// Assert
		domainEvent.Body.ShouldBeSameAs(domainEvent);
	}

	[Fact]
	public void Return_type_name_as_message_type()
	{
		// Act
		var domainEvent = new TestDomainEvent();

		// Assert
		domainEvent.MessageType.ShouldBe(nameof(TestDomainEvent));
	}

	[Fact]
	public void Return_message_id_as_event_id()
	{
		// Act
		var domainEvent = new TestDomainEvent();

		// Assert
		domainEvent.EventId.ShouldBe(domainEvent.MessageId);
	}

	[Fact]
	public void Have_empty_aggregate_id_by_default()
	{
		// Act
		var domainEvent = new TestDomainEvent();

		// Assert
		domainEvent.AggregateId.ShouldBe(string.Empty);
	}

	[Fact]
	public void Have_zero_version_by_default()
	{
		// Act
		var domainEvent = new TestDomainEvent();

		// Assert
		domainEvent.Version.ShouldBe(0);
	}

	[Fact]
	public void Return_timestamp_as_occurred_at()
	{
		// Act
		var domainEvent = new TestDomainEvent();

		// Assert
		domainEvent.OccurredAt.ShouldBe(domainEvent.Timestamp);
	}

	[Fact]
	public void Return_message_type_as_event_type()
	{
		// Act
		var domainEvent = new TestDomainEvent();

		// Assert
		domainEvent.EventType.ShouldBe(domainEvent.MessageType);
	}

	[Fact]
	public void Return_headers_as_metadata()
	{
		// Act
		var domainEvent = new TestDomainEvent();

		// Assert
		domainEvent.Metadata.ShouldNotBeNull();
	}

	[Fact]
	public void Add_correlation_id_header_when_not_empty()
	{
		// Arrange
		var correlationId = Guid.NewGuid();

		// Act
		var domainEvent = new TestDomainEventWithCorrelation(correlationId);

		// Assert
		domainEvent.Headers.ShouldContainKey("CorrelationId");
		domainEvent.Headers["CorrelationId"].ShouldBe(correlationId.ToString());
	}

	[Fact]
	public void Not_add_correlation_id_header_when_empty()
	{
		// Act
		var domainEvent = new TestDomainEventWithCorrelation(Guid.Empty);

		// Assert
		domainEvent.Headers.ShouldNotContainKey("CorrelationId");
	}

	[Fact]
	public void Implement_IDomainEvent()
	{
		// Act
		var domainEvent = new TestDomainEvent();

		// Assert
		domainEvent.ShouldBeAssignableTo<IDomainEvent>();
	}

	private sealed class TestDomainEvent : DomainEventBase
	{
	}

	private sealed class TestDomainEventWithCorrelation : DomainEventBase
	{
		public TestDomainEventWithCorrelation(Guid correlationId) : base(correlationId)
		{
		}
	}
}
