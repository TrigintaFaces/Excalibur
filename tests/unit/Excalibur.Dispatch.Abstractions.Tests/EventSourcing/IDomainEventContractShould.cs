namespace Excalibur.Dispatch.Tests.EventSourcing;

/// <summary>
/// Contract tests for the <see cref="IDomainEvent"/> messaging interface, and the structural
/// guard (K3) that keeps a stream version OFF the messaging contract.
/// <para>
/// The interface defines the members a message genuinely carries (id, timestamp, type, metadata).
/// It deliberately does NOT carry <c>AggregateId</c> or <c>Version</c>: an aggregate stream position
/// is a persistence concern owned by the persistence envelope (<c>StoredEvent.Version</c> →
/// <c>HistoricEvent</c>), not a messaging concern (dispatch-vs-excalibur boundary). Re-adding either
/// member to the contract re-opens the boundary leak and the "unstampable event" failure mode — so
/// the absence is asserted here as a regression lock, RED the moment a member is re-introduced.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Abstractions")]
public sealed class IDomainEventContractShould
{
	[Fact]
	public void Have_EventId_Property()
	{
		// Arrange
		var interfaceType = typeof(IDomainEvent);

		// Act
		var property = interfaceType.GetProperty(nameof(IDomainEvent.EventId));

		// Assert
		_ = property.ShouldNotBeNull("IDomainEvent must define EventId property");
		property.PropertyType.ShouldBe(typeof(string), "EventId must be of type string");
		property.CanRead.ShouldBeTrue("EventId must be readable");
	}

	// ── K3 (fvq5qf) structural regression guard: a stream version/aggregate id must NOT live on
	//    the messaging contract. RED the moment either is re-added to IDomainEvent or DomainEvent. ──

	[Fact]
	public void NotDefine_AggregateId_OnTheMessagingContract()
	{
		// A stream's aggregate identity is known to the repository from the stream it loads; it is
		// not a messaging concern. Re-adding it re-opens the dispatch-vs-excalibur boundary leak.
		typeof(IDomainEvent).GetProperty("AggregateId")
			.ShouldBeNull("IDomainEvent must NOT carry AggregateId (persistence concern, not messaging)");
		typeof(DomainEvent).GetProperty("AggregateId")
			.ShouldBeNull("DomainEvent must NOT carry AggregateId (removed as a boundary leak)");
	}

	[Fact]
	public void NotDefine_Version_OnTheMessagingContract()
	{
		// The authoritative aggregate stream version lives in the persistence envelope
		// (StoredEvent.Version -> HistoricEvent), never on the event payload. Re-adding a payload
		// Version re-introduces the "event stamped with a version nothing on the read path reads"
		// failure mode the removal made inexpressible.
		typeof(IDomainEvent).GetProperty("Version")
			.ShouldBeNull("IDomainEvent must NOT carry Version (lives in the persistence envelope)");
		typeof(DomainEvent).GetProperty("Version")
			.ShouldBeNull("DomainEvent must NOT carry Version (removed as a boundary leak)");
	}

	[Fact]
	public void Have_OccurredAt_Property()
	{
		// Arrange
		var interfaceType = typeof(IDomainEvent);

		// Act
		var property = interfaceType.GetProperty(nameof(IDomainEvent.OccurredAt));

		// Assert
		_ = property.ShouldNotBeNull("IDomainEvent must define OccurredAt property");
		property.PropertyType.ShouldBe(typeof(DateTimeOffset), "OccurredAt must be of type DateTimeOffset");
		property.CanRead.ShouldBeTrue("OccurredAt must be readable");
	}

	[Fact]
	public void NotDefine_EventType_OnTheMessagingContract()
	{
		// Identity is DECLARED on the type ([MessageName]), never carried per-instance. A payload
		// EventType let one type's instances disagree about what they are, and defaulting it to
		// GetType().Name silently rebound stored data to the class name -- so renaming a class
		// orphaned every event it had already written. Re-adding either re-opens both.
		typeof(IDomainEvent).GetProperty("EventType")
			.ShouldBeNull("IDomainEvent must NOT carry EventType (identity is declared via [MessageName])");
		typeof(DomainEvent).GetProperty("EventType")
			.ShouldBeNull("DomainEvent must NOT carry EventType (no GetType().Name fallback)");
	}

	[Fact]
	public void Have_Metadata_Property()
	{
		// Arrange
		var interfaceType = typeof(IDomainEvent);

		// Act
		var property = interfaceType.GetProperty(nameof(IDomainEvent.Metadata));

		// Assert
		_ = property.ShouldNotBeNull("IDomainEvent must define Metadata property");
		property.PropertyType.ShouldBe(typeof(IDictionary<string, object>), "Metadata must be of type IDictionary<string, object>");
		property.CanRead.ShouldBeTrue("Metadata must be readable");
	}
}
