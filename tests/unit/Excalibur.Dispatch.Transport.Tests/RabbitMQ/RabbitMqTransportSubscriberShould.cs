// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly -- FakeItEasy .Returns() stores ValueTask

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.Logging.Abstractions;

using RabbitMQ.Client;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ;

/// <summary>
/// Unit tests for <see cref="RabbitMqTransportSubscriber"/>.
/// Validates push-based subscription, message settlement, error handling, and disposal.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class RabbitMqTransportSubscriberShould : IAsyncDisposable
{
	private const string TestSource = "orders-queue";
	private const string TestQueueName = "orders-queue";
	private readonly IChannel _fakeChannel;
	private readonly RabbitMqTransportSubscriber _sut;

	public RabbitMqTransportSubscriberShould()
	{
		_fakeChannel = A.Fake<IChannel>();
		_sut = new RabbitMqTransportSubscriber(
			_fakeChannel,
			TestSource,
			TestQueueName,
			NullLogger<RabbitMqTransportSubscriber>.Instance);
	}

	[Fact]
	public void Expose_source_from_constructor()
	{
		_sut.Source.ShouldBe(TestSource);
	}

	[Fact]
	public void Throw_when_channel_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new RabbitMqTransportSubscriber(
				null!,
				TestSource,
				TestQueueName,
				NullLogger<RabbitMqTransportSubscriber>.Instance));
	}

	[Fact]
	public void Throw_when_source_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new RabbitMqTransportSubscriber(
				A.Fake<IChannel>(),
				null!,
				TestQueueName,
				NullLogger<RabbitMqTransportSubscriber>.Instance));
	}

	[Fact]
	public void Throw_when_queueName_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new RabbitMqTransportSubscriber(
				A.Fake<IChannel>(),
				TestSource,
				null!,
				NullLogger<RabbitMqTransportSubscriber>.Instance));
	}

	[Fact]
	public void Throw_when_logger_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new RabbitMqTransportSubscriber(
				A.Fake<IChannel>(),
				TestSource,
				TestQueueName,
				null!));
	}

	[Fact]
	public async Task Throw_when_handler_is_null()
	{
		using var cts = new CancellationTokenSource();

		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.SubscribeAsync(null!, cts.Token));
	}

	[Fact]
	public async Task Apply_prefetch_qos_when_prefetch_is_configured()
	{
		var channel = A.Fake<IChannel>();
		_ = A.CallTo(() => channel.BasicConsumeAsync(
				A<string>._,
				A<bool>._,
				A<string>._,
				A<bool>._,
				A<bool>._,
				A<IDictionary<string, object?>>._,
				A<IAsyncBasicConsumer>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult("consumer-tag"));
		_ = A.CallTo(() => channel.BasicCancelAsync(
				A<string>._,
				A<bool>._,
				A<CancellationToken>._))
			.Returns(Task.CompletedTask);

		var sut = new RabbitMqTransportSubscriber(
			channel,
			TestSource,
			TestQueueName,
			NullLogger<RabbitMqTransportSubscriber>.Instance,
			prefetchCount: 25,
			prefetchGlobal: false);

		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
		await sut.SubscribeAsync(static (_, _) => Task.FromResult(MessageAction.Acknowledge), cts.Token);

		A.CallTo(() => channel.BasicQosAsync(
				prefetchSize: 0,
				prefetchCount: (ushort)25,
				global: false,
				cancellationToken: A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task Skip_prefetch_qos_when_prefetch_is_zero()
	{
		var channel = A.Fake<IChannel>();
		_ = A.CallTo(() => channel.BasicConsumeAsync(
				A<string>._,
				A<bool>._,
				A<string>._,
				A<bool>._,
				A<bool>._,
				A<IDictionary<string, object?>>._,
				A<IAsyncBasicConsumer>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult("consumer-tag"));
		_ = A.CallTo(() => channel.BasicCancelAsync(
				A<string>._,
				A<bool>._,
				A<CancellationToken>._))
			.Returns(Task.CompletedTask);

		var sut = new RabbitMqTransportSubscriber(
			channel,
			TestSource,
			TestQueueName,
			NullLogger<RabbitMqTransportSubscriber>.Instance);

		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
		await sut.SubscribeAsync(static (_, _) => Task.FromResult(MessageAction.Acknowledge), cts.Token);

		A.CallTo(() => channel.BasicQosAsync(
				prefetchSize: A<uint>._,
				prefetchCount: A<ushort>._,
				global: A<bool>._,
				cancellationToken: A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public void Return_channel_via_GetService()
	{
		var result = _sut.GetService(typeof(IChannel));
		result.ShouldBe(_fakeChannel);
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
		_fakeChannel.Dispose();
	}
}
