// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly - FakeItEasy .Returns() stores ValueTask

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Google;

using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub;

/// <summary>
/// Unit tests for <see cref="PubSubTransportSubscriber"/>.
/// Validates constructor validation, source exposure, GetService, disposal, and interface implementation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class PubSubTransportSubscriberShould : IAsyncDisposable
{
	private const string TestSource = "projects/test-project/subscriptions/test-subscription";
	private readonly SubscriberClient _fakeSubscriber;
	private readonly PubSubTransportSubscriber _sut;

	public PubSubTransportSubscriberShould()
	{
		_fakeSubscriber = A.Fake<SubscriberClient>();
		_sut = new PubSubTransportSubscriber(
			_fakeSubscriber,
			TestSource,
			NullLogger<PubSubTransportSubscriber>.Instance);
	}

	[Fact]
	public void Expose_source_from_constructor()
	{
		_sut.Source.ShouldBe(TestSource);
	}

	[Fact]
	public void Throw_when_subscriber_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PubSubTransportSubscriber(null!, TestSource, NullLogger<PubSubTransportSubscriber>.Instance));
	}

	[Fact]
	public void Throw_when_source_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PubSubTransportSubscriber(A.Fake<SubscriberClient>(), null!, NullLogger<PubSubTransportSubscriber>.Instance));
	}

	[Fact]
	public void Throw_when_logger_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PubSubTransportSubscriber(A.Fake<SubscriberClient>(), TestSource, null!));
	}

	[Fact]
	public async Task Throw_when_handler_is_null()
	{
		using var cts = new CancellationTokenSource();

		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.SubscribeAsync(null!, cts.Token));
	}

	[Fact]
	public async Task Start_and_stop_subscriber_on_subscribe()
	{
		using var cts = new CancellationTokenSource();
		var subscriberStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		A.CallTo(() => _fakeSubscriber.StartAsync(A<Func<PubsubMessage, CancellationToken, Task<SubscriberClient.Reply>>>._))
			.Invokes(() => _ = subscriberStarted.TrySetResult())
			.Returns(Task.CompletedTask);

		var subscribeTask = _sut.SubscribeAsync(
			(_, _) => Task.FromResult(MessageAction.Acknowledge),
			cts.Token);

		var subscriberStartObserved = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			() => subscriberStarted.Task.IsCompleted,
			TimeSpan.FromSeconds(10),
			TimeSpan.FromMilliseconds(20));
		subscriberStartObserved.ShouldBeTrue("subscriber should start after subscribe");

		// Verify StartAsync was called
		A.CallTo(() => _fakeSubscriber.StartAsync(A<Func<PubsubMessage, CancellationToken, Task<SubscriberClient.Reply>>>._))
			.MustHaveHappenedOnceExactly();

		// Cancel the subscription
		await cts.CancelAsync();
		await subscribeTask;

		// Verify StopAsync was called
		A.CallTo(() => _fakeSubscriber.StopAsync(A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void Return_subscriber_client_via_GetService()
	{
		var result = _sut.GetService(typeof(SubscriberClient));
		result.ShouldBe(_fakeSubscriber);
	}

	[Fact]
	public void Return_null_for_unknown_service_type()
	{
		var result = _sut.GetService(typeof(string));
		result.ShouldBeNull();
	}

	[Fact]
	public void Throw_when_GetService_type_is_null()
	{
		Should.Throw<ArgumentNullException>(() => _sut.GetService(null!));
	}

	[Fact]
	public async Task Complete_without_throwing_on_DisposeAsync()
	{
		// Should not throw
		await _sut.DisposeAsync();
	}

	[Fact]
	public async Task Be_idempotent_on_multiple_DisposeAsync_calls()
	{
		// Both calls should complete without throwing
		await _sut.DisposeAsync();
		await _sut.DisposeAsync();
	}

	[Fact]
	public void Implement_ITransportSubscriber()
	{
		var subscriber = _sut as ITransportSubscriber;
		subscriber.ShouldNotBeNull();
		subscriber.Source.ShouldBe(TestSource);
	}

	[Fact]
	public void Implement_IAsyncDisposable()
	{
		var disposable = _sut as IAsyncDisposable;
		disposable.ShouldNotBeNull();
	}

	[Fact]
	public async Task Handle_StopAsync_InvalidOperationException_gracefully()
	{
		// StopAsync may throw InvalidOperationException if subscriber never started
		A.CallTo(() => _fakeSubscriber.StopAsync(A<CancellationToken>._))
			.Throws(new InvalidOperationException("Subscriber has not been started."));
		var subscriberStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		A.CallTo(() => _fakeSubscriber.StartAsync(A<Func<PubsubMessage, CancellationToken, Task<SubscriberClient.Reply>>>._))
			.Invokes(() => _ = subscriberStarted.TrySetResult())
			.Returns(Task.CompletedTask);

		using var cts = new CancellationTokenSource();

		var subscribeTask = _sut.SubscribeAsync(
			(_, _) => Task.FromResult(MessageAction.Acknowledge),
			cts.Token);

		var subscriberStartObserved = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			() => subscriberStarted.Task.IsCompleted,
			TimeSpan.FromSeconds(10),
			TimeSpan.FromMilliseconds(20));
		subscriberStartObserved.ShouldBeTrue("subscriber should start before cancellation");

		// Cancel the subscription - should not throw despite StopAsync throwing
		await cts.CancelAsync();
		await subscribeTask;
	}

	public async ValueTask DisposeAsync()
	{
		await _sut.DisposeAsync();
		if (_fakeSubscriber is IDisposable disposable)
		{
			disposable.Dispose();
		}
	}
}
