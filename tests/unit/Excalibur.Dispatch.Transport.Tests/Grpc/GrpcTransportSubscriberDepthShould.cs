// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Transport.Grpc;

using Grpc.Net.Client;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.Grpc;

/// <summary>
/// Depth coverage tests for <see cref="GrpcTransportSubscriber"/> covering
/// MapToReceivedMessage mapping, interface compliance, Source property,
/// dispose idempotency, and type verification.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class GrpcTransportSubscriberDepthShould : IAsyncDisposable
{
	private readonly GrpcChannel _channel;
	private readonly IOptions<GrpcTransportOptions> _options;

	public GrpcTransportSubscriberDepthShould()
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
	public void ImplementITransportSubscriber()
	{
		// Arrange
		var subscriber = new GrpcTransportSubscriber(_channel, _options, NullLogger<GrpcTransportSubscriber>.Instance);

		// Assert
		subscriber.ShouldBeAssignableTo<ITransportSubscriber>();
	}

	[Fact]
	public void ImplementIAsyncDisposable()
	{
		// Arrange
		var subscriber = new GrpcTransportSubscriber(_channel, _options, NullLogger<GrpcTransportSubscriber>.Instance);

		// Assert
		subscriber.ShouldBeAssignableTo<IAsyncDisposable>();
	}

	[Fact]
	public void BeInternalAndSealed()
	{
		typeof(GrpcTransportSubscriber).IsNotPublic.ShouldBeTrue();
		typeof(GrpcTransportSubscriber).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void ExposeSourceFromDestinationOption()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new GrpcTransportOptions
		{
			ServerAddress = "https://localhost:5001",
			Destination = "subscriber-source",
		});
		var subscriber = new GrpcTransportSubscriber(_channel, options, NullLogger<GrpcTransportSubscriber>.Instance);

		// Assert
		subscriber.Source.ShouldBe("subscriber-source");
	}

	[Fact]
	public void ReturnNullFromGetService_ForNonGrpcChannelTypes()
	{
		// Arrange
		var subscriber = new GrpcTransportSubscriber(_channel, _options, NullLogger<GrpcTransportSubscriber>.Instance);

		// Assert
		subscriber.GetService(typeof(ITransportSubscriber)).ShouldBeNull();
		subscriber.GetService(typeof(int)).ShouldBeNull();
	}

	[Fact]
	public void MapToReceivedMessage_ConvertsAllFields()
	{
		// Arrange — call private static MapToReceivedMessage via reflection
		var mapMethod = typeof(GrpcTransportSubscriber)
			.GetMethod("MapToReceivedMessage", BindingFlags.NonPublic | BindingFlags.Static);
		mapMethod.ShouldNotBeNull();

		var grpcMsg = new GrpcReceivedMessage
		{
			Id = "sub-recv-1",
			Body = Convert.ToBase64String(new byte[] { 5, 6, 7, 8 }),
			ContentType = "application/xml",
			MessageType = "PaymentEvent",
			CorrelationId = "corr-sub-1",
			Subject = "payments",
			DeliveryCount = 1,
			Source = "payment-stream",
			Properties = new Dictionary<string, string> { ["x-tag"] = "urgent" },
			ProviderData = new Dictionary<string, string> { ["sequence"] = "42" },
		};

		// Act
		var result = (TransportReceivedMessage)mapMethod.Invoke(null, [grpcMsg])!;

		// Assert
		result.Id.ShouldBe("sub-recv-1");
		result.Body.ToArray().ShouldBe(new byte[] { 5, 6, 7, 8 });
		result.ContentType.ShouldBe("application/xml");
		result.MessageType.ShouldBe("PaymentEvent");
		result.CorrelationId.ShouldBe("corr-sub-1");
		result.Subject.ShouldBe("payments");
		result.DeliveryCount.ShouldBe(1);
		result.Source.ShouldBe("payment-stream");
		((string)result.Properties["x-tag"]).ShouldBe("urgent");
		((string)result.ProviderData["sequence"]).ShouldBe("42");
	}

	[Fact]
	public void MapToReceivedMessage_SetsEnqueuedAtToUtcNow()
	{
		// Arrange
		var mapMethod = typeof(GrpcTransportSubscriber)
			.GetMethod("MapToReceivedMessage", BindingFlags.NonPublic | BindingFlags.Static);
		mapMethod.ShouldNotBeNull();

		var grpcMsg = new GrpcReceivedMessage
		{
			Id = "time-sub",
			Body = Convert.ToBase64String(Array.Empty<byte>()),
		};
		var before = DateTimeOffset.UtcNow;

		// Act
		var result = (TransportReceivedMessage)mapMethod.Invoke(null, [grpcMsg])!;

		// Assert
		result.EnqueuedAt.ShouldBeGreaterThanOrEqualTo(before);
		var assertionUpperBound1 = DateTimeOffset.UtcNow.AddSeconds(1);
		result.EnqueuedAt.ShouldBeLessThanOrEqualTo(assertionUpperBound1);
	}

	[Fact]
	public void MapToReceivedMessage_HandlesNullOptionalFields()
	{
		// Arrange
		var mapMethod = typeof(GrpcTransportSubscriber)
			.GetMethod("MapToReceivedMessage", BindingFlags.NonPublic | BindingFlags.Static);
		mapMethod.ShouldNotBeNull();

		var grpcMsg = new GrpcReceivedMessage
		{
			Id = "null-opts",
			Body = Convert.ToBase64String(Array.Empty<byte>()),
			ContentType = null,
			MessageType = null,
			CorrelationId = null,
			Subject = null,
			Source = null,
			Properties = [],
			ProviderData = [],
		};

		// Act
		var result = (TransportReceivedMessage)mapMethod.Invoke(null, [grpcMsg])!;

		// Assert
		result.ContentType.ShouldBeNull();
		result.MessageType.ShouldBeNull();
		result.CorrelationId.ShouldBeNull();
		result.Subject.ShouldBeNull();
		result.Source.ShouldBeNull();
	}

	[Fact]
	public async Task DisposeAsync_SetsDisposedFlag()
	{
		// Arrange
		var channel = GrpcChannel.ForAddress("https://localhost:5001");
		var subscriber = new GrpcTransportSubscriber(channel, _options, NullLogger<GrpcTransportSubscriber>.Instance);

		// Act
		await subscriber.DisposeAsync();

		// Assert — verify via reflection that _disposed is true
		var disposedField = typeof(GrpcTransportSubscriber)
			.GetField("_disposed", BindingFlags.NonPublic | BindingFlags.Instance);
		disposedField.ShouldNotBeNull();
		((bool)disposedField.GetValue(subscriber)!).ShouldBeTrue();
	}

	public async ValueTask DisposeAsync()
	{
		_channel.Dispose();
		await ValueTask.CompletedTask;
	}
}
