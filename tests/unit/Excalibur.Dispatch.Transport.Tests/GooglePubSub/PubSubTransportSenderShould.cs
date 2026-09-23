// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Google;
using Excalibur.Dispatch.Transport.GooglePubSub.Internal;

using Google.Api.Gax.Grpc;
using Google.Cloud.PubSub.V1;

using Grpc.Core;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub;

/// <summary>
/// Unit tests for <see cref="PubSubTransportSender"/>.
/// Validates constructor validation, destination exposure, and <c>GetService(Type)</c>.
/// </summary>
/// <remarks>
/// This class had no dedicated unit test file at all before this lock (bd-s1cprr). The GetService arm
/// pins the "hand out the native SDK seam" contract -- production already implements it correctly;
/// without this lock a regression to the base default would silently decline the
/// <see cref="IPublisherClientSeam"/> capability.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class PubSubTransportSenderShould : IAsyncDisposable
{
	private const string TestDestination = "projects/test-project/topics/orders-topic";
	private readonly IPublisherClientSeam _fakeClient;
	private readonly PubSubTransportSender _sut;

	public PubSubTransportSenderShould()
	{
		_fakeClient = A.Fake<IPublisherClientSeam>();
		_sut = new PubSubTransportSender(
			_fakeClient,
			TestDestination,
			NullLogger<PubSubTransportSender>.Instance);
	}

	public ValueTask DisposeAsync() => _sut.DisposeAsync();

	[Fact]
	public void Expose_destination_from_constructor()
	{
		_sut.Destination.ShouldBe(TestDestination);
	}

	[Fact]
	public void Throw_when_client_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PubSubTransportSender((IPublisherClientSeam)null!, TestDestination, NullLogger<PubSubTransportSender>.Instance));
	}

	[Fact]
	public void Throw_when_destination_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PubSubTransportSender(A.Fake<IPublisherClientSeam>(), null!, NullLogger<PubSubTransportSender>.Instance));
	}

	[Fact]
	public void Throw_when_logger_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PubSubTransportSender(A.Fake<IPublisherClientSeam>(), TestDestination, null!));
	}

	[Fact]
	public void Return_client_seam_via_GetService()
	{
		var result = _sut.GetService(typeof(IPublisherClientSeam));
		result.ShouldBe(_fakeClient);
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

	#region Publish failures are classified by their provider status

	// Every publish failure used to be reported with IsRetryable = false, which is SendError's default.
	// A caller honouring that field stopped retrying work the provider documents as recoverable: a
	// gRPC UNAVAILABLE that escaped the SDK's own retry budget became a permanent error.

	[Theory]
	[InlineData(StatusCode.Unavailable)]
	[InlineData(StatusCode.DeadlineExceeded)]
	[InlineData(StatusCode.Internal)]
	[InlineData(StatusCode.ResourceExhausted)]
	[InlineData(StatusCode.Cancelled)]
	public async Task Classify_a_transient_publish_failure_as_retryable(StatusCode statusCode)
	{
		// Arrange
		FailPublishWith(new RpcException(new Status(statusCode, "transient")));

		// Act
		var single = await _sut.SendAsync(Message("msg-1"), CancellationToken.None);
		var batch = await _sut.SendBatchAsync([Message("msg-1"), Message("msg-2")], CancellationToken.None);

		// Assert
		single.IsSuccess.ShouldBeFalse();
		single.Error!.IsRetryable.ShouldBeTrue();
		batch.Results.ShouldAllBe(r => r.Error!.IsRetryable);
	}

	[Theory]
	[InlineData(StatusCode.InvalidArgument)]
	[InlineData(StatusCode.PermissionDenied)]
	[InlineData(StatusCode.Unauthenticated)]
	[InlineData(StatusCode.NotFound)]
	[InlineData(StatusCode.FailedPrecondition)]
	[InlineData(StatusCode.AlreadyExists)]
	public async Task Classify_a_permanent_publish_failure_as_nonretryable(StatusCode statusCode)
	{
		// Safety control: retrying these cannot change the outcome, so a caller must not loop on them.
		FailPublishWith(new RpcException(new Status(statusCode, "permanent")));

		// Act
		var single = await _sut.SendAsync(Message("msg-1"), CancellationToken.None);
		var batch = await _sut.SendBatchAsync([Message("msg-1"), Message("msg-2")], CancellationToken.None);

		// Assert
		single.Error!.IsRetryable.ShouldBeFalse();
		batch.Results.ShouldAllBe(r => !r.Error!.IsRetryable);
	}

	[Fact]
	public async Task Classify_a_transient_failure_wrapped_in_another_exception_as_retryable()
	{
		// The SDK's retry layer can surface the status wrapped, so the classifier reads the chain.
		FailPublishWith(new InvalidOperationException(
			"publish failed",
			new RpcException(new Status(StatusCode.Unavailable, "transient"))));

		// Act
		var result = await _sut.SendAsync(Message("msg-1"), CancellationToken.None);

		// Assert
		result.Error!.IsRetryable.ShouldBeTrue();
	}

	[Fact]
	public async Task Classify_a_caller_requested_cancellation_as_nonretryable()
	{
		// Cancellation the caller asked for is not a broker failure, and reporting it retryable would
		// invite a retry loop against a token that is still cancelled.
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();
		FailPublishWith(new RpcException(new Status(StatusCode.Cancelled, "cancelled")));

		// Act
		var result = await _sut.SendAsync(Message("msg-1"), cts.Token);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.Error!.IsRetryable.ShouldBeFalse();
	}

	[Fact]
	public async Task Retain_the_exception_and_code_alongside_the_classification()
	{
		// Classification must not cost the diagnostics a caller needs to understand the failure.
		var thrown = new RpcException(new Status(StatusCode.Unavailable, "backend unavailable"));
		FailPublishWith(thrown);

		// Act
		var result = await _sut.SendAsync(Message("msg-1"), CancellationToken.None);

		// Assert
		result.Error!.Exception.ShouldBeSameAs(thrown);
		result.Error.Code.ShouldBe(nameof(RpcException));
		result.Error.Message.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task Let_a_caller_retry_a_transient_failure_and_then_succeed()
	{
		// Liveness: the classification is only useful if acting on it works. A caller that retries the
		// retryable failure must be able to complete the send, and the retry is the caller's, not an
		// unbounded automatic one inside the transport.
		var attempts = 0;
		A.CallTo(() => _fakeClient.PublishAsync(A<PublishRequest>._, A<CallSettings>._))
			.ReturnsLazily(() =>
			{
				attempts++;
				return attempts == 1
					? throw new RpcException(new Status(StatusCode.Unavailable, "transient"))
					: Task.FromResult(new PublishResponse { MessageIds = { "broker-1" } });
			});

		// Act
		var first = await _sut.SendAsync(Message("msg-1"), CancellationToken.None);
		first.Error!.IsRetryable.ShouldBeTrue();

		var second = await _sut.SendAsync(Message("msg-1"), CancellationToken.None);

		// Assert
		second.IsSuccess.ShouldBeTrue();
		second.MessageId.ShouldBe("broker-1");
		attempts.ShouldBe(2, "the transport must not retry on the caller's behalf");
	}

	#endregion

	#region Helpers

	private static TransportMessage Message(string id) =>
		new() { Id = id, Body = "payload"u8.ToArray(), ContentType = "application/json" };

	private void FailPublishWith(Exception exception) =>
		A.CallTo(() => _fakeClient.PublishAsync(A<PublishRequest>._, A<CallSettings>._))
			.ThrowsAsync(exception);

	#endregion
}
