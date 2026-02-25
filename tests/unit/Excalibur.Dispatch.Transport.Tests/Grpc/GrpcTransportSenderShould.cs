// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Grpc;

using Grpc.Net.Client;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.Grpc;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class GrpcTransportSenderShould : IAsyncDisposable
{
	private readonly GrpcChannel _channel;
	private readonly IOptions<GrpcTransportOptions> _options;

	public GrpcTransportSenderShould()
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
			new GrpcTransportSender(null!, _options, NullLogger<GrpcTransportSender>.Instance));
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new GrpcTransportSender(_channel, null!, NullLogger<GrpcTransportSender>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new GrpcTransportSender(_channel, _options, null!));
	}

	[Fact]
	public void ExposeDestinationFromOptions()
	{
		// Arrange
		var sender = new GrpcTransportSender(_channel, _options, NullLogger<GrpcTransportSender>.Instance);

		// Act & Assert
		sender.Destination.ShouldBe("test-destination");
	}

	[Fact]
	public void ReturnGrpcChannelFromGetService()
	{
		// Arrange
		var sender = new GrpcTransportSender(_channel, _options, NullLogger<GrpcTransportSender>.Instance);

		// Act
		var service = sender.GetService(typeof(GrpcChannel));

		// Assert
		service.ShouldBeSameAs(_channel);
	}

	[Fact]
	public void ReturnNullFromGetServiceForUnknownType()
	{
		// Arrange
		var sender = new GrpcTransportSender(_channel, _options, NullLogger<GrpcTransportSender>.Instance);

		// Act
		var service = sender.GetService(typeof(string));

		// Assert
		service.ShouldBeNull();
	}

	[Fact]
	public void ThrowWhenGetServiceTypeIsNull()
	{
		// Arrange
		var sender = new GrpcTransportSender(_channel, _options, NullLogger<GrpcTransportSender>.Instance);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => sender.GetService(null!));
	}

	[Fact]
	public async Task FlushAsyncCompletesImmediately()
	{
		// Arrange
		var sender = new GrpcTransportSender(_channel, _options, NullLogger<GrpcTransportSender>.Instance);

		// Act & Assert - should complete immediately since gRPC doesn't buffer
		await sender.FlushAsync(CancellationToken.None);
	}

	[Fact]
	public async Task ThrowWhenSendAsyncMessageIsNull()
	{
		// Arrange
		var sender = new GrpcTransportSender(_channel, _options, NullLogger<GrpcTransportSender>.Instance);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			sender.SendAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenSendBatchAsyncMessagesIsNull()
	{
		// Arrange
		var sender = new GrpcTransportSender(_channel, _options, NullLogger<GrpcTransportSender>.Instance);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			sender.SendBatchAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ReturnEmptyBatchResultForEmptyList()
	{
		// Arrange
		var sender = new GrpcTransportSender(_channel, _options, NullLogger<GrpcTransportSender>.Instance);

		// Act
		var result = await sender.SendBatchAsync(Array.Empty<TransportMessage>(), CancellationToken.None);

		// Assert
		result.TotalMessages.ShouldBe(0);
		result.SuccessCount.ShouldBe(0);
		result.FailureCount.ShouldBe(0);
	}

	[Fact]
	public async Task DisposeAsyncIdempotently()
	{
		// Arrange
		var channel = GrpcChannel.ForAddress("https://localhost:5001");
		var sender = new GrpcTransportSender(channel, _options, NullLogger<GrpcTransportSender>.Instance);

		// Act - dispose twice should not throw
		await sender.DisposeAsync();
		await sender.DisposeAsync();
	}

	public async ValueTask DisposeAsync()
	{
		_channel.Dispose();
		await ValueTask.CompletedTask;
	}
}
