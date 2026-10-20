// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Testing.Tests.Transport;

/// <summary>
/// Concrete xUnit conformance tests for <see cref="InMemoryTransportReceiver"/>.
/// Demonstrates the conformance test kit pattern using the InMemory transport.
/// </summary>
[UnitTest]
[Trait("Category", "Unit")]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait(TraitNames.Pattern, "Regression")]
public sealed class InMemoryTransportReceiverConformanceShould : TransportReceiverConformanceTests
{
	private const string Source = "conformance-test-source";

	protected override Task<ITransportReceiver> CreateReceiverAsync() =>
		Task.FromResult<ITransportReceiver>(new InMemoryTransportReceiver(Source));

	protected override Task SeedMessagesAsync(
		ITransportReceiver receiver,
		IReadOnlyList<TransportReceivedMessage> messages)
	{
		var inMemory = (InMemoryTransportReceiver)receiver;

		foreach (var msg in messages)
		{
			inMemory.Enqueue(msg);
		}

		return Task.CompletedTask;
	}

	[Fact]
	public Task Source_IsNotEmpty() => VerifySourceIsNotEmpty();

	[Fact]
	public Task ReceiveAsync_ReturnsMessages() => VerifyReceiveReturnsMessages();

	[Fact]
	public Task ReceiveAsync_RespectsMaxMessages() => VerifyReceiveRespectsMaxMessages();

	[Fact]
	public Task ReceiveAsync_ReturnsEmptyWhenNone() => VerifyReceiveReturnsEmptyWhenNone();

	[Fact]
	public Task ReceiveAsync_RespectsCancellation() => VerifyReceiveRespectsCancellation();

	[Fact]
	public Task AcknowledgeAsync_Succeeds() => VerifyAcknowledgeSucceeds();

	[Fact]
	public Task AcknowledgeAsync_HasRecordedEffect() => VerifyAcknowledgeHasObservableEffect((receiver, message) =>
	{
		var inMemory = (InMemoryTransportReceiver)receiver;
		inMemory.AcknowledgedMessages.Count.ShouldBe(1);
		inMemory.AcknowledgedMessages[0].ShouldBeSameAs(message);
		inMemory.RejectedMessages.ShouldBeEmpty();
		return Task.CompletedTask;
	});

	[Fact]
	public Task AcknowledgeAsync_ThrowsOnNull() => VerifyAcknowledgeThrowsOnNull();

	[Fact]
	public Task RejectAsync_Succeeds() => VerifyRejectSucceeds();

	[Fact]
	public Task RejectAsync_WithRequeue_Succeeds() => VerifyRejectWithRequeueSucceeds();

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task RejectAsync_HasRecordedEffect(bool requeue) => VerifyRejectHasObservableEffect(requeue, (receiver, message) =>
	{
		var inMemory = (InMemoryTransportReceiver)receiver;
		inMemory.RejectedMessages.Count.ShouldBe(1);
		inMemory.RejectedMessages[0].Message.ShouldBeSameAs(message);
		inMemory.RejectedMessages[0].Reason.ShouldBe(requeue ? "retry" : "test-reason");
		inMemory.RejectedMessages[0].Requeue.ShouldBe(requeue);
		inMemory.AcknowledgedMessages.ShouldBeEmpty();
		return Task.CompletedTask;
	});

	[Fact]
	public Task RejectAsync_AcceptsNullReason() => VerifyRejectAcceptsNullReason();

	[Fact]
	public Task GetService_ReturnsNullForUnknownType() => VerifyGetServiceReturnsNullForUnknownType();

	[Fact]
	public Task DisposeAsync_IsIdempotent() => VerifyDisposeAsyncIsIdempotent();
}
