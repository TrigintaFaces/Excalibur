// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Bus;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Bus;

/// <summary>
///     Tests for the <see cref="InMemoryMessageBusAdapter" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class InMemoryMessageBusAdapterShould : IAsyncDisposable
{
	private readonly InMemoryMessageBusAdapter _sut;

	public InMemoryMessageBusAdapterShould()
	{
		_sut = new InMemoryMessageBusAdapter(NullLogger<InMemoryMessageBusAdapter>.Instance);
	}

	[Fact]
	public void ThrowForNullLogger() =>
		Should.Throw<ArgumentNullException>(() => new InMemoryMessageBusAdapter(null!));

	[Fact]
	public void HaveCorrectName()
	{
		_sut.Name.ShouldBe("InMemory");
	}

	[Fact]
	public void SupportPublishing()
	{
		_sut.SupportsPublishing.ShouldBeTrue();
	}

	[Fact]
	public void SupportSubscription()
	{
		_sut.SupportsSubscription.ShouldBeTrue();
	}

	[Fact]
	public void NotSupportTransactions()
	{
		_sut.SupportsTransactions.ShouldBeFalse();
	}

	[Fact]
	public void NotBeConnectedInitially()
	{
		_sut.IsConnected.ShouldBeFalse();
	}

	[Fact]
	public async Task InitializeSuccessfully()
	{
		var options = A.Fake<IMessageBusOptions>();
		await _sut.InitializeAsync(options, CancellationToken.None).ConfigureAwait(false);
		_sut.IsConnected.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFailedResultWhenNotConnected()
	{
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		var result = await _sut.PublishAsync(message, context, CancellationToken.None).ConfigureAwait(false);
		result.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public async Task PublishSuccessfullyWhenConnected()
	{
		var options = A.Fake<IMessageBusOptions>();
		await _sut.InitializeAsync(options, CancellationToken.None).ConfigureAwait(false);

		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("msg-1");
		A.CallTo(() => context.Message).Returns(message);

		var result = await _sut.PublishAsync(message, context, CancellationToken.None).ConfigureAwait(false);
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task ThrowForNullMessageOnPublish()
	{
		var options = A.Fake<IMessageBusOptions>();
		await _sut.InitializeAsync(options, CancellationToken.None).ConfigureAwait(false);

		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.PublishAsync(null!, A.Fake<IMessageContext>(), CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ThrowForNullContextOnPublish()
	{
		var options = A.Fake<IMessageBusOptions>();
		await _sut.InitializeAsync(options, CancellationToken.None).ConfigureAwait(false);

		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.PublishAsync(A.Fake<IDispatchMessage>(), null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task SubscribeSuccessfully()
	{
		await _sut.StartAsync(CancellationToken.None).ConfigureAwait(false);

		await _sut.SubscribeAsync(
			"test-sub",
			(_, _, _) => Task.FromResult<IMessageResult>(MessageResult.Success()),
			null,
			CancellationToken.None).ConfigureAwait(false);
	}

	[Fact]
	public async Task ThrowForNullSubscriptionName() =>
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.SubscribeAsync(null!, (_, _, _) => Task.FromResult<IMessageResult>(MessageResult.Success()), null, CancellationToken.None)).ConfigureAwait(false);

	[Fact]
	public async Task ThrowWhenSubscribingWhileNotConnected() =>
		await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.SubscribeAsync("sub", (_, _, _) => Task.FromResult<IMessageResult>(MessageResult.Success()), null, CancellationToken.None)).ConfigureAwait(false);

	[Fact]
	public async Task UnsubscribeSuccessfully()
	{
		await _sut.StartAsync(CancellationToken.None).ConfigureAwait(false);
		await _sut.SubscribeAsync("sub-1", (_, _, _) => Task.FromResult<IMessageResult>(MessageResult.Success()), null, CancellationToken.None).ConfigureAwait(false);
		await _sut.UnsubscribeAsync("sub-1", CancellationToken.None).ConfigureAwait(false);
	}

	[Fact]
	public async Task CheckHealthWhenConnected()
	{
		await _sut.StartAsync(CancellationToken.None).ConfigureAwait(false);
		var health = await _sut.CheckHealthAsync(CancellationToken.None).ConfigureAwait(false);
		health.Status.ShouldBe(HealthCheckStatus.Healthy);
	}

	[Fact]
	public async Task CheckHealthWhenDisconnected()
	{
		var health = await _sut.CheckHealthAsync(CancellationToken.None).ConfigureAwait(false);
		health.Status.ShouldBe(HealthCheckStatus.Unhealthy);
	}

	[Fact]
	public async Task StartAndStopSuccessfully()
	{
		await _sut.StartAsync(CancellationToken.None).ConfigureAwait(false);
		_sut.IsConnected.ShouldBeTrue();
		await _sut.StopAsync(CancellationToken.None).ConfigureAwait(false);
		_sut.IsConnected.ShouldBeFalse();
	}

	[Fact]
	public async Task DisposeMultipleTimesSafely()
	{
		await _sut.DisposeAsync().ConfigureAwait(false);
		await _sut.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public void SyncDisposeMultipleTimesSafely()
	{
		_sut.Dispose();
		_sut.Dispose();
	}

	public async ValueTask DisposeAsync() => await _sut.DisposeAsync().ConfigureAwait(false);
}
