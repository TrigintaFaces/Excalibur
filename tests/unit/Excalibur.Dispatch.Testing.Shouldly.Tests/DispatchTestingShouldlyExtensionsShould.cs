// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Testing;
using Excalibur.Dispatch.Testing.Tracking;
using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Testing.Shouldly.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Testing")]
public sealed class DispatchTestingShouldlyExtensionsShould
{
	// --- ShouldHaveDispatched<T> (pass) ---

	[Fact]
	public void ShouldHaveDispatched_Passes_WhenMessageExists()
	{
		var log = CreateLogWith(new TestMessage());

		log.ShouldHaveDispatched<TestMessage>();
	}

	// --- ShouldHaveDispatched<T>(count) (pass) ---

	[Fact]
	public void ShouldHaveDispatched_WithCount_Passes_WhenCountMatches()
	{
		var log = CreateLogWith(new TestMessage(), new TestMessage());

		log.ShouldHaveDispatched<TestMessage>(2);
	}

	// --- ShouldNotHaveDispatched<T> (pass) ---

	[Fact]
	public void ShouldNotHaveDispatched_Passes_WhenNoMessages()
	{
		var log = CreateLogWith(new TestMessage());

		log.ShouldNotHaveDispatched<OtherMessage>();
	}

	// --- ShouldHaveDispatchedCount (pass) ---

	[Fact]
	public void ShouldHaveDispatchedCount_Passes_WhenCountMatches()
	{
		var log = CreateLogWith(new TestMessage(), new OtherMessage());

		log.ShouldHaveDispatchedCount(2);
	}

	// --- ShouldHaveSent (pass) ---

	[Fact]
	public async Task ShouldHaveSent_Passes_WhenCountMatches()
	{
		var sender = new InMemoryTransportSender("test-dest");
		await sender.SendAsync(TransportMessage.FromString("hello"), CancellationToken.None);
		await sender.SendAsync(TransportMessage.FromString("world"), CancellationToken.None);

		sender.ShouldHaveSent(2);

		await sender.DisposeAsync();
	}

	// --- ShouldHaveSentTo (pass) ---

	[Fact]
	public void ShouldHaveSentTo_Passes_WhenDestinationMatches()
	{
		var sender = new InMemoryTransportSender("orders-topic");

		sender.ShouldHaveSentTo("orders-topic");
	}

	// --- ShouldHaveSentMessageMatching (pass) ---

	[Fact]
	public async Task ShouldHaveSentMessageMatching_Passes_WhenPredicateMatches()
	{
		var sender = new InMemoryTransportSender("test-dest");
		var msg = TransportMessage.FromString("target");
		msg.Subject = "important";
		await sender.SendAsync(msg, CancellationToken.None);

		sender.ShouldHaveSentMessageMatching(m => m.Subject == "important");

		await sender.DisposeAsync();
	}

	// --- ShouldHaveAcknowledged (pass) ---

	[Fact]
	public async Task ShouldHaveAcknowledged_Passes_WhenCountMatches()
	{
		var receiver = new InMemoryTransportReceiver("test-source");
		await receiver.AcknowledgeAsync(new TransportReceivedMessage { Id = "msg-1" }, CancellationToken.None);

		receiver.ShouldHaveAcknowledged(1);

		await receiver.DisposeAsync();
	}

	// --- ShouldHaveRejected (pass) ---

	[Fact]
	public async Task ShouldHaveRejected_Passes_WhenCountMatches()
	{
		var receiver = new InMemoryTransportReceiver("test-source");
		await receiver.RejectAsync(
			new TransportReceivedMessage { Id = "msg-1" },
			"bad data", false, CancellationToken.None);

		receiver.ShouldHaveRejected(1);

		await receiver.DisposeAsync();
	}

	// --- ShouldHaveDispatched<T> (fail) ---

	[Fact]
	public void ShouldHaveDispatched_Throws_WhenNoMessages()
	{
		var log = new DispatchedMessageLog();

		Should.Throw<ShouldAssertException>(() => log.ShouldHaveDispatched<TestMessage>());
	}

	// --- ShouldNotHaveDispatched<T> (fail) ---

	[Fact]
	public void ShouldNotHaveDispatched_Throws_WhenMessagesExist()
	{
		var log = CreateLogWith(new TestMessage());

		Should.Throw<ShouldAssertException>(() => log.ShouldNotHaveDispatched<TestMessage>());
	}

	// --- ShouldHaveSent (fail) ---

	[Fact]
	public void ShouldHaveSent_Throws_WhenCountMismatch()
	{
		var sender = new InMemoryTransportSender("test-dest");

		Should.Throw<ShouldAssertException>(() => sender.ShouldHaveSent(1));
	}

	// --- ShouldHaveAcknowledged (fail) ---

	[Fact]
	public void ShouldHaveAcknowledged_Throws_WhenCountMismatch()
	{
		var receiver = new InMemoryTransportReceiver("test-source");

		Should.Throw<ShouldAssertException>(() => receiver.ShouldHaveAcknowledged(1));
	}

	// --- ShouldHaveRejected (fail) ---

	[Fact]
	public void ShouldHaveRejected_Throws_WhenCountMismatch()
	{
		var receiver = new InMemoryTransportReceiver("test-source");

		Should.Throw<ShouldAssertException>(() => receiver.ShouldHaveRejected(1));
	}

	// --- Helpers ---

	private static DispatchedMessageLog CreateLogWith(params IDispatchMessage[] messages)
	{
		var log = new DispatchedMessageLog();

		foreach (var msg in messages)
		{
			log.Record(new DispatchedMessage(
				msg,
				A.Fake<IMessageContext>(),
				DateTimeOffset.UtcNow,
				null));
		}

		return log;
	}

	private sealed class TestMessage : IDispatchMessage;
	private sealed class OtherMessage : IDispatchMessage;
}
