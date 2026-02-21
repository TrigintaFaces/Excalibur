// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly — FakeItEasy .Returns() stores ValueTask

using Azure.Messaging.ServiceBus;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Azure;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.ServiceBus;

/// <summary>
/// Unit tests for <see cref="ServiceBusTransportSubscriber"/>.
/// Validates push-based subscription, message settlement, error handling, and disposal.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class ServiceBusTransportSubscriberShould : IAsyncDisposable
{
	private const string TestSource = "orders-queue";
	private readonly ServiceBusProcessor _fakeProcessor;
	private readonly ServiceBusTransportSubscriber _sut;

	public ServiceBusTransportSubscriberShould()
	{
		_fakeProcessor = A.Fake<ServiceBusProcessor>();
		_sut = new ServiceBusTransportSubscriber(
			_fakeProcessor,
			TestSource,
			NullLogger<ServiceBusTransportSubscriber>.Instance);
	}

	[Fact]
	public void Expose_source_from_constructor()
	{
		_sut.Source.ShouldBe(TestSource);
	}

	[Fact]
	public void Throw_when_processor_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ServiceBusTransportSubscriber(null!, TestSource, NullLogger<ServiceBusTransportSubscriber>.Instance));
	}

	[Fact]
	public void Throw_when_source_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ServiceBusTransportSubscriber(A.Fake<ServiceBusProcessor>(), null!, NullLogger<ServiceBusTransportSubscriber>.Instance));
	}

	[Fact]
	public void Throw_when_logger_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ServiceBusTransportSubscriber(A.Fake<ServiceBusProcessor>(), TestSource, null!));
	}

	[Fact]
	public async Task Throw_when_handler_is_null()
	{
		using var cts = new CancellationTokenSource();

		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.SubscribeAsync(null!, cts.Token));
	}

	[Fact]
	public async Task Start_and_stop_processor_on_subscribe()
	{
		using var cts = new CancellationTokenSource();

		var subscribeTask = _sut.SubscribeAsync(
			(_, _) => Task.FromResult(MessageAction.Acknowledge),
			cts.Token);

		// Allow time for processor to start
		await Task.Delay(100);

		// Verify StartProcessingAsync was called
		A.CallTo(() => _fakeProcessor.StartProcessingAsync(A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();

		// Cancel the subscription
		await cts.CancelAsync();
		await subscribeTask;

		// Verify StopProcessingAsync was called
		A.CallTo(() => _fakeProcessor.StopProcessingAsync(A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void Return_processor_via_GetService()
	{
		var result = _sut.GetService(typeof(ServiceBusProcessor));
		result.ShouldBe(_fakeProcessor);
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

	public async ValueTask DisposeAsync()
	{
		await _sut.DisposeAsync();
		if (_fakeProcessor is IAsyncDisposable asyncDisposable)
		{
			await asyncDisposable.DisposeAsync();
		}
		else if (_fakeProcessor is IDisposable disposable)
		{
			disposable.Dispose();
		}
	}
}
