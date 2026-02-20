// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Grpc;

using Grpc.Net.Client;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.Grpc;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class GrpcTransportSubscriberShould : IAsyncDisposable
{
	private readonly GrpcChannel _channel;
	private readonly IOptions<GrpcTransportOptions> _options;

	public GrpcTransportSubscriberShould()
	{
		_channel = GrpcChannel.ForAddress("https://localhost:5001");
		_options = Microsoft.Extensions.Options.Options.Create(new GrpcTransportOptions
		{
			ServerAddress = "https://localhost:5001",
			Destination = "test-source",
			DeadlineSeconds = 10,
		});
	}

	[Fact]
	public void ThrowWhenChannelIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new GrpcTransportSubscriber(null!, _options, NullLogger<GrpcTransportSubscriber>.Instance));
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new GrpcTransportSubscriber(_channel, null!, NullLogger<GrpcTransportSubscriber>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new GrpcTransportSubscriber(_channel, _options, null!));
	}

	[Fact]
	public void ExposeSourceFromOptions()
	{
		// Arrange
		var subscriber = new GrpcTransportSubscriber(_channel, _options, NullLogger<GrpcTransportSubscriber>.Instance);

		// Act & Assert
		subscriber.Source.ShouldBe("test-source");
	}

	[Fact]
	public void ReturnGrpcChannelFromGetService()
	{
		// Arrange
		var subscriber = new GrpcTransportSubscriber(_channel, _options, NullLogger<GrpcTransportSubscriber>.Instance);

		// Act
		var service = subscriber.GetService(typeof(GrpcChannel));

		// Assert
		service.ShouldBeSameAs(_channel);
	}

	[Fact]
	public void ReturnNullFromGetServiceForUnknownType()
	{
		// Arrange
		var subscriber = new GrpcTransportSubscriber(_channel, _options, NullLogger<GrpcTransportSubscriber>.Instance);

		// Act
		var service = subscriber.GetService(typeof(string));

		// Assert
		service.ShouldBeNull();
	}

	[Fact]
	public void ThrowWhenGetServiceTypeIsNull()
	{
		// Arrange
		var subscriber = new GrpcTransportSubscriber(_channel, _options, NullLogger<GrpcTransportSubscriber>.Instance);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => subscriber.GetService(null!));
	}

	[Fact]
	public async Task ThrowWhenSubscribeHandlerIsNull()
	{
		// Arrange
		var subscriber = new GrpcTransportSubscriber(_channel, _options, NullLogger<GrpcTransportSubscriber>.Instance);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			subscriber.SubscribeAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task DisposeAsyncIdempotently()
	{
		// Arrange
		var channel = GrpcChannel.ForAddress("https://localhost:5001");
		var subscriber = new GrpcTransportSubscriber(channel, _options, NullLogger<GrpcTransportSubscriber>.Instance);

		// Act - dispose twice should not throw
		await subscriber.DisposeAsync();
		await subscriber.DisposeAsync();
	}

	public async ValueTask DisposeAsync()
	{
		_channel.Dispose();
		await ValueTask.CompletedTask;
	}
}
