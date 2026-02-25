// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.EventSourced;
using Excalibur.Saga.Models;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Saga.Tests.EventSourced;

/// <summary>
/// Functional tests for <see cref="InMemoryEventSourcedSagaStore"/> covering
/// full saga lifecycle replay, concurrent access, mixed event types, and
/// custom stream prefix behavior.
/// </summary>
[Trait("Category", "Unit")]
public sealed class InMemoryEventSourcedSagaStoreFunctionalShould
{
	private static InMemoryEventSourcedSagaStore CreateStore(string prefix = "saga-") =>
		new(
			Microsoft.Extensions.Options.Options.Create(new EventSourcedSagaOptions { StreamPrefix = prefix }),
			NullLogger<InMemoryEventSourcedSagaStore>.Instance);

	[Fact]
	public async Task RehydrateFullSagaLifecycle_Created_Running_Completed()
	{
		// Arrange
		var store = CreateStore();
		var now = DateTimeOffset.UtcNow;

		await store.AppendEventAsync("saga-1",
			new SagaStateTransitioned
			{
				SagaId = "saga-1",
				FromStatus = SagaStatus.Created,
				ToStatus = SagaStatus.Running,
				OccurredAt = now,
			}, CancellationToken.None);

		await store.AppendEventAsync("saga-1",
			new SagaStepCompleted
			{
				SagaId = "saga-1",
				StepName = "ValidateOrder",
				StepIndex = 0,
				Duration = TimeSpan.FromMilliseconds(100),
				OccurredAt = now.AddSeconds(1),
			}, CancellationToken.None);

		await store.AppendEventAsync("saga-1",
			new SagaStepCompleted
			{
				SagaId = "saga-1",
				StepName = "ProcessPayment",
				StepIndex = 1,
				Duration = TimeSpan.FromMilliseconds(200),
				OccurredAt = now.AddSeconds(2),
			}, CancellationToken.None);

		await store.AppendEventAsync("saga-1",
			new SagaStateTransitioned
			{
				SagaId = "saga-1",
				FromStatus = SagaStatus.Running,
				ToStatus = SagaStatus.Completed,
				OccurredAt = now.AddSeconds(3),
			}, CancellationToken.None);

		// Act
		var state = await store.RehydrateAsync("saga-1", CancellationToken.None);

		// Assert
		state.ShouldNotBeNull();
		state.SagaId.ShouldBe("saga-1");
		state.Status.ShouldBe(SagaStatus.Completed);
		state.CurrentStepIndex.ShouldBe(2);
		state.StepHistory.Count.ShouldBe(2);
		state.StepHistory[0].StepName.ShouldBe("ValidateOrder");
		state.StepHistory[0].IsSuccess.ShouldBeTrue();
		state.StepHistory[1].StepName.ShouldBe("ProcessPayment");
		state.StepHistory[1].IsSuccess.ShouldBeTrue();
		state.CompletedAt.ShouldNotBeNull();
		state.StartedAt.ShouldBe(now.UtcDateTime);
		state.LastUpdatedAt.ShouldBe(now.AddSeconds(3).UtcDateTime);
	}

	[Fact]
	public async Task RehydrateFailedSaga_WithStepFailureAndCompensation()
	{
		// Arrange
		var store = CreateStore();
		var now = DateTimeOffset.UtcNow;

		await store.AppendEventAsync("saga-2",
			new SagaStateTransitioned
			{
				SagaId = "saga-2",
				FromStatus = SagaStatus.Created,
				ToStatus = SagaStatus.Running,
				OccurredAt = now,
			}, CancellationToken.None);

		await store.AppendEventAsync("saga-2",
			new SagaStepCompleted
			{
				SagaId = "saga-2",
				StepName = "Step1",
				StepIndex = 0,
				Duration = TimeSpan.FromMilliseconds(50),
				OccurredAt = now.AddSeconds(1),
			}, CancellationToken.None);

		await store.AppendEventAsync("saga-2",
			new SagaStepFailed
			{
				SagaId = "saga-2",
				StepName = "Step2",
				StepIndex = 1,
				ErrorMessage = "Payment declined",
				RetryCount = 3,
				OccurredAt = now.AddSeconds(5),
			}, CancellationToken.None);

		await store.AppendEventAsync("saga-2",
			new SagaStateTransitioned
			{
				SagaId = "saga-2",
				FromStatus = SagaStatus.Running,
				ToStatus = SagaStatus.Failed,
				Reason = "Step2 failed after 3 retries",
				OccurredAt = now.AddSeconds(6),
			}, CancellationToken.None);

		// Act
		var state = await store.RehydrateAsync("saga-2", CancellationToken.None);

		// Assert
		state.ShouldNotBeNull();
		state.Status.ShouldBe(SagaStatus.Failed);
		state.ErrorMessage.ShouldBe("Step2 failed after 3 retries");
		state.StepHistory.Count.ShouldBe(2);
		state.StepHistory[0].IsSuccess.ShouldBeTrue();
		state.StepHistory[1].IsSuccess.ShouldBeFalse();
		state.StepHistory[1].ErrorMessage.ShouldBe("Payment declined");
		state.StepHistory[1].RetryCount.ShouldBe(3);
		state.CompletedAt.ShouldNotBeNull();
	}

	[Fact]
	public async Task MaintainIndependentStreams_ForDifferentSagas()
	{
		// Arrange
		var store = CreateStore();
		var now = DateTimeOffset.UtcNow;

		await store.AppendEventAsync("saga-a",
			new SagaStateTransitioned
			{
				SagaId = "saga-a",
				FromStatus = SagaStatus.Created,
				ToStatus = SagaStatus.Running,
				OccurredAt = now,
			}, CancellationToken.None);

		await store.AppendEventAsync("saga-b",
			new SagaStateTransitioned
			{
				SagaId = "saga-b",
				FromStatus = SagaStatus.Created,
				ToStatus = SagaStatus.Completed,
				OccurredAt = now,
			}, CancellationToken.None);

		// Act
		var stateA = await store.RehydrateAsync("saga-a", CancellationToken.None);
		var stateB = await store.RehydrateAsync("saga-b", CancellationToken.None);

		// Assert
		stateA.ShouldNotBeNull();
		stateA.Status.ShouldBe(SagaStatus.Running);

		stateB.ShouldNotBeNull();
		stateB.Status.ShouldBe(SagaStatus.Completed);

		store.StreamCount.ShouldBe(2);
	}

	[Fact]
	public async Task SupportCustomStreamPrefix()
	{
		// Arrange
		var store = CreateStore("custom-prefix-");

		await store.AppendEventAsync("saga-1",
			new SagaStateTransitioned
			{
				SagaId = "saga-1",
				FromStatus = SagaStatus.Created,
				ToStatus = SagaStatus.Running,
				OccurredAt = DateTimeOffset.UtcNow,
			}, CancellationToken.None);

		// Act
		var state = await store.RehydrateAsync("saga-1", CancellationToken.None);

		// Assert - the stream name uses custom prefix but rehydrate should still work
		state.ShouldNotBeNull();
		state.Status.ShouldBe(SagaStatus.Running);
		store.StreamCount.ShouldBe(1);
	}

	[Fact]
	public async Task HandleConcurrentAppends_ThreadSafely()
	{
		// Arrange
		var store = CreateStore();
		var tasks = new List<Task>();

		for (var i = 0; i < 50; i++)
		{
			var index = i;
			tasks.Add(store.AppendEventAsync($"saga-{index % 5}",
				new SagaStepCompleted
				{
					SagaId = $"saga-{index % 5}",
					StepName = $"Step{index}",
					StepIndex = index,
					Duration = TimeSpan.FromMilliseconds(1),
					OccurredAt = DateTimeOffset.UtcNow,
				}, CancellationToken.None));
		}

		// Act
		await Task.WhenAll(tasks);

		// Assert - 5 streams, 10 events each
		store.StreamCount.ShouldBe(5);
		store.TotalEventCount.ShouldBe(50);
	}

	[Fact]
	public async Task TrackTotalEventCount_AcrossStreams()
	{
		// Arrange
		var store = CreateStore();
		var now = DateTimeOffset.UtcNow;

		await store.AppendEventAsync("saga-1",
			new SagaStateTransitioned { SagaId = "saga-1", FromStatus = SagaStatus.Created, ToStatus = SagaStatus.Running, OccurredAt = now },
			CancellationToken.None);
		await store.AppendEventAsync("saga-1",
			new SagaStepCompleted { SagaId = "saga-1", StepName = "S1", StepIndex = 0, Duration = TimeSpan.Zero, OccurredAt = now },
			CancellationToken.None);
		await store.AppendEventAsync("saga-2",
			new SagaStateTransitioned { SagaId = "saga-2", FromStatus = SagaStatus.Created, ToStatus = SagaStatus.Running, OccurredAt = now },
			CancellationToken.None);

		// Assert
		store.StreamCount.ShouldBe(2);
		store.TotalEventCount.ShouldBe(3);
	}

	[Fact]
	public async Task ClearAllStreams()
	{
		// Arrange
		var store = CreateStore();
		await store.AppendEventAsync("saga-1",
			new SagaStateTransitioned { SagaId = "saga-1", FromStatus = SagaStatus.Created, ToStatus = SagaStatus.Running, OccurredAt = DateTimeOffset.UtcNow },
			CancellationToken.None);
		await store.AppendEventAsync("saga-2",
			new SagaStateTransitioned { SagaId = "saga-2", FromStatus = SagaStatus.Created, ToStatus = SagaStatus.Running, OccurredAt = DateTimeOffset.UtcNow },
			CancellationToken.None);

		// Act
		store.Clear();

		// Assert
		store.StreamCount.ShouldBe(0);
		store.TotalEventCount.ShouldBe(0);
		(await store.RehydrateAsync("saga-1", CancellationToken.None)).ShouldBeNull();
	}

	[Fact]
	public async Task ReturnNull_ForNonexistentSaga()
	{
		// Arrange
		var store = CreateStore();

		// Act
		var state = await store.RehydrateAsync("nonexistent", CancellationToken.None);

		// Assert
		state.ShouldBeNull();
	}

	[Fact]
	public async Task RehydrateCompensatedSaga()
	{
		// Arrange
		var store = CreateStore();
		var now = DateTimeOffset.UtcNow;

		await store.AppendEventAsync("saga-comp",
			new SagaStateTransitioned { SagaId = "saga-comp", FromStatus = SagaStatus.Created, ToStatus = SagaStatus.Running, OccurredAt = now },
			CancellationToken.None);
		await store.AppendEventAsync("saga-comp",
			new SagaStateTransitioned { SagaId = "saga-comp", FromStatus = SagaStatus.Running, ToStatus = SagaStatus.Compensating, OccurredAt = now.AddSeconds(1) },
			CancellationToken.None);
		await store.AppendEventAsync("saga-comp",
			new SagaStateTransitioned { SagaId = "saga-comp", FromStatus = SagaStatus.Compensating, ToStatus = SagaStatus.Compensated, OccurredAt = now.AddSeconds(2) },
			CancellationToken.None);

		// Act
		var state = await store.RehydrateAsync("saga-comp", CancellationToken.None);

		// Assert
		state.ShouldNotBeNull();
		state.Status.ShouldBe(SagaStatus.Compensated);
		state.CompletedAt.ShouldNotBeNull();
	}

	[Fact]
	public async Task RehydrateCancelledSaga()
	{
		// Arrange
		var store = CreateStore();
		var now = DateTimeOffset.UtcNow;

		await store.AppendEventAsync("saga-cancel",
			new SagaStateTransitioned { SagaId = "saga-cancel", FromStatus = SagaStatus.Running, ToStatus = SagaStatus.Cancelled, Reason = "User requested", OccurredAt = now },
			CancellationToken.None);

		// Act
		var state = await store.RehydrateAsync("saga-cancel", CancellationToken.None);

		// Assert
		state.ShouldNotBeNull();
		state.Status.ShouldBe(SagaStatus.Cancelled);
		state.CompletedAt.ShouldNotBeNull();
		state.ErrorMessage.ShouldBe("User requested");
	}
}
