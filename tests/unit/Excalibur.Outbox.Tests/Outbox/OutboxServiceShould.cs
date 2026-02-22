using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Delivery;

#pragma warning disable CA2012 // Use ValueTasks correctly - FakeItEasy requires this

namespace Excalibur.Outbox.Tests.Outbox;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class OutboxServiceShould
{
	private readonly IOutboxDispatcher _fakeOutbox;

	public OutboxServiceShould()
	{
		_fakeOutbox = A.Fake<IOutboxDispatcher>();
	}

	[Fact]
	public void BeABackgroundService()
	{
		// Arrange & Act
		using var service = new OutboxService(_fakeOutbox);

		// Assert
		service.ShouldBeAssignableTo<Microsoft.Extensions.Hosting.BackgroundService>();
	}

	[Fact]
	public async Task DelegateExecutionToOutboxDispatcher()
	{
		// Arrange
		var dispatchStarted = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
		using var service = new OutboxService(_fakeOutbox);

		A.CallTo(() => _fakeOutbox.RunOutboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.Invokes((string dispatcherId, CancellationToken _) => dispatchStarted.TrySetResult(dispatcherId))
			.Returns(Task.FromResult(0));

		// Act
		await service.StartAsync(CancellationToken.None).ConfigureAwait(true);
		var dispatcherId = await dispatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(true);
		await service.StopAsync(CancellationToken.None).ConfigureAwait(true);

		// Assert
		dispatcherId.ShouldNotBeNullOrWhiteSpace();
		A.CallTo(() => _fakeOutbox.RunOutboxDispatchAsync(A<string>.That.IsNotNull(), A<CancellationToken>._))
			.MustHaveHappenedOnceOrMore();
	}

	[Fact]
	public async Task DisposeOutboxOnStop()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		using var service = new OutboxService(_fakeOutbox);

		A.CallTo(() => _fakeOutbox.RunOutboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult(0));
		A.CallTo(() => _fakeOutbox.DisposeAsync()).Returns(ValueTask.CompletedTask);

		// Act
		await service.StartAsync(cts.Token).ConfigureAwait(true);
		await service.StopAsync(CancellationToken.None).ConfigureAwait(true);

		// Assert
		A.CallTo(() => _fakeOutbox.DisposeAsync()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PassUniqueDispatcherIdToOutbox()
	{
		// Arrange
		var capturedDispatcherId = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
		using var service = new OutboxService(_fakeOutbox);

		A.CallTo(() => _fakeOutbox.RunOutboxDispatchAsync(A<string>._, A<CancellationToken>._))
			.Invokes((string id, CancellationToken _) => capturedDispatcherId.TrySetResult(id))
			.Returns(Task.FromResult(0));

		// Act
		await service.StartAsync(CancellationToken.None).ConfigureAwait(true);
		var capturedId = await capturedDispatcherId.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(true);
		await service.StopAsync(CancellationToken.None).ConfigureAwait(true);

		// Assert
		capturedId.ShouldNotBeNullOrWhiteSpace();
	}
}

#pragma warning restore CA2012

