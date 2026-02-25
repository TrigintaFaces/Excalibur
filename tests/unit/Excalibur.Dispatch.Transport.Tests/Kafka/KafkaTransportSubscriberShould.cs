// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly — FakeItEasy .Returns() stores ValueTask

using Confluent.Kafka;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.Kafka;

/// <summary>
/// Unit tests for <see cref="KafkaTransportSubscriber"/>.
/// Validates constructor validation, source property, GetService, disposal, and interface implementation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class KafkaTransportSubscriberShould : IAsyncDisposable
{
	private const string TestSource = "orders-topic";
	private readonly IConsumer<string, byte[]> _fakeConsumer;
	private readonly KafkaTransportSubscriber _sut;

	public KafkaTransportSubscriberShould()
	{
		_fakeConsumer = A.Fake<IConsumer<string, byte[]>>();
		_sut = new KafkaTransportSubscriber(
			_fakeConsumer,
			TestSource,
			NullLogger<KafkaTransportSubscriber>.Instance);
	}

	[Fact]
	public void Expose_source_from_constructor()
	{
		_sut.Source.ShouldBe(TestSource);
	}

	[Fact]
	public void Throw_when_consumer_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new KafkaTransportSubscriber(null!, TestSource, NullLogger<KafkaTransportSubscriber>.Instance));
	}

	[Fact]
	public void Throw_when_source_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new KafkaTransportSubscriber(A.Fake<IConsumer<string, byte[]>>(), null!, NullLogger<KafkaTransportSubscriber>.Instance));
	}

	[Fact]
	public void Throw_when_logger_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new KafkaTransportSubscriber(A.Fake<IConsumer<string, byte[]>>(), TestSource, null!));
	}

	[Fact]
	public void Return_consumer_via_GetService()
	{
		var result = _sut.GetService(typeof(IConsumer<string, byte[]>));
		result.ShouldBe(_fakeConsumer);
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
		await _sut.DisposeAsync();
	}

	[Fact]
	public async Task Be_idempotent_on_multiple_DisposeAsync_calls()
	{
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
	public async Task Throw_when_handler_is_null()
	{
		using var cts = new CancellationTokenSource();

		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.SubscribeAsync(null!, cts.Token));
	}

	[Fact]
	public async Task Subscribe_to_topic_and_close_consumer_on_cancellation()
	{
		using var cts = new CancellationTokenSource();

		// Arrange: Consume throws OperationCanceledException when cancelled
		A.CallTo(() => _fakeConsumer.Consume(A<CancellationToken>._))
			.Throws(() => new OperationCanceledException());

		// Act
		await _sut.SubscribeAsync(
			(_, _) => Task.FromResult(MessageAction.Acknowledge),
			cts.Token);

		// Assert: Subscribe was called with the source topic
		A.CallTo(() => _fakeConsumer.Subscribe(TestSource))
			.MustHaveHappenedOnceExactly();

		// Assert: Close was called in the finally block
		A.CallTo(() => _fakeConsumer.Close())
			.MustHaveHappenedOnceExactly();
	}

	public async ValueTask DisposeAsync()
	{
		await _sut.DisposeAsync();
		_fakeConsumer.Dispose();
	}
}
