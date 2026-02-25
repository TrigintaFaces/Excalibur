// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Saga.Orchestration;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Saga.Tests.Core.Orchestration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SagaCoordinatorDepthShould
{
	private readonly IServiceProvider _serviceProvider = A.Fake<IServiceProvider>();
	private readonly ISagaStore _sagaStore = A.Fake<ISagaStore>();

	[Fact]
	public async Task ProcessEventAsyncThrowsWhenMessageContextIsNull()
	{
		// Arrange
		var sut = new SagaCoordinator(_serviceProvider, _sagaStore, NullLogger<SagaCoordinator>.Instance);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await sut.ProcessEventAsync(null!, A.Fake<ISagaEvent>(), CancellationToken.None));
	}

	[Fact]
	public async Task ProcessEventAsyncThrowsWhenEventIsNull()
	{
		// Arrange
		var sut = new SagaCoordinator(_serviceProvider, _sagaStore, NullLogger<SagaCoordinator>.Instance);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await sut.ProcessEventAsync(A.Fake<IMessageContext>(), null!, CancellationToken.None));
	}

	[Fact]
	public async Task ProcessEventAsyncReturnsWhenNoSagaRegistered()
	{
		// Arrange - SagaRegistry has no registrations
		var sut = new SagaCoordinator(_serviceProvider, _sagaStore, NullLogger<SagaCoordinator>.Instance);
		var evt = A.Fake<ISagaEvent>();

		// Act - should not throw, just log and return
		await sut.ProcessEventAsync(A.Fake<IMessageContext>(), evt, CancellationToken.None);

		// Assert - sagaStore should not be called
		A.CallTo(_sagaStore).MustNotHaveHappened();
	}

	[Fact]
	public async Task HandleEventInternalAsyncThrowsWhenMessageContextIsNull()
	{
		// Arrange
		var sut = new SagaCoordinator(_serviceProvider, _sagaStore, NullLogger<SagaCoordinator>.Instance);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await sut.HandleEventInternalAsync<TestCoordinatorSaga, TestCoordinatorSagaState>(
				null!, A.Fake<ISagaEvent>(), new SagaInfo(typeof(TestCoordinatorSaga), typeof(TestCoordinatorSagaState)), CancellationToken.None));
	}

	[Fact]
	public async Task HandleEventInternalAsyncThrowsWhenEventIsNull()
	{
		// Arrange
		var sut = new SagaCoordinator(_serviceProvider, _sagaStore, NullLogger<SagaCoordinator>.Instance);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await sut.HandleEventInternalAsync<TestCoordinatorSaga, TestCoordinatorSagaState>(
				A.Fake<IMessageContext>(), null!, new SagaInfo(typeof(TestCoordinatorSaga), typeof(TestCoordinatorSagaState)), CancellationToken.None));
	}

	[Fact]
	public async Task HandleEventInternalAsyncThrowsWhenSagaInfoIsNull()
	{
		// Arrange
		var sut = new SagaCoordinator(_serviceProvider, _sagaStore, NullLogger<SagaCoordinator>.Instance);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await sut.HandleEventInternalAsync<TestCoordinatorSaga, TestCoordinatorSagaState>(
				A.Fake<IMessageContext>(), A.Fake<ISagaEvent>(), null!, CancellationToken.None));
	}
}

// Test types for coordinator testing
public class TestCoordinatorSagaState : SagaState
{
	public string OrderId { get; set; } = string.Empty;
}

public class TestCoordinatorSaga : SagaBase<TestCoordinatorSagaState>
{
	public TestCoordinatorSaga(TestCoordinatorSagaState state, IDispatcher dispatcher, Microsoft.Extensions.Logging.ILogger logger)
		: base(state, dispatcher, logger)
	{
	}

	public override bool HandlesEvent(object eventMessage) => false;

	public override Task HandleAsync(object eventMessage, CancellationToken cancellationToken) => Task.CompletedTask;
}
