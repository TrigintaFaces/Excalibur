// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Text.Json;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Examples.ECommerceSample;
using Excalibur.Dispatch.Examples.ECommerceSample.Infrastructure;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Excalibur.ECommerceSample.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Regression")]
public sealed class NotificationDrainServiceShould
{
	[Fact]
	public async Task SendAndSettleExactlyOnceWithCapturedEvidence()
	{
		await using var harness = new Harness(Row("first"));
		await harness.RunCycleAsync();
		harness.Monitor.NotificationsSent.ShouldBe(1);
		harness.Monitor.NotificationSendRetries.ShouldBe(0);
		A.CallTo(() => harness.Sender.SendEmailAsync(A<EmailNotification>.That.Matches(message => message.ToEmail == "first@example.test"), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => harness.Completion.MarkSentAsync("first", "claim-first", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		harness.AssertNoWeakOrFailureWrites();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ChargeAGenuineDeliveryFailureIncludingIndependentCancellation(bool independentCancellation)
	{
		await using var harness = new Harness(Row("first", 4));
		Exception failure = independentCancellation ? new OperationCanceledException("independent cancellation") : new IOException("send failed");
		A.CallTo(() => harness.Sender.SendEmailAsync(A<EmailNotification>._, A<CancellationToken>._)).Returns(Task.FromException(failure));
		await harness.RunCycleAsync();
		harness.Monitor.NotificationsSent.ShouldBe(0);
		harness.Monitor.NotificationSendRetries.ShouldBe(1);
		A.CallTo(() => harness.Completion.MarkFailedAsync("first", failure.Message, 5, "claim-first", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => harness.Completion.MarkSentAsync(A<string>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
		harness.AssertNoWeakWrites();
	}

	[Fact]
	public async Task NeverReportADeliveryFailureAfterAcceptanceWhenSettlementThrows()
	{
		await using var harness = new Harness(Row("first"));
		A.CallTo(() => harness.Completion.MarkSentAsync("first", "claim-first", A<CancellationToken>._))
			.ReturnsLazily(() => ValueTask.FromException<OutboxCompletionOutcome>(new IOException("settlement failed")));
		await harness.RunCycleAsync();
		A.CallTo(() => harness.Sender.SendEmailAsync(A<EmailNotification>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		harness.Monitor.NotificationsSent.ShouldBe(0);
		harness.Monitor.NotificationSendRetries.ShouldBe(0);
		harness.Logs.ShouldContain(message => message.Contains("settlement failed", StringComparison.OrdinalIgnoreCase));
		harness.AssertNoWeakOrFailureWrites();
	}

	[Theory]
	[InlineData(OutboxCompletionOutcome.ClaimLost)]
	[InlineData(OutboxCompletionOutcome.Unknown)]
	public async Task DoNotCountARefusedFailureAsAPersistedRetry(OutboxCompletionOutcome refusal)
	{
		await using var harness = new Harness(Row("first"));
		A.CallTo(() => harness.Sender.SendEmailAsync(A<EmailNotification>._, A<CancellationToken>._)).Returns(Task.FromException(new IOException("send failed")));
		A.CallTo(() => harness.Completion.MarkFailedAsync("first", "send failed", 1, "claim-first", A<CancellationToken>._)).Returns(new ValueTask<OutboxCompletionOutcome>(refusal));
		await harness.RunCycleAsync();
		harness.Monitor.NotificationsSent.ShouldBe(0);
		harness.Monitor.NotificationSendRetries.ShouldBe(0);
		harness.Logs.ShouldContain(message => message.Contains(refusal.ToString(), StringComparison.Ordinal));
		A.CallTo(() => harness.Completion.MarkFailedAsync("first", "send failed", 1, "claim-first", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		harness.AssertNoWeakWrites();
	}

	[Theory]
	[InlineData(OutboxCompletionOutcome.ClaimLost)]
	[InlineData(OutboxCompletionOutcome.AlreadyTerminal)]
	[InlineData(OutboxCompletionOutcome.MessageNotFound)]
	[InlineData(OutboxCompletionOutcome.Unknown)]
	public async Task RefuseSettlementWithoutFallbackAndContinueAnIndependentRow(OutboxCompletionOutcome refusal)
	{
		await using var harness = new Harness(Row("first"), Row("second"));
		A.CallTo(() => harness.Completion.MarkSentAsync("first", "claim-first", A<CancellationToken>._)).Returns(new ValueTask<OutboxCompletionOutcome>(refusal));
		await harness.RunCycleAsync();
		harness.Monitor.NotificationsSent.ShouldBe(1);
		harness.Monitor.NotificationSendRetries.ShouldBe(0);
		harness.Logs.ShouldContain(message => message.Contains(refusal.ToString(), StringComparison.Ordinal));
		A.CallTo(() => harness.Completion.MarkSentAsync("second", "claim-second", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		harness.AssertNoWeakOrFailureWrites();
	}

	[Fact]
	public async Task StopFurtherAdmissionOnFenceRefused()
	{
		await using var harness = new Harness(Row("first"), Row("second"));
		A.CallTo(() => harness.Completion.MarkSentAsync("first", "claim-first", A<CancellationToken>._)).Returns(new ValueTask<OutboxCompletionOutcome>(OutboxCompletionOutcome.FenceRefused));
		await harness.RunCycleAsync();
		A.CallTo(() => harness.Sender.SendEmailAsync(A<EmailNotification>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		harness.Monitor.NotificationsSent.ShouldBe(0);
		harness.AssertNoWeakOrFailureWrites();
	}

	[Fact]
	public async Task FreezeTheEntireBatchBeforeTheFirstCallbackMutatesRows()
	{
		var first = Row("first", 2);
		var second = Row("second", 6);
		await using var harness = new Harness(first, second);
		A.CallTo(() => harness.Sender.SendEmailAsync(A<EmailNotification>.That.Matches(message => message.ToEmail == "first@example.test"), A<CancellationToken>._))
			.Invokes(() =>
			{
				foreach (var row in new[] { first, second })
				{
					row.Id = "forged"; row.DispatcherId = "forged-claim"; row.RetryCount = 100;
					Array.Fill(row.Payload, (byte)'!');
				}
			}).Returns(Task.CompletedTask);
		A.CallTo(() => harness.Sender.SendEmailAsync(A<EmailNotification>.That.Matches(message => message.ToEmail == "second@example.test"), A<CancellationToken>._)).Returns(Task.FromException(new IOException("second failed")));
		await harness.RunCycleAsync();
		A.CallTo(() => harness.Completion.MarkSentAsync("first", "claim-first", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => harness.Completion.MarkFailedAsync("second", "second failed", 7, "claim-second", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		harness.Monitor.NotificationsSent.ShouldBe(1);
		harness.Monitor.NotificationSendRetries.ShouldBe(1);
		harness.AssertNoWeakWrites();
	}

	[Theory]
	[InlineData(false, "returned")]
	[InlineData(false, "stale")]
	[InlineData(false, "unavailable")]
	[InlineData(true, "returned")]
	[InlineData(true, "stale")]
	[InlineData(true, "unavailable")]
	public async Task StopAdmissionsForReturnedOrThrownAuthorityRefusalAtEitherSettlementPhase(bool failedDelivery, string refusal)
	{
		await using var harness = new Harness(Row("first"), Row("second"));
		ValueTask<OutboxCompletionOutcome> Refuse() => refusal switch
		{
			"returned" => new ValueTask<OutboxCompletionOutcome>(OutboxCompletionOutcome.FenceRefused),
			"stale" => ValueTask.FromException<OutboxCompletionOutcome>(new StaleOutboxFencingTokenException("controlled stale authority")),
			"unavailable" => ValueTask.FromException<OutboxCompletionOutcome>(new OutboxFencingTokenUnavailableException("controlled unavailable authority")),
			_ => throw new ArgumentOutOfRangeException(nameof(refusal)),
		};
		if (failedDelivery)
		{
			A.CallTo(() => harness.Sender.SendEmailAsync(A<EmailNotification>._, A<CancellationToken>._)).Returns(Task.FromException(new IOException("send failed")));
			A.CallTo(() => harness.Completion.MarkFailedAsync("first", "send failed", 1, "claim-first", A<CancellationToken>._)).ReturnsLazily(Refuse);
		}
		else
		{
			A.CallTo(() => harness.Completion.MarkSentAsync("first", "claim-first", A<CancellationToken>._)).ReturnsLazily(Refuse);
		}
		await harness.RunCycleAsync();
		if (failedDelivery)
		{
			A.CallTo(() => harness.Completion.MarkFailedAsync("first", "send failed", 1, "claim-first", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
			A.CallTo(() => harness.Completion.MarkSentAsync(A<string>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
		}
		else
		{
			A.CallTo(() => harness.Completion.MarkSentAsync("first", "claim-first", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
			A.CallTo(() => harness.Completion.MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
		}
		A.CallTo(() => harness.Sender.SendEmailAsync(A<EmailNotification>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => harness.Sender.SendEmailAsync(A<EmailNotification>.That.Matches(message => message.ToEmail == "second@example.test"), A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => harness.Completion.MarkSentAsync("second", A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => harness.Completion.MarkFailedAsync("second", A<string>._, A<int>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
		harness.Monitor.NotificationsSent.ShouldBe(0);
		harness.Monitor.NotificationSendRetries.ShouldBe(0);
		if (refusal != "returned") { harness.Logs.ShouldContain(message => message.Contains("authority refused", StringComparison.Ordinal)); }
		harness.AssertNoWeakWrites();
	}

	[Fact]
	public async Task LeaveASuccessorClaimUnchangedWhenTheCapturedAcquisitionIsLost()
	{
		await using var harness = new Harness(Row("first"));
		A.CallTo(() => harness.Sender.SendEmailAsync(A<EmailNotification>._, A<CancellationToken>._))
			.Invokes(() => harness.LiveClaims["first"] = "successor-claim").Returns(Task.CompletedTask);
		await harness.RunCycleAsync();
		A.CallTo(() => harness.Completion.MarkSentAsync("first", "claim-first", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		harness.LiveClaims["first"].ShouldBe("successor-claim");
		harness.Monitor.NotificationsSent.ShouldBe(0);
		harness.AssertNoWeakOrFailureWrites();
	}

	[Fact]
	public async Task PreserveCallerCancellationWithoutChargingTheFailureBudget()
	{
		await using var harness = new Harness(Row("first"));
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		A.CallTo(() => harness.Sender.SendEmailAsync(A<EmailNotification>._, A<CancellationToken>._))
			.ReturnsLazily((EmailNotification _, CancellationToken token) => { entered.TrySetResult(); return Task.Delay(Timeout.Infinite, token); });
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		await harness.Worker.StartAsync(deadline.Token);
		await entered.Task.WaitAsync(deadline.Token);
		await harness.Worker.StopAsync(deadline.Token);
		harness.Monitor.NotificationsSent.ShouldBe(0);
		harness.Monitor.NotificationSendRetries.ShouldBe(0);
		A.CallTo(() => harness.Completion.MarkSentAsync(A<string>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
		harness.AssertNoWeakOrFailureWrites();
	}

	[Fact]
	public async Task RejectTheWholeMalformedBatchBeforeAnyDelivery()
	{
		var bad = Row("second"); bad.DispatcherId = " ";
		await using var harness = new Harness(Row("first"), bad);
		await harness.RunCycleAsync();
		A.CallTo(() => harness.Sender.SendEmailAsync(A<EmailNotification>._, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => harness.Completion.MarkSentAsync(A<string>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
		harness.AssertNoWeakOrFailureWrites();
	}

	[Fact]
	public void RequireRealAcquisitionCompletionBeforeAnyClaimOrDelivery()
	{
		var store = A.Fake<IOutboxStore>();
		var sender = A.Fake<IEmailSender>();
		Should.Throw<InvalidOperationException>(() => new NotificationDrainService(store, sender, new PerformanceMonitor(), NullLogger<NotificationDrainService>.Instance));
		A.CallTo(() => store.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => sender.SendEmailAsync(A<EmailNotification>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	private static OutboundMessage Row(string id, int retries = 0) => new(nameof(EmailNotification),
		JsonSerializer.SerializeToUtf8Bytes(new EmailNotification { ToEmail = id + "@example.test", Subject = "subject", Body = "body", NotificationType = "OrderConfirmation" }), "email-notifications")
	{ Id = id, DispatcherId = "claim-" + id, RetryCount = retries };

	private sealed class Harness : IAsyncDisposable
	{
		private readonly TaskCompletionSource _cycle = new(TaskCreationOptions.RunContinuationsAsynchronously);
		internal IOutboxStore Store { get; } = A.Fake<IOutboxStore>(options => options.Implements<IClaimScopedOutboxStore>());
		internal IClaimScopedOutboxStore Completion => (IClaimScopedOutboxStore)Store;
		internal IEmailSender Sender { get; } = A.Fake<IEmailSender>();
		internal PerformanceMonitor Monitor { get; } = new();
		internal ConcurrentDictionary<string, string> LiveClaims { get; } = new(StringComparer.Ordinal);
		internal ConcurrentQueue<string> Logs { get; } = new();
		internal NotificationDrainService Worker { get; }
		internal Harness(params OutboundMessage[] rows)
		{
			foreach (var row in rows) { LiveClaims[row.Id] = row.DispatcherId!; }
			A.CallTo(() => Store.GetService(typeof(IClaimScopedOutboxStore))).Returns(Store);
			var acquisitions = 0;
			A.CallTo(() => Store.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._)).ReturnsLazily(() =>
			{
				if (Interlocked.Increment(ref acquisitions) == 1) { return new ValueTask<IEnumerable<OutboundMessage>>(rows); }
				// Actual worker is serial: its next acquisition cannot occur while a prior callback or settlement is pending.
				_cycle.TrySetResult();
				return new ValueTask<IEnumerable<OutboundMessage>>([]);
			});
			A.CallTo(() => Sender.SendEmailAsync(A<EmailNotification>._, A<CancellationToken>._)).Returns(Task.CompletedTask);
			A.CallTo(() => Completion.MarkSentAsync(A<string>._, A<string>._, A<CancellationToken>._))
				.ReturnsLazily((string id, string claim, CancellationToken token) => Complete(id, claim, token));
			A.CallTo(() => Completion.MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<string>._, A<CancellationToken>._))
				.ReturnsLazily((string id, string _, int _, string claim, CancellationToken token) => Complete(id, claim, token));
			Worker = new NotificationDrainService(Store, Sender, Monitor, new CaptureLogger(Logs));
		}
		private ValueTask<OutboxCompletionOutcome> Complete(string id, string claim, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			return ValueTask.FromResult(LiveClaims.TryGetValue(id, out var owner) && string.Equals(owner, claim, StringComparison.Ordinal)
				? OutboxCompletionOutcome.Applied : OutboxCompletionOutcome.ClaimLost);
		}
		internal async Task RunCycleAsync()
		{
			using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
			await Worker.StartAsync(deadline.Token);
			await _cycle.Task.WaitAsync(deadline.Token);
			await Worker.StopAsync(deadline.Token);
		}
		internal void AssertNoWeakWrites()
		{
			A.CallTo(() => Store.MarkSentAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
			A.CallTo(() => Store.MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<CancellationToken>._)).MustNotHaveHappened();
			var acquisition = Fake.GetCalls(Store).First(call => call.Method.Name == nameof(IOutboxStore.GetUnsentMessagesAsync));
			var token = (CancellationToken)acquisition.Arguments[1]!;
			foreach (var call in Fake.GetCalls(Store).Where(call => call.Method.DeclaringType == typeof(IClaimScopedOutboxStore)))
			{
				((CancellationToken)call.Arguments.Last()!).ShouldBe(token);
			}
		}
		internal void AssertNoWeakOrFailureWrites()
		{
			AssertNoWeakWrites();
			A.CallTo(() => Completion.MarkFailedAsync(A<string>._, A<string>._, A<int>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
		}
		public async ValueTask DisposeAsync()
		{
			using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
			await Worker.StopAsync(deadline.Token);
			Worker.Dispose();
		}
	}

	private sealed class CaptureLogger(ConcurrentQueue<string> messages) : ILogger<NotificationDrainService>
	{
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
		public bool IsEnabled(LogLevel logLevel) => true;
		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => messages.Enqueue(formatter(state, exception));
	}
}
