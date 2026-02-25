// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Saga.Faults;

namespace Excalibur.Saga.Tests.Faults;

/// <summary>
/// Unit tests for <see cref="SagaFaultEvent"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaFaultEventShould
{
	[Fact]
	public void ImplementISagaFaultEvent()
	{
		var evt = new SagaFaultEvent();
		evt.ShouldBeAssignableTo<ISagaFaultEvent>();
	}

	[Fact]
	public void ImplementIDomainEvent()
	{
		var evt = new SagaFaultEvent();
		evt.ShouldBeAssignableTo<IDomainEvent>();
	}

	[Fact]
	public void HaveDefaultEventType()
	{
		var evt = new SagaFaultEvent();
		evt.EventType.ShouldBe(nameof(SagaFaultEvent));
	}

	[Fact]
	public void GenerateUniqueEventIds()
	{
		var evt1 = new SagaFaultEvent();
		var evt2 = new SagaFaultEvent();

		evt1.EventId.ShouldNotBe(evt2.EventId);
	}

	[Fact]
	public void SetDefaultOccurredAtToUtcNow()
	{
		var before = DateTimeOffset.UtcNow;
		var evt = new SagaFaultEvent();

		evt.OccurredAt.ShouldBeGreaterThanOrEqualTo(before);
	}

	[Fact]
	public void AllowInitializingAllProperties()
	{
		var metadata = new Dictionary<string, object>(StringComparer.Ordinal) { ["key"] = "value" };

		var evt = new SagaFaultEvent
		{
			SagaId = "saga-1",
			AggregateId = "saga-1",
			FaultReason = "timeout",
			FailedStepName = "Step3",
			EventId = "evt-1",
			Version = 5,
			OccurredAt = DateTimeOffset.UnixEpoch,
			EventType = "CustomType",
			Metadata = metadata,
		};

		evt.SagaId.ShouldBe("saga-1");
		evt.AggregateId.ShouldBe("saga-1");
		evt.FaultReason.ShouldBe("timeout");
		evt.FailedStepName.ShouldBe("Step3");
		evt.EventId.ShouldBe("evt-1");
		evt.Version.ShouldBe(5);
		evt.OccurredAt.ShouldBe(DateTimeOffset.UnixEpoch);
		evt.EventType.ShouldBe("CustomType");
		evt.Metadata.ShouldBe(metadata);
	}

	[Fact]
	public void HaveNullMetadataByDefault()
	{
		var evt = new SagaFaultEvent();
		evt.Metadata.ShouldBeNull();
	}
}
