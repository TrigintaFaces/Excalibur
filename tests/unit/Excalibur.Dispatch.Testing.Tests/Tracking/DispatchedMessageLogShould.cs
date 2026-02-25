// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Testing.Tracking;

namespace Excalibur.Dispatch.Testing.Tests.Tracking;

[Trait("Category", "Unit")]
[Trait("Component", "Excalibur.Dispatch.Testing")]
public sealed class DispatchedMessageLogShould
{
	[Fact]
	public void StartEmpty()
	{
		var log = new DispatchedMessageLog();
		log.Count.ShouldBe(0);
		log.All.ShouldBeEmpty();
	}

	[Fact]
	public void RecordSingleMessage()
	{
		var log = new DispatchedMessageLog();
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var result = A.Fake<IMessageResult>();
		var record = new DispatchedMessage(message, context, DateTimeOffset.UtcNow, result);

		log.Record(record);

		log.Count.ShouldBe(1);
		log.All.ShouldHaveSingleItem();
		log.All[0].Message.ShouldBeSameAs(message);
	}

	[Fact]
	public void RecordMultipleMessages()
	{
		var log = new DispatchedMessageLog();
		var msg1 = new DispatchedMessage(A.Fake<IDispatchMessage>(), A.Fake<IMessageContext>(), DateTimeOffset.UtcNow, null);
		var msg2 = new DispatchedMessage(A.Fake<IDispatchMessage>(), A.Fake<IMessageContext>(), DateTimeOffset.UtcNow, null);

		log.Record(msg1);
		log.Record(msg2);

		log.Count.ShouldBe(2);
	}

	[Fact]
	public void SelectByMessageType()
	{
		var log = new DispatchedMessageLog();
		var testMsg = new TestMessage();
		var otherMsg = new OtherMessage();

		log.Record(new DispatchedMessage(testMsg, A.Fake<IMessageContext>(), DateTimeOffset.UtcNow, null));
		log.Record(new DispatchedMessage(otherMsg, A.Fake<IMessageContext>(), DateTimeOffset.UtcNow, null));

		var selected = log.Select<TestMessage>();
		selected.Count.ShouldBe(1);
		selected[0].Message.ShouldBeSameAs(testMsg);
	}

	[Fact]
	public void ReturnTrueForAnyWhenTypeExists()
	{
		var log = new DispatchedMessageLog();
		log.Record(new DispatchedMessage(new TestMessage(), A.Fake<IMessageContext>(), DateTimeOffset.UtcNow, null));

		log.Any<TestMessage>().ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseForAnyWhenTypeDoesNotExist()
	{
		var log = new DispatchedMessageLog();
		log.Record(new DispatchedMessage(new TestMessage(), A.Fake<IMessageContext>(), DateTimeOffset.UtcNow, null));

		log.Any<OtherMessage>().ShouldBeFalse();
	}

	[Fact]
	public void ReturnEmptyForSelectWhenNoMatchingType()
	{
		var log = new DispatchedMessageLog();
		log.Record(new DispatchedMessage(new TestMessage(), A.Fake<IMessageContext>(), DateTimeOffset.UtcNow, null));

		log.Select<OtherMessage>().ShouldBeEmpty();
	}

	[Fact]
	public void ClearAllMessages()
	{
		var log = new DispatchedMessageLog();
		log.Record(new DispatchedMessage(new TestMessage(), A.Fake<IMessageContext>(), DateTimeOffset.UtcNow, null));
		log.Record(new DispatchedMessage(new OtherMessage(), A.Fake<IMessageContext>(), DateTimeOffset.UtcNow, null));

		log.Clear();

		log.Count.ShouldBe(0);
		log.All.ShouldBeEmpty();
		log.Any<TestMessage>().ShouldBeFalse();
	}

	[Fact]
	public async Task BeThreadSafe()
	{
		var log = new DispatchedMessageLog();
		const int count = 100;
		var tasks = new Task[count];

		for (var i = 0; i < count; i++)
		{
			tasks[i] = Task.Run(() =>
				log.Record(new DispatchedMessage(new TestMessage(), A.Fake<IMessageContext>(), DateTimeOffset.UtcNow, null)));
		}

		await Task.WhenAll(tasks);

		log.Count.ShouldBe(count);
	}

	private sealed class TestMessage : IDispatchMessage;
	private sealed class OtherMessage : IDispatchMessage;
}
