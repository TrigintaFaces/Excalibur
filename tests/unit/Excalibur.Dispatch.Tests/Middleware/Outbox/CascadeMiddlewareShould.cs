// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Middleware.Outbox;

using Microsoft.Extensions.Logging.Testing;

using Tests.Shared.TestDoubles;

namespace Excalibur.Dispatch.Tests.Middleware.Outbox;

/// <summary>
/// Verifies that the counts <see cref="CascadeMiddleware"/> logs describe what it actually did.
/// The emitted count is an operator's only signal for how much traffic a cascade produced, so a
/// raw list length silently overstates it exactly when the handler result is malformed.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Middleware)]
public sealed class CascadeMiddlewareShould : UnitTestBase
{
	private readonly FakeLogger<CascadeMiddleware> _logger = new();

	[Fact]
	public async Task LogTheStagedCountExcludingNullCascadeEntries()
	{
		// Arrange: a malformed handler result -- 3 dispatchable messages interleaved with 2 nulls.
		var first = A.Fake<IDispatchMessage>();
		var second = A.Fake<IDispatchMessage>();
		var third = A.Fake<IDispatchMessage>();
		var cascade = new TestCascade([first, null!, second, null!, third]);
		var outbox = NewOutboxContext();
		var context = NewContext(outbox);

		// Act
		_ = await new CascadeMiddleware(_logger)
			.InvokeAsync(A.Fake<IDispatchMessage>(), context, NextReturning(cascade), CancellationToken.None)
			.ConfigureAwait(false);

		// Assert: the log reports 3, not the raw list length of 5.
		StagedCount().ShouldBe(3);

		// Liveness (a): the three non-null messages really were staged, and nothing else was.
		outbox.OutboundMessages.Select(m => m.Message).ShouldBe([first, second, third]);
	}

	[Fact]
	public async Task LogEveryMessageWhenNoCascadeEntryIsNull()
	{
		// Liveness (b): a middleware that always logged 0, or stopped logging, would pass the test
		// above and fail this one.
		var messages = new IDispatchMessage[] { A.Fake<IDispatchMessage>(), A.Fake<IDispatchMessage>() };
		var outbox = NewOutboxContext();
		var context = NewContext(outbox);

		_ = await new CascadeMiddleware(_logger)
			.InvokeAsync(A.Fake<IDispatchMessage>(), context, NextReturning(new TestCascade(messages)), CancellationToken.None)
			.ConfigureAwait(false);

		StagedCount().ShouldBe(2);
		outbox.OutboundMessages.Count.ShouldBe(2);
	}

	[Fact]
	public async Task ReportOnlyDispatchableMessagesOnTheNoOutboxWarning()
	{
		// The no-outbox warning tells the operator how much went undispatched. A null entry was
		// never dispatchable, so counting it would overstate the loss -- the same defect as above.
		var cascade = new TestCascade([A.Fake<IDispatchMessage>(), null!, A.Fake<IDispatchMessage>(), null!]);
		var context = new TestMessageContext { MessageId = Guid.NewGuid().ToString(), MessageType = "TestMessage" };

		_ = await new CascadeMiddleware(_logger)
			.InvokeAsync(A.Fake<IDispatchMessage>(), context, NextReturning(cascade), CancellationToken.None)
			.ConfigureAwait(false);

		CountLoggedFor(MiddlewareEventId.CascadeWithoutOutbox).ShouldBe(2);
	}

	private static OutboxContext NewOutboxContext() => new(null, null, null, "TestMessage");

	private static TestMessageContext NewContext(OutboxContext outbox)
	{
		var context = new TestMessageContext { MessageId = Guid.NewGuid().ToString(), MessageType = "TestMessage" };
		context.SetItem("OutboxContext", outbox);
		return context;
	}

	private static DispatchRequestDelegate NextReturning(ICascade cascade) =>
		(_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success(cascade));

	private int StagedCount() => CountLoggedFor(MiddlewareEventId.CascadeStaged);

	private int CountLoggedFor(int eventId)
	{
		var record = _logger.Collector.GetSnapshot().Single(r => r.Id.Id == eventId);
		var count = record.StructuredState!.Single(kv => kv.Key == "Count").Value;
		return int.Parse(count!, System.Globalization.CultureInfo.InvariantCulture);
	}

	private sealed class TestCascade(IReadOnlyList<IDispatchMessage> messages) : ICascade
	{
		public IReadOnlyList<IDispatchMessage> Messages { get; } = messages;
	}
}
