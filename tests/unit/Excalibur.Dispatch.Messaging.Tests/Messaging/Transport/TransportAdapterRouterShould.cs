// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

/// <summary>
///     Tests for the <see cref="TransportAdapterRouter" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TransportAdapterRouterShould
{
	[Fact]
	public void ThrowForNullDispatcher() =>
		Should.Throw<ArgumentNullException>(
			() => new TransportAdapterRouter(null!, NullLogger<TransportAdapterRouter>.Instance));

	[Fact]
	public void ThrowForNullLogger() =>
		Should.Throw<ArgumentNullException>(
			() => new TransportAdapterRouter(A.Fake<IDispatcher>(), null!));

	[Fact]
	public void CreateSuccessfully()
	{
		var sut = new TransportAdapterRouter(A.Fake<IDispatcher>(), NullLogger<TransportAdapterRouter>.Instance);
		sut.ShouldNotBeNull();
	}

	[Fact]
	public async Task ThrowForNullMessageOnRoute()
	{
		var sut = new TransportAdapterRouter(A.Fake<IDispatcher>(), NullLogger<TransportAdapterRouter>.Instance);

		await Should.ThrowAsync<ArgumentNullException>(
			() => sut.RouteAsync(null!, A.Fake<IMessageContext>(), "adapter-1", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ThrowForNullContextOnRoute()
	{
		var sut = new TransportAdapterRouter(A.Fake<IDispatcher>(), NullLogger<TransportAdapterRouter>.Instance);

		await Should.ThrowAsync<ArgumentNullException>(
			() => sut.RouteAsync(A.Fake<IDispatchMessage>(), null!, "adapter-1", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ThrowForNullAdapterId()
	{
		var sut = new TransportAdapterRouter(A.Fake<IDispatcher>(), NullLogger<TransportAdapterRouter>.Instance);

		await Should.ThrowAsync<ArgumentException>(
			() => sut.RouteAsync(A.Fake<IDispatchMessage>(), A.Fake<IMessageContext>(), null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ThrowForEmptyAdapterId()
	{
		var sut = new TransportAdapterRouter(A.Fake<IDispatcher>(), NullLogger<TransportAdapterRouter>.Instance);

		await Should.ThrowAsync<ArgumentException>(
			() => sut.RouteAsync(A.Fake<IDispatchMessage>(), A.Fake<IMessageContext>(), string.Empty, CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task RegisterAdapterSuccessfully()
	{
		var sut = new TransportAdapterRouter(A.Fake<IDispatcher>(), NullLogger<TransportAdapterRouter>.Instance);
		var adapter = A.Fake<IMessageBusAdapter>();
		A.CallTo(() => adapter.Name).Returns("test-adapter");

		await Should.NotThrowAsync(
			() => sut.RegisterAdapterAsync(adapter, "test", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public void ImplementITransportAdapterRouter()
	{
		var sut = new TransportAdapterRouter(A.Fake<IDispatcher>(), NullLogger<TransportAdapterRouter>.Instance);
		sut.ShouldBeAssignableTo<ITransportAdapterRouter>();
	}
}
