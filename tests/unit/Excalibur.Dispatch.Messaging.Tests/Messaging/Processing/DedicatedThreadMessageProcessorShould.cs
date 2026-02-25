// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Pooling;
using Excalibur.Dispatch.Processing;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Processing;

[Trait("Category", TestCategories.Unit)]
[Trait("Component", TestComponents.Messaging)]
public sealed class DedicatedThreadMessageProcessorShould
{
	[Fact]
	public void ThrowForInvalidThreadCount()
	{
		var handler = new DelegateMessageHandler(static (_, _) => ProcessingResult.Ok());
		var bufferPool = new MessageBufferPool();
		var logger = NullLogger<DedicatedThreadMessageProcessor<TestMessage>>.Instance;

		_ = Should.Throw<ArgumentException>(() =>
			new DedicatedThreadMessageProcessor<TestMessage>(
				threadCount: 0,
				handler,
				bufferPool,
				logger));
	}

	[Fact]
	public void ReturnFalseWhenSubmittingBeforeStart()
	{
		using var sut = CreateProcessor(new DelegateMessageHandler(static (_, _) => ProcessingResult.Ok()));
		var message = new TestMessage(Value: 5);

		var accepted = sut.TrySubmit(in message, correlationId: 42);

		accepted.ShouldBeFalse();
	}

	[Fact]
	public void ThrowWhenStartingTwice()
	{
		using var sut = CreateProcessor(new DelegateMessageHandler(static (_, _) => ProcessingResult.Ok()));
		sut.Start();

		try
		{
			_ = Should.Throw<InvalidOperationException>(() => sut.Start());
		}
		finally
		{
			sut.Stop();
		}
	}

	[Fact]
	public async Task ProcessSubmittedMessagesAndReportStatistics()
	{
		var handled = 0;
		using var sut = CreateProcessor(new DelegateMessageHandler((_, _) =>
		{
			_ = Interlocked.Increment(ref handled);
			return ProcessingResult.Ok();
		}));

		sut.Start();
		var submitted = 0;
		for (var i = 0; i < 12; i++)
		{
			var message = new TestMessage(i);
			if (sut.TrySubmit(in message, correlationId: (ulong)i))
			{
				submitted++;
			}
		}

		var processed = await global::Tests.Shared.Infrastructure.WaitHelpers
			.WaitUntilAsync(() => Volatile.Read(ref handled) >= submitted, TimeSpan.FromSeconds(10))
			.ConfigureAwait(false);
		processed.ShouldBeTrue();

		sut.Stop();
		var stats = sut.GetStatistics();

		stats.TotalMessagesProcessed.ShouldBeGreaterThanOrEqualTo(submitted);
		stats.TotalErrors.ShouldBe(0);
	}

	[Fact]
	public async Task CountFailedHandlerResultsAsErrors()
	{
		var handled = 0;
		using var sut = CreateProcessor(new DelegateMessageHandler((_, _) =>
		{
			_ = Interlocked.Increment(ref handled);
			return ProcessingResult.Error(17);
		}));

		sut.Start();
		var submitted = 0;
		for (var i = 0; i < 6; i++)
		{
			var message = new TestMessage(i);
			if (sut.TrySubmit(in message))
			{
				submitted++;
			}
		}

		var processed = await global::Tests.Shared.Infrastructure.WaitHelpers
			.WaitUntilAsync(() => Volatile.Read(ref handled) >= submitted, TimeSpan.FromSeconds(10))
			.ConfigureAwait(false);
		processed.ShouldBeTrue();

		sut.Stop();
		var stats = sut.GetStatistics();

		stats.TotalMessagesProcessed.ShouldBe(0);
		stats.TotalErrors.ShouldBeGreaterThanOrEqualTo(submitted);
	}

	[Fact]
	public async Task CountThrownExceptionsAsErrors()
	{
		var handled = 0;
		using var sut = CreateProcessor(new DelegateMessageHandler((_, _) =>
		{
			_ = Interlocked.Increment(ref handled);
			throw new InvalidOperationException("boom");
		}));

		sut.Start();
		var submitted = 0;
		for (var i = 0; i < 4; i++)
		{
			var message = new TestMessage(i);
			if (sut.TrySubmit(in message))
			{
				submitted++;
			}
		}

		var attempted = await global::Tests.Shared.Infrastructure.WaitHelpers
			.WaitUntilAsync(() => Volatile.Read(ref handled) >= submitted, TimeSpan.FromSeconds(10))
			.ConfigureAwait(false);
		attempted.ShouldBeTrue();

		sut.Stop();
		var stats = sut.GetStatistics();
		stats.TotalErrors.ShouldBeGreaterThanOrEqualTo(submitted);
	}

	[Fact]
	public async Task SubmitBatchWhenRunningAndReturnZeroWhenStopped()
	{
		var handled = 0;
		using var sut = CreateProcessor(new DelegateMessageHandler((_, _) =>
		{
			_ = Interlocked.Increment(ref handled);
			return ProcessingResult.Ok();
		}));

		var messages = new[]
		{
			new TestMessage(1),
			new TestMessage(2),
			new TestMessage(3),
			new TestMessage(4),
		};

		sut.SubmitBatch(messages).ShouldBe(0);

		sut.Start();
		var submitted = sut.SubmitBatch(messages);
		submitted.ShouldBe(messages.Length);

		var processed = await global::Tests.Shared.Infrastructure.WaitHelpers
			.WaitUntilAsync(() => Volatile.Read(ref handled) >= submitted, TimeSpan.FromSeconds(10))
			.ConfigureAwait(false);
		processed.ShouldBeTrue();

		sut.Stop();
	}

	private static DedicatedThreadMessageProcessor<TestMessage> CreateProcessor(
		IMessageHandler<TestMessage> handler) =>
		new(
			threadCount: 1,
			handler,
			new MessageBufferPool(),
			NullLogger<DedicatedThreadMessageProcessor<TestMessage>>.Instance);

	private readonly record struct TestMessage(int Value);

	private sealed class DelegateMessageHandler(Func<TestMessage, ulong, ProcessingResult> callback) : IMessageHandler<TestMessage>
	{
		public ProcessingResult ProcessMessage(in TestMessage message, ulong correlationId, Span<byte> responseBuffer) =>
			callback(message, correlationId);
	}
}
