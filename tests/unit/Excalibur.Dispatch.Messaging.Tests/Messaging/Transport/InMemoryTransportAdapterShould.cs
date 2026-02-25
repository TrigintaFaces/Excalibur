// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

/// <summary>
///     Tests for the <see cref="InMemoryTransportAdapter" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class InMemoryTransportAdapterShould : IAsyncDisposable
{
	private readonly InMemoryTransportAdapter _sut = new(NullLogger<InMemoryTransportAdapter>.Instance);

	[Fact]
	public void ThrowForNullLogger() =>
		Should.Throw<ArgumentNullException>(() => new InMemoryTransportAdapter(null!));

	[Fact]
	public void CreateWithDefaultOptions()
	{
		_sut.ShouldNotBeNull();
		_sut.Name.ShouldBe(InMemoryTransportAdapter.DefaultName);
		_sut.TransportType.ShouldBe(InMemoryTransportAdapter.TransportTypeName);
	}

	[Fact]
	public async Task CreateWithCustomOptions()
	{
		var adapter = new InMemoryTransportAdapter(
			NullLogger<InMemoryTransportAdapter>.Instance,
			new InMemoryTransportOptions { Name = "custom-adapter", ChannelCapacity = 500 });

		adapter.Name.ShouldBe("custom-adapter");

		await adapter.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public void NotBeRunningInitially()
	{
		_sut.IsRunning.ShouldBeFalse();
	}

	[Fact]
	public void HaveEmptySentMessagesInitially()
	{
		_sut.SentMessages.Count.ShouldBe(0);
	}

	[Fact]
	public async Task ThrowForNullTransportMessageOnReceive()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.ReceiveAsync(null!, A.Fake<IDispatcher>(), CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ThrowForNullDispatcherOnReceive()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.ReceiveAsync(new object(), null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ReturnFailedResultWhenNotRunning()
	{
		var message = A.Fake<IDispatchMessage>();
		var result = await _sut.ReceiveAsync(message, A.Fake<IDispatcher>(), CancellationToken.None).ConfigureAwait(false);

		result.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public async Task ReturnFailedResultForInvalidMessageType()
	{
		// Start the adapter first
		await _sut.StartAsync(CancellationToken.None).ConfigureAwait(false);

		// Pass a non-IDispatchMessage object
		var result = await _sut.ReceiveAsync("not-a-message", A.Fake<IDispatcher>(), CancellationToken.None).ConfigureAwait(false);

		result.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public async Task StartSuccessfully()
	{
		await _sut.StartAsync(CancellationToken.None).ConfigureAwait(false);
		_sut.IsRunning.ShouldBeTrue();
	}

	[Fact]
	public async Task StopSuccessfully()
	{
		await _sut.StartAsync(CancellationToken.None).ConfigureAwait(false);
		await _sut.StopAsync(CancellationToken.None).ConfigureAwait(false);
		_sut.IsRunning.ShouldBeFalse();
	}

	[Fact]
	public void ImplementITransportAdapter()
	{
		_sut.ShouldBeAssignableTo<ITransportAdapter>();
	}

	[Fact]
	public void ImplementITransportHealthChecker()
	{
		_sut.ShouldBeAssignableTo<ITransportHealthChecker>();
	}

	[Fact]
	public void ImplementIAsyncDisposable()
	{
		_sut.ShouldBeAssignableTo<IAsyncDisposable>();
	}

	[Fact]
	public void HaveConstantDefaultName()
	{
		InMemoryTransportAdapter.DefaultName.ShouldBe("InMemory");
	}

	[Fact]
	public void HaveConstantTransportTypeName()
	{
		InMemoryTransportAdapter.TransportTypeName.ShouldBe("inmemory");
	}

	public async ValueTask DisposeAsync() => await _sut.DisposeAsync().ConfigureAwait(false);
}
