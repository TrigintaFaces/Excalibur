// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Projections;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// An inline projection that takes too long warns, with the threshold the consumer configured.
/// </summary>
/// <remarks>
/// <para>
/// An inline projection runs inside <c>SaveAsync</c>, so a slow one adds its latency directly to every
/// write. Two public settings promised a warning for exactly that — the global
/// <c>EventNotificationOptions.InlineProjectionWarningThreshold</c> and the per-projection
/// <c>ProjectionOptions.WarningThreshold</c>, reached through <c>WithOptions</c> — and NEITHER was read
/// by any production code. The global one was even validated at start-up, so a host was told its value
/// was in range and then nothing used it.
/// </para>
/// <para>
/// Timing is driven by a <see cref="FakeTimeProvider"/> advanced from inside the apply delegate, so the
/// arms assert the threshold comparison rather than how fast the machine is.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SlowInlineProjectionWarningShould
{
	private readonly InMemoryProjectionRegistry _registry = new();
	private readonly CapturingLogger _logger = new();
	private readonly FakeTimeProvider _clock = new();

	[Fact]
	public async Task WarnWhenAProjectionExceedsItsOwnThreshold()
	{
		await RunApplyTaking(
			TimeSpan.FromMilliseconds(500),
			perProjection: TimeSpan.FromMilliseconds(200));

		_logger.Warnings.ShouldHaveSingleItem()
			.ShouldContain(nameof(OrderSummary), Case.Sensitive);
	}

	/// <summary>
	/// CONTROL. Without it the arm above is satisfied by a processor that warns on every apply.
	/// </summary>
	[Fact]
	public async Task StaySilentWhenAProjectionIsInsideItsThreshold()
	{
		await RunApplyTaking(
			TimeSpan.FromMilliseconds(50),
			perProjection: TimeSpan.FromMilliseconds(200));

		_logger.Warnings.ShouldBeEmpty();
	}

	/// <summary>
	/// The per-projection threshold OVERRIDES the global one, which is the whole point of having both:
	/// one slow projection should not force every other projection's threshold up.
	/// </summary>
	[Fact]
	public async Task PreferThePerProjectionThresholdOverTheGlobalOne()
	{
		// Over the global, under its own: silent.
		await RunApplyTaking(
			TimeSpan.FromMilliseconds(500),
			perProjection: TimeSpan.FromSeconds(5),
			global: TimeSpan.FromMilliseconds(100));

		_logger.Warnings.ShouldBeEmpty();
	}

	/// <summary>
	/// With no per-projection threshold the global applies — otherwise the global setting is the inert
	/// one it used to be.
	/// </summary>
	[Fact]
	public async Task FallBackToTheGlobalThresholdWhenAProjectionSetsNone()
	{
		await RunApplyTaking(
			TimeSpan.FromMilliseconds(500),
			perProjection: null,
			global: TimeSpan.FromMilliseconds(100));

		_logger.Warnings.ShouldHaveSingleItem()
			.ShouldContain(nameof(OrderSummary), Case.Sensitive);
	}

	private async Task RunApplyTaking(
		TimeSpan duration,
		TimeSpan? perProjection,
		TimeSpan? global = null)
	{
		ProjectionOptions? options = perProjection is null
			? null
			: new ProjectionOptions { WarningThreshold = perProjection.Value };

		_registry.Register(new ProjectionRegistration(
			typeof(OrderSummary),
			ProjectionMode.Inline,
			new MultiStreamProjection<OrderSummary>(),
			inlineApply: (_, _, _, _) =>
			{
				// The apply "takes" this long, deterministically.
				_clock.Advance(duration);
				return Task.CompletedTask;
			},
			options: options));

		var notificationOptions = new EventNotificationOptions();
		if (global is not null)
		{
			notificationOptions.InlineProjectionWarningThreshold = global.Value;
		}

		var processor = new InlineProjectionProcessor(
			_registry,
			A.Fake<IServiceScopeFactory>(),
			_logger,
			notificationOptions: Options.Create(notificationOptions),
			timeProvider: _clock);

		await processor.ProcessAsync(
			[new TestOrderPlaced()],
			new EventNotificationContext("agg-1", "TestAggregate", 1, DateTimeOffset.UtcNow),
			NotificationFailurePolicy.Propagate,
			CancellationToken.None);
	}

	/// <summary>
	/// Captures warning text. A fake of <c>ILogger</c> would assert on a generic Log call and its state
	/// object; the message is what a consumer reads, so that is what this asserts.
	/// </summary>
	private sealed class CapturingLogger : ILogger<InlineProjectionProcessor>
	{
		internal List<string> Warnings { get; } = [];

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			if (logLevel == LogLevel.Warning)
			{
				Warnings.Add(formatter(state, exception));
			}
		}
	}
}
