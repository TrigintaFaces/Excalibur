// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Testing.Tests.Transport;

/// <summary>
/// Concrete xUnit conformance tests for <see cref="InMemoryTransportSubscriber"/>.
/// Demonstrates the conformance test kit pattern using the InMemory transport.
/// </summary>
[UnitTest]
public class InMemoryTransportSubscriberConformanceShould : TransportSubscriberConformanceTests
{
	private const string Source = "conformance-test-topic";

	protected override Task<ITransportSubscriber> CreateSubscriberAsync() =>
		Task.FromResult<ITransportSubscriber>(new InMemoryTransportSubscriber(Source));

	protected override async Task<MessageAction> PushMessageToSubscriberAsync(
		ITransportSubscriber subscriber,
		TransportReceivedMessage message,
		CancellationToken cancellationToken)
	{
		var inMemory = (InMemoryTransportSubscriber)subscriber;
		return await inMemory.PushAsync(message, cancellationToken).ConfigureAwait(false);
	}

	[Fact]
	public Task Source_IsNotEmpty() => VerifySourceIsNotEmpty();

	[Fact]
	public Task SubscribeAsync_StartsAndStops() => VerifySubscribeStartsAndStops();

	[Fact]
	public Task SubscribeAsync_ThrowsOnNullHandler() => VerifySubscribeThrowsOnNullHandler();

	[Fact]
	public Task Handler_ReceivesMessage_Acknowledge() => VerifyHandlerReceivesMessageAndAcknowledges();

	[Fact]
	public Task Handler_ReceivesMessage_Reject() => VerifyHandlerCanRejectMessage();

	[Fact]
	public Task Handler_ReceivesMessage_Requeue() => VerifyHandlerCanRequeueMessage();

	[Fact]
	public Task Handler_ReceivesMultipleMessages() => VerifyHandlerReceivesMultipleMessages();

	[Fact]
	public Task GetService_ReturnsNullForUnknownType() => VerifyGetServiceReturnsNullForUnknownType();

	[Fact]
	public Task DisposeAsync_IsIdempotent() => VerifyDisposeAsyncIsIdempotent();
}
