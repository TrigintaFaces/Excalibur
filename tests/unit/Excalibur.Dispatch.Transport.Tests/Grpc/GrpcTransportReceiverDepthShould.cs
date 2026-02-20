// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Transport.Grpc;

using Grpc.Net.Client;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.Grpc;

/// <summary>
/// Depth coverage tests for <see cref="GrpcTransportReceiver"/> covering
/// MapToReceivedMessage mapping, interface compliance, Source property,
/// and edge cases for acknowledge/reject method path construction.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class GrpcTransportReceiverDepthShould : IAsyncDisposable
{
	private readonly GrpcChannel _channel;
	private readonly IOptions<GrpcTransportOptions> _options;

	public GrpcTransportReceiverDepthShould()
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
	public void ImplementITransportReceiver()
	{
		// Arrange
		var receiver = new GrpcTransportReceiver(_channel, _options, NullLogger<GrpcTransportReceiver>.Instance);

		// Assert
		receiver.ShouldBeAssignableTo<ITransportReceiver>();
	}

	[Fact]
	public void ImplementIAsyncDisposable()
	{
		// Arrange
		var receiver = new GrpcTransportReceiver(_channel, _options, NullLogger<GrpcTransportReceiver>.Instance);

		// Assert
		receiver.ShouldBeAssignableTo<IAsyncDisposable>();
	}

	[Fact]
	public void BeInternalAndSealed()
	{
		typeof(GrpcTransportReceiver).IsNotPublic.ShouldBeTrue();
		typeof(GrpcTransportReceiver).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void ExposeSourceFromDestinationOption()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new GrpcTransportOptions
		{
			ServerAddress = "https://localhost:5001",
			Destination = "custom-source",
		});
		var receiver = new GrpcTransportReceiver(_channel, options, NullLogger<GrpcTransportReceiver>.Instance);

		// Assert — Source property returns options.Destination
		receiver.Source.ShouldBe("custom-source");
	}

	[Fact]
	public void ReturnNullFromGetService_ForNonGrpcChannelTypes()
	{
		// Arrange
		var receiver = new GrpcTransportReceiver(_channel, _options, NullLogger<GrpcTransportReceiver>.Instance);

		// Assert
		receiver.GetService(typeof(ITransportReceiver)).ShouldBeNull();
		receiver.GetService(typeof(string)).ShouldBeNull();
	}

	[Fact]
	public void MapToReceivedMessage_ConvertsAllFields()
	{
		// Arrange — call private static MapToReceivedMessage via reflection
		var mapMethod = typeof(GrpcTransportReceiver)
			.GetMethod("MapToReceivedMessage", BindingFlags.NonPublic | BindingFlags.Static);
		mapMethod.ShouldNotBeNull();

		var grpcMsg = new GrpcReceivedMessage
		{
			Id = "recv-123",
			Body = Convert.ToBase64String(new byte[] { 10, 20, 30 }),
			ContentType = "application/octet-stream",
			MessageType = "OrderEvent",
			CorrelationId = "corr-789",
			Subject = "orders",
			DeliveryCount = 3,
			Source = "queue-1",
			Properties = new Dictionary<string, string> { ["pk"] = "pv" },
			ProviderData = new Dictionary<string, string> { ["dk"] = "dv" },
		};

		// Act
		var result = (TransportReceivedMessage)mapMethod.Invoke(null, [grpcMsg])!;

		// Assert
		result.Id.ShouldBe("recv-123");
		result.Body.ToArray().ShouldBe(new byte[] { 10, 20, 30 });
		result.ContentType.ShouldBe("application/octet-stream");
		result.MessageType.ShouldBe("OrderEvent");
		result.CorrelationId.ShouldBe("corr-789");
		result.Subject.ShouldBe("orders");
		result.DeliveryCount.ShouldBe(3);
		result.Source.ShouldBe("queue-1");
		result.Properties.ShouldContainKey("pk");
		result.ProviderData.ShouldContainKey("dk");
	}

	[Fact]
	public void MapToReceivedMessage_SetsEnqueuedAtToUtcNow()
	{
		// Arrange
		var mapMethod = typeof(GrpcTransportReceiver)
			.GetMethod("MapToReceivedMessage", BindingFlags.NonPublic | BindingFlags.Static);
		mapMethod.ShouldNotBeNull();

		var grpcMsg = new GrpcReceivedMessage
		{
			Id = "time-test",
			Body = Convert.ToBase64String(Array.Empty<byte>()),
		};

		var before = DateTimeOffset.UtcNow;

		// Act
		var result = (TransportReceivedMessage)mapMethod.Invoke(null, [grpcMsg])!;

		// Assert — EnqueuedAt should be approximately now
		result.EnqueuedAt.ShouldBeGreaterThanOrEqualTo(before);
		result.EnqueuedAt.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow.AddSeconds(1));
	}

	[Fact]
	public void MapToReceivedMessage_HandlesEmptyPropertiesAndProviderData()
	{
		// Arrange
		var mapMethod = typeof(GrpcTransportReceiver)
			.GetMethod("MapToReceivedMessage", BindingFlags.NonPublic | BindingFlags.Static);
		mapMethod.ShouldNotBeNull();

		var grpcMsg = new GrpcReceivedMessage
		{
			Id = "empty-props",
			Body = Convert.ToBase64String(Array.Empty<byte>()),
			Properties = [],
			ProviderData = [],
		};

		// Act
		var result = (TransportReceivedMessage)mapMethod.Invoke(null, [grpcMsg])!;

		// Assert
		result.Properties.ShouldNotBeNull();
		result.Properties.ShouldBeEmpty();
		result.ProviderData.ShouldNotBeNull();
		result.ProviderData.ShouldBeEmpty();
	}

	[Fact]
	public void MapToReceivedMessage_DecodesBase64Body()
	{
		// Arrange
		var mapMethod = typeof(GrpcTransportReceiver)
			.GetMethod("MapToReceivedMessage", BindingFlags.NonPublic | BindingFlags.Static);
		mapMethod.ShouldNotBeNull();

		var originalBody = System.Text.Encoding.UTF8.GetBytes("Hello, gRPC!");
		var grpcMsg = new GrpcReceivedMessage
		{
			Id = "decode-test",
			Body = Convert.ToBase64String(originalBody),
		};

		// Act
		var result = (TransportReceivedMessage)mapMethod.Invoke(null, [grpcMsg])!;

		// Assert
		result.Body.ToArray().ShouldBe(originalBody);
	}

	[Fact]
	public void MapToReceivedMessage_HandlesMultipleProperties()
	{
		// Arrange
		var mapMethod = typeof(GrpcTransportReceiver)
			.GetMethod("MapToReceivedMessage", BindingFlags.NonPublic | BindingFlags.Static);
		mapMethod.ShouldNotBeNull();

		var grpcMsg = new GrpcReceivedMessage
		{
			Id = "multi-props",
			Body = Convert.ToBase64String(Array.Empty<byte>()),
			Properties = new Dictionary<string, string>
			{
				["key1"] = "val1",
				["key2"] = "val2",
				["key3"] = "val3",
			},
			ProviderData = new Dictionary<string, string>
			{
				["pkey1"] = "pval1",
				["pkey2"] = "pval2",
			},
		};

		// Act
		var result = (TransportReceivedMessage)mapMethod.Invoke(null, [grpcMsg])!;

		// Assert
		result.Properties.Count.ShouldBe(3);
		((string)result.Properties["key1"]).ShouldBe("val1");
		result.ProviderData.Count.ShouldBe(2);
		((string)result.ProviderData["pkey1"]).ShouldBe("pval1");
	}

	public async ValueTask DisposeAsync()
	{
		_channel.Dispose();
		await ValueTask.CompletedTask;
	}
}
