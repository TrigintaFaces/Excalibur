// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Resilience;
using Excalibur.Data.Firestore;

using Grpc.Core;

using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.Firestore;

/// <summary>
/// Unit tests for <see cref="FirestoreRetryPolicy"/> retry/backoff behavior.
/// The class is internal, so we test it via the public RetryPolicy property on
/// <see cref="FirestorePersistenceProvider"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class FirestoreRetryPolicyShould : UnitTestBase
{
	private readonly IDataRequestRetryPolicy _retryPolicy;

	public FirestoreRetryPolicyShould()
	{
		var options = Options.Create(new FirestoreOptions { ProjectId = "test" });
		var logger = A.Fake<ILogger<FirestorePersistenceProvider>>();
		var provider = new FirestorePersistenceProvider(options, logger);
		_retryPolicy = provider.RetryPolicy;
	}

	#region Configuration

	[Fact]
	public void MaxRetryAttempts_Returns3()
	{
		_retryPolicy.MaxRetryAttempts.ShouldBe(3);
	}

	[Fact]
	public void BaseRetryDelay_Returns100Milliseconds()
	{
		_retryPolicy.BaseRetryDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
	}

	#endregion Configuration

	#region ShouldRetry — Transient gRPC status codes

	[Theory]
	[InlineData(StatusCode.Unavailable)]
	[InlineData(StatusCode.DeadlineExceeded)]
	[InlineData(StatusCode.Aborted)]
	[InlineData(StatusCode.ResourceExhausted)]
	[InlineData(StatusCode.Internal)]
	public void ShouldRetry_WithTransientRpcException_ReturnsTrue(StatusCode statusCode)
	{
		var exception = new RpcException(new Status(statusCode, "Transient error"));

		_retryPolicy.ShouldRetry(exception).ShouldBeTrue();
	}

	#endregion ShouldRetry — Transient gRPC status codes

	#region ShouldRetry — Non-transient gRPC status codes

	[Theory]
	[InlineData(StatusCode.NotFound)]
	[InlineData(StatusCode.PermissionDenied)]
	[InlineData(StatusCode.InvalidArgument)]
	[InlineData(StatusCode.AlreadyExists)]
	[InlineData(StatusCode.Unauthenticated)]
	[InlineData(StatusCode.FailedPrecondition)]
	[InlineData(StatusCode.Unimplemented)]
	public void ShouldRetry_WithNonTransientRpcException_ReturnsFalse(StatusCode statusCode)
	{
		var exception = new RpcException(new Status(statusCode, "Non-transient error"));

		_retryPolicy.ShouldRetry(exception).ShouldBeFalse();
	}

	#endregion ShouldRetry — Non-transient gRPC status codes

	#region ShouldRetry — Other exception types

	[Fact]
	public void ShouldRetry_WithHttpRequestException_ReturnsTrue()
	{
		var exception = new HttpRequestException("Network error");

		_retryPolicy.ShouldRetry(exception).ShouldBeTrue();
	}

	[Fact]
	public void ShouldRetry_WithTaskCanceledException_ReturnsFalse()
	{
		var exception = new TaskCanceledException("Cancelled");

		_retryPolicy.ShouldRetry(exception).ShouldBeFalse();
	}

	[Fact]
	public void ShouldRetry_WithOperationCanceledException_ReturnsFalse()
	{
		var exception = new OperationCanceledException("Cancelled");

		_retryPolicy.ShouldRetry(exception).ShouldBeFalse();
	}

	[Fact]
	public void ShouldRetry_WithGenericException_ReturnsFalse()
	{
		var exception = new InvalidOperationException("Some error");

		_retryPolicy.ShouldRetry(exception).ShouldBeFalse();
	}

	#endregion ShouldRetry — Other exception types

	#region Singleton

	[Fact]
	public void Instance_ReturnsSameInstance()
	{
		var options = Options.Create(new FirestoreOptions { ProjectId = "test" });
		var logger = A.Fake<ILogger<FirestorePersistenceProvider>>();

		var provider1 = new FirestorePersistenceProvider(options, logger);
		var provider2 = new FirestorePersistenceProvider(options, logger);

		provider1.RetryPolicy.ShouldBeSameAs(provider2.RetryPolicy);
	}

	#endregion Singleton
}
