// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Metrics;

using Excalibur.EventSourcing.Observability;

using Xunit.Abstractions;

namespace Excalibur.Tests.Observability;

/// <summary>
/// Telemetry parity tests per ADR-078 boundary requirements.
/// </summary>
/// <remarks>
/// <para>
/// These tests verify that activity sources are properly separated by ownership:
/// <list type="bullet">
/// <item>Dispatch owns messaging observability ("Excalibur.Dispatch")</item>
/// <item>Excalibur owns event sourcing observability ("Excalibur.EventSourcing")</item>
/// </list>
/// </para>
/// <para>
/// Sprint 127 task xssis: Ensure no overlap in activity sources between Dispatch and Excalibur.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class TelemetryParityShould
{
	private readonly ITestOutputHelper _output;

	public TelemetryParityShould(ITestOutputHelper output)
	{
		_output = output;
	}

	#region Activity Source Name Verification

	/// <summary>
	/// Verifies that Dispatch activity source has correct name per ADR-078.
	/// </summary>
	[Fact]
	public void DispatchActivitySource_ShouldHaveCorrectName()
	{
		// Assert
		DispatchActivitySource.Name.ShouldBe("Excalibur.Dispatch");
		_output.WriteLine($"DispatchActivitySource.Name = \"{DispatchActivitySource.Name}\"");
	}

	/// <summary>
	/// Verifies that EventSourcing activity source has correct name per ADR-078.
	/// </summary>
	[Fact]
	public void EventSourcingActivitySource_ShouldHaveCorrectName()
	{
		// Assert
		EventSourcingActivitySource.Name.ShouldBe("Excalibur.EventSourcing");
		_output.WriteLine($"EventSourcingActivitySource.Name = \"{EventSourcingActivitySource.Name}\"");
	}

	/// <summary>
	/// Verifies that Dispatch and EventSourcing activity sources have distinct names.
	/// </summary>
	[Fact]
	public void ActivitySources_ShouldHaveDistinctNames()
	{
		// Assert - Names must not overlap
		DispatchActivitySource.Name.ShouldNotBe(EventSourcingActivitySource.Name);

		// Assert - Neither should start with the other's full prefix (no namespace collision)
		EventSourcingActivitySource.Name.ShouldNotStartWith("Excalibur.Dispatch");
		DispatchActivitySource.Name.ShouldNotStartWith("Excalibur.EventSourcing");

		_output.WriteLine($"Dispatch: \"{DispatchActivitySource.Name}\"");
		_output.WriteLine($"EventSourcing: \"{EventSourcingActivitySource.Name}\"");
		_output.WriteLine("Names are distinct - no overlap");
	}

	#endregion Activity Source Name Verification

	#region Activity Source Instance Verification

	/// <summary>
	/// Verifies that Dispatch activity source instance is initialized.
	/// </summary>
	[Fact]
	public void DispatchActivitySource_InstanceShouldBeInitialized()
	{
		// Assert
		_ = DispatchActivitySource.Instance.ShouldNotBeNull();
		DispatchActivitySource.Instance.Name.ShouldBe("Excalibur.Dispatch");

		_output.WriteLine("DispatchActivitySource.Instance is initialized correctly");
	}

	/// <summary>
	/// Verifies that EventSourcing activity source instance is initialized.
	/// </summary>
	[Fact]
	public void EventSourcingActivitySource_InstanceShouldBeInitialized()
	{
		// Assert
		_ = EventSourcingActivitySource.Instance.ShouldNotBeNull();
		EventSourcingActivitySource.Instance.Name.ShouldBe("Excalibur.EventSourcing");

		_output.WriteLine("EventSourcingActivitySource.Instance is initialized correctly");
	}

	/// <summary>
	/// Verifies that Dispatch and EventSourcing have different activity source instances.
	/// </summary>
	[Fact]
	public void ActivitySources_ShouldBeDifferentInstances()
	{
		// Assert - Different instances
		ReferenceEquals(DispatchActivitySource.Instance, EventSourcingActivitySource.Instance).ShouldBeFalse();

		_output.WriteLine("Activity source instances are distinct");
	}

	#endregion Activity Source Instance Verification

	#region EventSourcing Constants Verification

	/// <summary>
	/// Verifies that EventSourcing activity source constants follow Excalibur naming.
	/// </summary>
	[Fact]
	public void EventSourcingActivitySources_ShouldFollowExcaliburNaming()
	{
		// Assert - All constants should start with "Excalibur.EventSourcing"
		EventSourcingActivitySources.EventStore.ShouldStartWith("Excalibur.EventSourcing");
		EventSourcingActivitySources.SnapshotStore.ShouldStartWith("Excalibur.EventSourcing");

		_output.WriteLine($"EventStore source: \"{EventSourcingActivitySources.EventStore}\"");
		_output.WriteLine($"SnapshotStore source: \"{EventSourcingActivitySources.SnapshotStore}\"");
	}

	/// <summary>
	/// Verifies that EventSourcing meter constants follow Excalibur naming.
	/// </summary>
	[Fact]
	public void EventSourcingMeters_ShouldFollowExcaliburNaming()
	{
		// Assert - All meters should start with "Excalibur.EventSourcing"
		EventSourcingMeters.EventStore.ShouldStartWith("Excalibur.EventSourcing");
		EventSourcingMeters.SnapshotStore.ShouldStartWith("Excalibur.EventSourcing");

		_output.WriteLine($"EventStore meter: \"{EventSourcingMeters.EventStore}\"");
		_output.WriteLine($"SnapshotStore meter: \"{EventSourcingMeters.SnapshotStore}\"");
	}

	/// <summary>
	/// Verifies that EventSourcing activity names follow domain conventions.
	/// </summary>
	[Fact]
	public void EventSourcingActivities_ShouldFollowDomainConventions()
	{
		// Assert - Activity names should be descriptive
		EventSourcingActivities.Append.ShouldBe("EventStore.Append");
		EventSourcingActivities.Load.ShouldBe("EventStore.Load");
		EventSourcingActivities.GetUndispatched.ShouldBe("EventStore.GetUndispatched");
		EventSourcingActivities.MarkDispatched.ShouldBe("EventStore.MarkDispatched");
		EventSourcingActivities.SaveSnapshot.ShouldBe("SnapshotStore.Save");
		EventSourcingActivities.GetSnapshot.ShouldBe("SnapshotStore.Get");
		EventSourcingActivities.DeleteSnapshots.ShouldBe("SnapshotStore.Delete");

		_output.WriteLine("EventSourcing activity names verified");
	}

	/// <summary>
	/// Verifies that EventSourcing tag constants follow OpenTelemetry conventions.
	/// </summary>
	[Fact]
	public void EventSourcingTags_ShouldFollowOpenTelemetryConventions()
	{
		// Assert - Tags should use dot notation
		EventSourcingTags.AggregateId.ShouldBe("aggregate.id");
		EventSourcingTags.AggregateType.ShouldBe("aggregate.type");
		EventSourcingTags.Version.ShouldBe("event.version");
		EventSourcingTags.EventCount.ShouldBe("event.count");
		EventSourcingTags.EventId.ShouldBe("event.id");
		EventSourcingTags.BatchSize.ShouldBe("batch.size");
		EventSourcingTags.Provider.ShouldBe("store.provider");
		EventSourcingTags.OperationResult.ShouldBe("operation.result");

		_output.WriteLine("EventSourcing tags follow OpenTelemetry conventions");
	}

	#endregion EventSourcing Constants Verification

	#region Boundary Ownership Verification

	/// <summary>
	/// Verifies that Dispatch owns messaging telemetry (not EventSourcing concerns).
	/// </summary>
	[Fact]
	public void DispatchOwnership_ShouldBeMessagingOnly()
	{
		// Assert - Dispatch name should not reference EventSourcing concepts
		DispatchActivitySource.Name.ShouldNotContain("EventSourcing");
		DispatchActivitySource.Name.ShouldNotContain("Snapshot");
		DispatchActivitySource.Name.ShouldNotContain("Aggregate");

		_output.WriteLine("Dispatch activity source correctly scoped to messaging");
	}

	/// <summary>
	/// Verifies that EventSourcing owns domain telemetry (not messaging concerns).
	/// </summary>
	[Fact]
	public void EventSourcingOwnership_ShouldBeDomainOnly()
	{
		// Assert - EventSourcing should focus on domain concepts
		EventSourcingActivitySource.Name.ShouldContain("Excalibur");
		EventSourcingActivitySource.Name.ShouldContain("EventSourcing");
		EventSourcingActivitySource.Name.ShouldNotContain("Dispatch");
		EventSourcingActivitySource.Name.ShouldNotContain("Messaging");

		_output.WriteLine("EventSourcing activity source correctly scoped to domain");
	}

	/// <summary>
	/// Verifies that activity source names don't cross ADR-078 boundaries.
	/// </summary>
	[Fact]
	public void ActivitySources_ShouldRespectADR078Boundaries()
	{
		// Assert - Clear boundary separation
		// Dispatch: "Excalibur.Dispatch" - messaging primitives
		// Excalibur: "Excalibur.*" - domain/application framework

		DispatchActivitySource.Name.ShouldBe("Excalibur.Dispatch",
			"Dispatch should own messaging observability with 'Excalibur.Dispatch' prefix");

		// Excalibur packages should own domain observability with 'Excalibur.' prefix
		EventSourcingActivitySource.Name.ShouldStartWith("Excalibur.");

		_output.WriteLine("ADR-078 boundary compliance verified:");
		_output.WriteLine($"  Dispatch owns: \"{DispatchActivitySource.Name}\"");
		_output.WriteLine($"  Excalibur owns: \"{EventSourcingActivitySource.Name}\"");
	}

	#endregion Boundary Ownership Verification
}
