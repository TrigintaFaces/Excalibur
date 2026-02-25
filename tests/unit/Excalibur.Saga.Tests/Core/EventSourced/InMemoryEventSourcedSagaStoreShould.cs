// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.EventSourced;
using Excalibur.Saga.Models;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Tests.Core.EventSourced;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryEventSourcedSagaStoreShould
{
	private readonly InMemoryEventSourcedSagaStore _sut;

	public InMemoryEventSourcedSagaStoreShould()
	{
		_sut = new InMemoryEventSourcedSagaStore(
			Microsoft.Extensions.Options.Options.Create(new EventSourcedSagaOptions { StreamPrefix = "saga-" }),
			NullLogger<InMemoryEventSourcedSagaStore>.Instance);
	}

	[Fact]
	public async Task AppendEventAsync_StoreEvent()
	{
		// Arrange
		var sagaEvent = new SagaStateTransitioned
		{
			SagaId = "saga-1",
			FromStatus = SagaStatus.Created,
			ToStatus = SagaStatus.Running,
			OccurredAt = DateTimeOffset.UtcNow
		};

		// Act
		await _sut.AppendEventAsync("saga-1", sagaEvent, CancellationToken.None);

		// Assert
		_sut.StreamCount.ShouldBe(1);
		_sut.TotalEventCount.ShouldBe(1);
	}

	[Fact]
	public async Task RehydrateAsync_ReturnNull_WhenNoEvents()
	{
		var result = await _sut.RehydrateAsync("nonexistent", CancellationToken.None);
		result.ShouldBeNull();
	}

	[Fact]
	public async Task RehydrateAsync_RebuildStateFromTransitionEvents()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;
		await _sut.AppendEventAsync("saga-1",
			new SagaStateTransitioned { SagaId = "saga-1", FromStatus = SagaStatus.Created, ToStatus = SagaStatus.Running, OccurredAt = now },
			CancellationToken.None);
		await _sut.AppendEventAsync("saga-1",
			new SagaStateTransitioned { SagaId = "saga-1", FromStatus = SagaStatus.Running, ToStatus = SagaStatus.Completed, OccurredAt = now.AddSeconds(5) },
			CancellationToken.None);

		// Act
		var state = await _sut.RehydrateAsync("saga-1", CancellationToken.None);

		// Assert
		state.ShouldNotBeNull();
		state.SagaId.ShouldBe("saga-1");
		state.Status.ShouldBe(SagaStatus.Completed);
		state.CompletedAt.ShouldNotBeNull();
	}

	[Fact]
	public async Task RehydrateAsync_TrackStepCompletions()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;
		await _sut.AppendEventAsync("saga-1",
			new SagaStateTransitioned { SagaId = "saga-1", FromStatus = SagaStatus.Created, ToStatus = SagaStatus.Running, OccurredAt = now },
			CancellationToken.None);
		await _sut.AppendEventAsync("saga-1",
			new SagaStepCompleted { SagaId = "saga-1", StepName = "Step1", StepIndex = 0, Duration = TimeSpan.FromSeconds(1), OccurredAt = now.AddSeconds(1) },
			CancellationToken.None);
		await _sut.AppendEventAsync("saga-1",
			new SagaStepCompleted { SagaId = "saga-1", StepName = "Step2", StepIndex = 1, Duration = TimeSpan.FromSeconds(2), OccurredAt = now.AddSeconds(3) },
			CancellationToken.None);

		// Act
		var state = await _sut.RehydrateAsync("saga-1", CancellationToken.None);

		// Assert
		state.ShouldNotBeNull();
		state.CurrentStepIndex.ShouldBe(2);
		state.StepHistory.Count.ShouldBe(2);
		state.StepHistory[0].StepName.ShouldBe("Step1");
		state.StepHistory[0].IsSuccess.ShouldBeTrue();
		state.StepHistory[1].StepName.ShouldBe("Step2");
	}

	[Fact]
	public async Task RehydrateAsync_TrackStepFailures()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;
		await _sut.AppendEventAsync("saga-1",
			new SagaStateTransitioned { SagaId = "saga-1", FromStatus = SagaStatus.Created, ToStatus = SagaStatus.Running, OccurredAt = now },
			CancellationToken.None);
		await _sut.AppendEventAsync("saga-1",
			new SagaStepFailed { SagaId = "saga-1", StepName = "Step1", StepIndex = 0, ErrorMessage = "Something broke", RetryCount = 1, OccurredAt = now.AddSeconds(1) },
			CancellationToken.None);

		// Act
		var state = await _sut.RehydrateAsync("saga-1", CancellationToken.None);

		// Assert
		state.ShouldNotBeNull();
		state.StepHistory.Count.ShouldBe(1);
		state.StepHistory[0].IsSuccess.ShouldBeFalse();
		state.StepHistory[0].ErrorMessage.ShouldBe("Something broke");
		state.ErrorMessage.ShouldBe("Something broke");
	}

	[Fact]
	public async Task RehydrateAsync_SetErrorMessage_WhenTransitionHasReason()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;
		await _sut.AppendEventAsync("saga-1",
			new SagaStateTransitioned { SagaId = "saga-1", FromStatus = SagaStatus.Running, ToStatus = SagaStatus.Failed, Reason = "deadline exceeded", OccurredAt = now },
			CancellationToken.None);

		// Act
		var state = await _sut.RehydrateAsync("saga-1", CancellationToken.None);

		// Assert
		state.ShouldNotBeNull();
		state.ErrorMessage.ShouldBe("deadline exceeded");
		state.Status.ShouldBe(SagaStatus.Failed);
	}

	[Fact]
	public async Task Clear_RemoveAllStreams()
	{
		// Arrange
		await _sut.AppendEventAsync("saga-1",
			new SagaStateTransitioned { SagaId = "saga-1", FromStatus = SagaStatus.Created, ToStatus = SagaStatus.Running, OccurredAt = DateTimeOffset.UtcNow },
			CancellationToken.None);
		_sut.StreamCount.ShouldBe(1);

		// Act
		_sut.Clear();

		// Assert
		_sut.StreamCount.ShouldBe(0);
		_sut.TotalEventCount.ShouldBe(0);
	}

	[Fact]
	public async Task ThrowOnInvalidArgs()
	{
		var evt = new SagaStateTransitioned { SagaId = "s", FromStatus = SagaStatus.Created, ToStatus = SagaStatus.Running };

		await Should.ThrowAsync<ArgumentException>(
			() => _sut.AppendEventAsync(null!, evt, CancellationToken.None));
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.AppendEventAsync("", evt, CancellationToken.None));
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.AppendEventAsync("saga-1", null!, CancellationToken.None));
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.RehydrateAsync(null!, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnNullConstructorArgs()
	{
		var opts = Microsoft.Extensions.Options.Options.Create(new EventSourcedSagaOptions());
		var logger = NullLogger<InMemoryEventSourcedSagaStore>.Instance;

		Should.Throw<ArgumentNullException>(() => new InMemoryEventSourcedSagaStore(null!, logger));
		Should.Throw<ArgumentNullException>(() => new InMemoryEventSourcedSagaStore(opts, null!));
	}
}
