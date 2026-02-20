// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly — FakeItEasy .Returns() stores ValueTask

using Amazon.SQS;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs;

/// <summary>
/// Unit tests for <see cref="SqsTransportSubscriber"/>.
/// Validates constructor validation, source property, GetService, disposal, and interface implementation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class SqsTransportSubscriberShould : IAsyncDisposable
{
	private const string TestSource = "orders-queue";
	private const string TestQueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/orders-queue";
	private readonly IAmazonSQS _fakeSqsClient;
	private readonly SqsTransportSubscriber _sut;

	public SqsTransportSubscriberShould()
	{
		_fakeSqsClient = A.Fake<IAmazonSQS>();
		_sut = new SqsTransportSubscriber(
			_fakeSqsClient,
			TestSource,
			TestQueueUrl,
			NullLogger<SqsTransportSubscriber>.Instance);
	}

	[Fact]
	public void Expose_source_from_constructor()
	{
		_sut.Source.ShouldBe(TestSource);
	}

	[Fact]
	public void Throw_when_sqsClient_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new SqsTransportSubscriber(null!, TestSource, TestQueueUrl, NullLogger<SqsTransportSubscriber>.Instance));
	}

	[Fact]
	public void Throw_when_source_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new SqsTransportSubscriber(A.Fake<IAmazonSQS>(), null!, TestQueueUrl, NullLogger<SqsTransportSubscriber>.Instance));
	}

	[Fact]
	public void Throw_when_queueUrl_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new SqsTransportSubscriber(A.Fake<IAmazonSQS>(), TestSource, null!, NullLogger<SqsTransportSubscriber>.Instance));
	}

	[Fact]
	public void Throw_when_logger_is_null()
	{
		Should.Throw<ArgumentNullException>(() =>
			new SqsTransportSubscriber(A.Fake<IAmazonSQS>(), TestSource, TestQueueUrl, null!));
	}

	[Fact]
	public async Task Throw_when_handler_is_null()
	{
		using var cts = new CancellationTokenSource();

		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.SubscribeAsync(null!, cts.Token));
	}

	[Fact]
	public void Return_sqs_client_via_GetService()
	{
		var result = _sut.GetService(typeof(IAmazonSQS));
		result.ShouldBe(_fakeSqsClient);
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

	[Fact]
	public async Task Complete_without_throwing_on_DisposeAsync()
	{
		// Should not throw
		await _sut.DisposeAsync();
	}

	[Fact]
	public async Task Be_idempotent_on_multiple_DisposeAsync_calls()
	{
		// Both calls should complete without throwing
		await _sut.DisposeAsync();
		await _sut.DisposeAsync();
	}

	[Fact]
	public void Implement_ITransportSubscriber()
	{
		var subscriber = _sut as ITransportSubscriber;
		subscriber.ShouldNotBeNull();
		subscriber.Source.ShouldBe(TestSource);
	}

	[Fact]
	public void Implement_IAsyncDisposable()
	{
		var disposable = _sut as IAsyncDisposable;
		disposable.ShouldNotBeNull();
	}

	public async ValueTask DisposeAsync()
	{
		await _sut.DisposeAsync();
	}
}
