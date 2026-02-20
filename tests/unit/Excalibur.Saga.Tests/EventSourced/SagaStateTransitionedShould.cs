// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.EventSourced;
using Excalibur.Saga.Models;

namespace Excalibur.Saga.Tests.EventSourced;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SagaStateTransitionedShould
{
	[Fact]
	public void DefaultSagaIdToEmpty()
	{
		var sut = new SagaStateTransitioned();
		sut.SagaId.ShouldBe(string.Empty);
	}

	[Fact]
	public void ExposeEventTypeAsSagaStateTransitioned()
	{
		var sut = new SagaStateTransitioned();
		sut.EventType.ShouldBe("SagaStateTransitioned");
	}

	[Fact]
	public void SetOccurredAtToUtcNowByDefault()
	{
		var before = DateTimeOffset.UtcNow;
		var sut = new SagaStateTransitioned();
		var after = DateTimeOffset.UtcNow;

		sut.OccurredAt.ShouldBeGreaterThanOrEqualTo(before);
		sut.OccurredAt.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void ExposeFromAndToStatus()
	{
		var sut = new SagaStateTransitioned
		{
			SagaId = "saga-42",
			FromStatus = SagaStatus.Running,
			ToStatus = SagaStatus.Completed,
			Reason = "All steps completed",
		};

		sut.SagaId.ShouldBe("saga-42");
		sut.FromStatus.ShouldBe(SagaStatus.Running);
		sut.ToStatus.ShouldBe(SagaStatus.Completed);
		sut.Reason.ShouldBe("All steps completed");
	}

	[Fact]
	public void AllowNullReason()
	{
		var sut = new SagaStateTransitioned();
		sut.Reason.ShouldBeNull();
	}

	[Fact]
	public void ImplementISagaEvent()
	{
		var sut = new SagaStateTransitioned();
		sut.ShouldBeAssignableTo<ISagaEvent>();
	}
}
