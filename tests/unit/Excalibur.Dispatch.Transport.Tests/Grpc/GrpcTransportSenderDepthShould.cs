// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Transport.Grpc;

using Grpc.Core;
using Grpc.Net.Client;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.Grpc;

/// <summary>
/// Depth coverage tests for <see cref="GrpcTransportSender"/> covering
/// MapToRequest mapping, IsTransient logic, interface compliance,
/// batch with empty list edge case, and Destination routing.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class GrpcTransportSenderDepthShould : IAsyncDisposable
{
	private readonly GrpcChannel _channel;
	private readonly IOptions<GrpcTransportOptions> _options;

	public GrpcTransportSenderDepthShould()
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
	public void ImplementITransportSender()
	{
		// Arrange
		var sender = new GrpcTransportSender(_channel, _options, NullLogger<GrpcTransportSender>.Instance);

		// Assert
		sender.ShouldBeAssignableTo<ITransportSender>();
	}

	[Fact]
	public void ImplementIAsyncDisposable()
	{
		// Arrange
		var sender = new GrpcTransportSender(_channel, _options, NullLogger<GrpcTransportSender>.Instance);

		// Assert
		sender.ShouldBeAssignableTo<IAsyncDisposable>();
	}

	[Fact]
	public void BeInternalAndSealed()
	{
		// Assert
		typeof(GrpcTransportSender).IsNotPublic.ShouldBeTrue();
		typeof(GrpcTransportSender).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void ExposeDestinationFromCustomOptions()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new GrpcTransportOptions
		{
			ServerAddress = "https://localhost:5001",
			Destination = "my-custom-dest",
		});
		var sender = new GrpcTransportSender(_channel, options, NullLogger<GrpcTransportSender>.Instance);

		// Assert
		sender.Destination.ShouldBe("my-custom-dest");
	}

	[Fact]
	public void ReturnNullForNonGrpcChannelServiceType()
	{
		// Arrange
		var sender = new GrpcTransportSender(_channel, _options, NullLogger<GrpcTransportSender>.Instance);

		// Act & Assert
		sender.GetService(typeof(ITransportSender)).ShouldBeNull();
		sender.GetService(typeof(int)).ShouldBeNull();
		sender.GetService(typeof(GrpcTransportOptions)).ShouldBeNull();
	}

	[Fact]
	public async Task ReturnEmptyBatchResult_WithZeroCounts_ForEmptyList()
	{
		// Arrange
		var sender = new GrpcTransportSender(_channel, _options, NullLogger<GrpcTransportSender>.Instance);

		// Act
		var result = await sender.SendBatchAsync([], CancellationToken.None);

		// Assert — empty batch returns immediately without gRPC call
		result.TotalMessages.ShouldBe(0);
		result.SuccessCount.ShouldBe(0);
		result.FailureCount.ShouldBe(0);
	}

	[Fact]
	public async Task FlushAsync_CompleteSynchronously()
	{
		// Arrange
		var sender = new GrpcTransportSender(_channel, _options, NullLogger<GrpcTransportSender>.Instance);

		// Act — FlushAsync returns Task.CompletedTask since gRPC doesn't buffer
		var task = sender.FlushAsync(CancellationToken.None);

		// Assert
		task.IsCompleted.ShouldBeTrue();
		await task; // should not throw
	}

	[Fact]
	public void MapToRequest_HandlesMessageWithProperties()
	{
		// Arrange — use reflection to call private static MapToRequest
		var mapMethod = typeof(GrpcTransportSender)
			.GetMethod("MapToRequest", BindingFlags.NonPublic | BindingFlags.Static);
		mapMethod.ShouldNotBeNull();

		var message = new TransportMessage
		{
			Id = "msg-123",
			Body = new byte[] { 1, 2, 3 },
			ContentType = "application/json",
			MessageType = "TestEvent",
			CorrelationId = "corr-456",
			Subject = "test-subject",
		};

		// Act
		var result = (GrpcTransportRequest)mapMethod.Invoke(null, [message])!;

		// Assert
		result.Id.ShouldBe("msg-123");
		result.Body.ShouldBe(Convert.ToBase64String(new byte[] { 1, 2, 3 }));
		result.ContentType.ShouldBe("application/json");
		result.MessageType.ShouldBe("TestEvent");
		result.CorrelationId.ShouldBe("corr-456");
		result.Subject.ShouldBe("test-subject");
	}

	[Fact]
	public void MapToRequest_ConvertsBodyToBase64()
	{
		// Arrange
		var mapMethod = typeof(GrpcTransportSender)
			.GetMethod("MapToRequest", BindingFlags.NonPublic | BindingFlags.Static);
		mapMethod.ShouldNotBeNull();

		var body = new byte[] { 72, 101, 108, 108, 111 }; // "Hello"
		var message = new TransportMessage { Id = "base64-test", Body = body };

		// Act
		var result = (GrpcTransportRequest)mapMethod.Invoke(null, [message])!;

		// Assert — Base64 of "Hello" = "SGVsbG8="
		result.Body.ShouldBe("SGVsbG8=");
	}

	[Fact]
	public void MapToRequest_ExtractsDestinationFromProperties()
	{
		// Arrange
		var mapMethod = typeof(GrpcTransportSender)
			.GetMethod("MapToRequest", BindingFlags.NonPublic | BindingFlags.Static);
		mapMethod.ShouldNotBeNull();

		var message = new TransportMessage { Id = "dest-test", Body = Array.Empty<byte>() };
		message.Properties["dispatch.destination"] = "target-queue";
		message.Properties["other.key"] = "other-value";

		// Act
		var result = (GrpcTransportRequest)mapMethod.Invoke(null, [message])!;

		// Assert — destination extracted from properties
		result.Destination.ShouldBe("target-queue");
	}

	[Fact]
	public void MapToRequest_FiltersNonStringProperties()
	{
		// Arrange
		var mapMethod = typeof(GrpcTransportSender)
			.GetMethod("MapToRequest", BindingFlags.NonPublic | BindingFlags.Static);
		mapMethod.ShouldNotBeNull();

		var message = new TransportMessage { Id = "filter-test", Body = Array.Empty<byte>() };
		message.Properties["string-key"] = "string-value";
		message.Properties["int-key"] = 42;
		message.Properties["bool-key"] = true;

		// Act
		var result = (GrpcTransportRequest)mapMethod.Invoke(null, [message])!;

		// Assert — only string values should be in the request properties
		result.Properties.ShouldContainKey("string-key");
		result.Properties.ShouldNotContainKey("int-key");
		result.Properties.ShouldNotContainKey("bool-key");
	}

	[Fact]
	public void IsTransient_ReturnsTrueForUnavailable()
	{
		// Arrange — use reflection to call private static IsTransient
		var method = typeof(GrpcTransportSender)
			.GetMethod("IsTransient", BindingFlags.NonPublic | BindingFlags.Static);
		method.ShouldNotBeNull();

		// Act & Assert
		((bool)method.Invoke(null, [new RpcException(new Status(StatusCode.Unavailable, ""))])!).ShouldBeTrue();
	}

	[Fact]
	public void IsTransient_ReturnsTrueForDeadlineExceeded()
	{
		// Arrange
		var method = typeof(GrpcTransportSender)
			.GetMethod("IsTransient", BindingFlags.NonPublic | BindingFlags.Static);
		method.ShouldNotBeNull();

		// Act & Assert
		((bool)method.Invoke(null, [new RpcException(new Status(StatusCode.DeadlineExceeded, ""))])!).ShouldBeTrue();
	}

	[Fact]
	public void IsTransient_ReturnsTrueForAborted()
	{
		// Arrange
		var method = typeof(GrpcTransportSender)
			.GetMethod("IsTransient", BindingFlags.NonPublic | BindingFlags.Static);
		method.ShouldNotBeNull();

		// Act & Assert
		((bool)method.Invoke(null, [new RpcException(new Status(StatusCode.Aborted, ""))])!).ShouldBeTrue();
	}

	[Fact]
	public void IsTransient_ReturnsFalseForNotFound()
	{
		// Arrange
		var method = typeof(GrpcTransportSender)
			.GetMethod("IsTransient", BindingFlags.NonPublic | BindingFlags.Static);
		method.ShouldNotBeNull();

		// Act & Assert
		((bool)method.Invoke(null, [new RpcException(new Status(StatusCode.NotFound, ""))])!).ShouldBeFalse();
	}

	[Fact]
	public void IsTransient_ReturnsFalseForPermissionDenied()
	{
		// Arrange
		var method = typeof(GrpcTransportSender)
			.GetMethod("IsTransient", BindingFlags.NonPublic | BindingFlags.Static);
		method.ShouldNotBeNull();

		// Act & Assert
		((bool)method.Invoke(null, [new RpcException(new Status(StatusCode.PermissionDenied, ""))])!).ShouldBeFalse();
	}

	[Fact]
	public void IsTransient_ReturnsFalseForInvalidArgument()
	{
		// Arrange
		var method = typeof(GrpcTransportSender)
			.GetMethod("IsTransient", BindingFlags.NonPublic | BindingFlags.Static);
		method.ShouldNotBeNull();

		// Act & Assert
		((bool)method.Invoke(null, [new RpcException(new Status(StatusCode.InvalidArgument, ""))])!).ShouldBeFalse();
	}

	[Fact]
	public void CreateCallOptions_SetsDeadlineFromOptions()
	{
		// Arrange — use reflection to call private instance method
		var method = typeof(GrpcTransportSender)
			.GetMethod("CreateCallOptions", BindingFlags.NonPublic | BindingFlags.Instance);
		method.ShouldNotBeNull();

		var sender = new GrpcTransportSender(_channel, _options, NullLogger<GrpcTransportSender>.Instance);
		var before = DateTime.UtcNow;

		// Act
		var callOptions = (CallOptions)method.Invoke(sender, [CancellationToken.None])!;

		// Assert — deadline should be ~10 seconds from now (DeadlineSeconds = 10)
		callOptions.Deadline.ShouldNotBeNull();
		var deadline = callOptions.Deadline!.Value;
		deadline.ShouldBeGreaterThanOrEqualTo(before.AddSeconds(9));
		var assertionUpperBound = DateTime.UtcNow.AddSeconds(11);
		deadline.ShouldBeLessThanOrEqualTo(assertionUpperBound);
	}

	public async ValueTask DisposeAsync()
	{
		_channel.Dispose();
		await ValueTask.CompletedTask;
	}
}
