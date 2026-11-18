// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Threading.Channels;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Examples.ECommerceSample;
using Excalibur.Dispatch.Examples.ECommerceSample.Infrastructure;
using FakeItEasy;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

namespace Excalibur.ECommerceSample.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Regression")]
public sealed class WorkerPollingDelayShould
{
	[Theory]
	[InlineData("notifications", "success")]
	[InlineData("notifications", "failure")]
	[InlineData("notifications", "independent-cancellation")]
	[InlineData("inventory", "success")]
	[InlineData("inventory", "failure")]
	[InlineData("inventory", "independent-cancellation")]
	public async Task WaitExactlyOnePollingIntervalAfterEveryCompletedCycle(string worker, string outcome)
	{
		await using var harness = new Harness(worker, outcome);
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		await harness.Worker.StartAsync(deadline.Token);
		var first = await harness.Clock.Registrations.Reader.ReadAsync(deadline.Token);
		first.DueTime.ShouldBe(TimeSpan.FromMilliseconds(250));
		first.Period.ShouldBe(Timeout.InfiniteTimeSpan);
		harness.Reads.ShouldBe(1);
		harness.Clock.Advance(TimeSpan.FromMilliseconds(249));
		harness.Reads.ShouldBe(1);
		harness.Clock.Advance(TimeSpan.FromMilliseconds(1));
		await harness.SecondRead.Task.WaitAsync(deadline.Token);
		var second = await harness.Clock.Registrations.Reader.ReadAsync(deadline.Token);
		harness.Reads.ShouldBe(2);
		second.DueTime.ShouldBe(TimeSpan.FromMilliseconds(250));
		second.Period.ShouldBe(Timeout.InfiniteTimeSpan);
		second.CreatedAt.ShouldBe(first.CreatedAt.AddMilliseconds(250));
		harness.CycleErrors.ShouldBe(outcome == "success" ? 0 : 2);
		await harness.Worker.StopAsync(deadline.Token);
		var execution = harness.Worker.ExecuteTask.ShouldNotBeNull();
		execution.IsCompletedSuccessfully.ShouldBeTrue();
	}

	[Theory]
	[InlineData("notifications")]
	[InlineData("inventory")]
	public async Task InterruptAFailedCyclePollingDelayWithoutAdvancingTime(string worker)
	{
		await using var harness = new Harness(worker, "failure");
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		await harness.Worker.StartAsync(deadline.Token);
		_ = await harness.Clock.Registrations.Reader.ReadAsync(deadline.Token);
		var before = harness.Clock.GetUtcNow();
		harness.Reads.ShouldBe(1);
		harness.CycleErrors.ShouldBe(1);
		await harness.Worker.StopAsync(deadline.Token);
		var execution = harness.Worker.ExecuteTask.ShouldNotBeNull();
		execution.IsCompletedSuccessfully.ShouldBeTrue();
		harness.Clock.GetUtcNow().ShouldBe(before);
		harness.Reads.ShouldBe(1);
	}

	[Theory]
	[InlineData("notifications")]
	[InlineData("inventory")]
	public async Task StopDuringTheActualStoreReadWithoutStartingAPollTimer(string worker)
	{
		await using var harness = new Harness(worker, "pending");
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		await harness.Worker.StartAsync(deadline.Token);
		await harness.FirstRead.Task.WaitAsync(deadline.Token);
		var before = harness.Clock.GetUtcNow();
		await harness.Worker.StopAsync(deadline.Token);
		var execution = harness.Worker.ExecuteTask.ShouldNotBeNull();
		execution.IsCompletedSuccessfully.ShouldBeTrue();
		harness.Reads.ShouldBe(1);
		harness.Clock.GetUtcNow().ShouldBe(before);
		harness.Clock.Registrations.Reader.TryRead(out _).ShouldBeFalse();
		harness.CycleErrors.ShouldBe(0);
	}

	private sealed class Harness : IAsyncDisposable
	{
		private readonly string _outcome;
		private int _reads;
		private int _cycleErrors;
		internal int Reads => Volatile.Read(ref _reads);
		internal int CycleErrors => Volatile.Read(ref _cycleErrors);
		internal TaskCompletionSource FirstRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		internal TaskCompletionSource SecondRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		internal ObservedClock Clock { get; } = new();
		internal BackgroundService Worker { get; }
		internal Harness(string worker, string outcome)
		{
			_outcome = outcome;
			if (worker == "notifications")
			{
				var store = A.Fake<IOutboxStore>(options => options.Implements<IClaimScopedOutboxStore>());
				A.CallTo(() => store.GetService(typeof(IClaimScopedOutboxStore))).Returns(store);
				A.CallTo(() => store.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
					.ReturnsLazily((int _, CancellationToken token) => new ValueTask<IEnumerable<OutboundMessage>>(ReadOutboxAsync(token)));
				Worker = new NotificationDrainService(store, A.Fake<IEmailSender>(), new PerformanceMonitor(), new CycleLogger<NotificationDrainService>(RecordCycleError), Clock);
			}
			else if (worker == "inventory")
			{
				var store = A.Fake<IScheduleStore>();
				A.CallTo(() => store.GetAllAsync(A<CancellationToken>._)).ReturnsLazily((CancellationToken token) => ReadSchedulesAsync(token));
				var inventory = new InventoryService(store,
					new InMemoryInventoryRepository(NullLogger<InMemoryInventoryRepository>.Instance),
					new PerformanceMonitor(), NullLogger<InventoryService>.Instance);
				Worker = new InventoryCheckProcessor(store, inventory, new CycleLogger<InventoryCheckProcessor>(RecordCycleError), Clock);
			}
			else { throw new ArgumentOutOfRangeException(nameof(worker)); }
		}
		private void RecordCycleError() => Interlocked.Increment(ref _cycleErrors);
		private async Task<IEnumerable<OutboundMessage>> ReadOutboxAsync(CancellationToken token)
		{
			await ReadAsync(token);
			return [];
		}
		private async Task<IEnumerable<IScheduledMessage>> ReadSchedulesAsync(CancellationToken token)
		{
			await ReadAsync(token);
			return [];
		}
		private Task ReadAsync(CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			var read = Interlocked.Increment(ref _reads);
			if (read == 1) { FirstRead.TrySetResult(); }
			if (read == 2) { SecondRead.TrySetResult(); }
			return _outcome switch
			{
				"success" => Task.CompletedTask,
				"failure" => Task.FromException(new IOException("controlled store outage")),
				"independent-cancellation" => Task.FromException(new OperationCanceledException("independent store cancellation")),
				"pending" => Task.Delay(Timeout.Infinite, token),
				_ => throw new InvalidOperationException($"Unexpected controlled read outcome: {_outcome}"),
			};
		}
		public async ValueTask DisposeAsync()
		{
			using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
			await Worker.StopAsync(deadline.Token);
			Worker.Dispose();
		}
	}

	private sealed record Registration(TimeSpan DueTime, TimeSpan Period, DateTimeOffset CreatedAt);

	private sealed class CycleLogger<T>(Action error) : ILogger<T>
	{
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
		public bool IsEnabled(LogLevel logLevel) => true;
		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			if (logLevel == LogLevel.Error) { error(); }
		}
	}
	private sealed class ObservedClock : FakeTimeProvider
	{
		internal Channel<Registration> Registrations { get; } = Channel.CreateUnbounded<Registration>(new UnboundedChannelOptions { AllowSynchronousContinuations = false });
		public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
		{
			var createdAt = GetUtcNow();
			var timer = base.CreateTimer(callback, state, dueTime, period);
			if (!Registrations.Writer.TryWrite(new Registration(dueTime, period, createdAt)))
			{
				throw new InvalidOperationException("Cannot record official timer registration.");
			}
			return timer;
		}
	}
}
