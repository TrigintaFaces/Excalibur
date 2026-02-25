// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Grpc;

using Grpc.Net.Client;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.Grpc;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class GrpcTransportReceiverShould : IAsyncDisposable
{
	private readonly GrpcChannel _channel;
	private readonly IOptions<GrpcTransportOptions> _options;

	public GrpcTransportReceiverShould()
	{
		_channel = GrpcChannel.ForAddress("https://localhost:5001");
		_options = Microsoft.Extensions.Options.Options.Create(new GrpcTransportOptions
		{
			ServerAddress = "https://localhost:5001",
			Destination = "test-destination",
			DeadlineSeconds = 10,
		});
	}

	[Fact]
	public void ThrowWhenChannelIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new GrpcTransportReceiver(null!, _options, NullLogger<GrpcTransportReceiver>.Instance));
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new GrpcTransportReceiver(_channel, null!, NullLogger<GrpcTransportReceiver>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new GrpcTransportReceiver(_channel, _options, null!));
	}

	[Fact]
	public void ExposeSourceFromOptions()
	{
		// Arrange
		var receiver = new GrpcTransportReceiver(_channel, _options, NullLogger<GrpcTransportReceiver>.Instance);

		// Act & Assert
		receiver.Source.ShouldBe("test-destination");
	}

	[Fact]
	public void ReturnGrpcChannelFromGetService()
	{
		// Arrange
		var receiver = new GrpcTransportReceiver(_channel, _options, NullLogger<GrpcTransportReceiver>.Instance);

		// Act
		var service = receiver.GetService(typeof(GrpcChannel));

		// Assert
		service.ShouldBeSameAs(_channel);
	}

	[Fact]
	public void ReturnNullFromGetServiceForUnknownType()
	{
		// Arrange
		var receiver = new GrpcTransportReceiver(_channel, _options, NullLogger<GrpcTransportReceiver>.Instance);

		// Act
		var service = receiver.GetService(typeof(string));

		// Assert
		service.ShouldBeNull();
	}

	[Fact]
	public void ThrowWhenGetServiceTypeIsNull()
	{
		// Arrange
		var receiver = new GrpcTransportReceiver(_channel, _options, NullLogger<GrpcTransportReceiver>.Instance);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => receiver.GetService(null!));
	}

	[Fact]
	public async Task DisposeAsyncIdempotently()
	{
		// Arrange
		var channel = GrpcChannel.ForAddress("https://localhost:5001");
		var receiver = new GrpcTransportReceiver(channel, _options, NullLogger<GrpcTransportReceiver>.Instance);

		// Act - dispose twice should not throw
		await receiver.DisposeAsync();
		await receiver.DisposeAsync();
	}

	[Fact]
	public async Task ThrowWhenAcknowledgeMessageIsNull()
	{
		// Arrange
		var receiver = new GrpcTransportReceiver(_channel, _options, NullLogger<GrpcTransportReceiver>.Instance);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			receiver.AcknowledgeAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenRejectMessageIsNull()
	{
		// Arrange
		var receiver = new GrpcTransportReceiver(_channel, _options, NullLogger<GrpcTransportReceiver>.Instance);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			receiver.RejectAsync(null!, "reason", false, CancellationToken.None));
	}

	public async ValueTask DisposeAsync()
	{
		_channel.Dispose();
		await ValueTask.CompletedTask;
	}
}
