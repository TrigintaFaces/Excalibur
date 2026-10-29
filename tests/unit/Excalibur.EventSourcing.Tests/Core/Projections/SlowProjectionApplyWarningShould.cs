// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Diagnostics;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;
using Excalibur.EventSourcing.Tests.Projections;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

/// <summary>
/// A slow apply is the earliest observable cause of a degraded session-consistent read, so the host
/// warns when applying one batch to one projection exceeds its configured threshold.
/// </summary>
/// <remarks>
/// <para>
/// <b>These arms exist because the option had no reader.</b> Both thresholds were declared, published
/// in the API baseline, and validated at startup — so a host was told its value was in range — while
/// nothing read either one. A validated option with no reader is the shape that survives review: the
/// startup validation looks like use.
/// </para>
/// <para>
/// <b>Why a fake clock rather than a real sleep.</b> The quantity under test is elapsed time, so a real
/// delay would make the arms rate-dependent and flaky under full-suite load. The apply delegate advances
/// the clock itself, which is both deterministic and exactly the measurement the host performs.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class SlowProjectionApplyWarningShould
{
	private static readonly TimeSpan GlobalThreshold = TimeSpan.FromMilliseconds(100);

	[Fact]
	public async Task WarnWhenAnApplyExceedsTheGlobalThreshold()
	{
		var warnings = await RunAsync(applyTakes: TimeSpan.FromMilliseconds(250), perProjection: null);

		warnings.Count.ShouldBe(1);
		warnings[0].EventId.ShouldBe(EventSourcingEventId.AsyncProjectionApplySlow);
		warnings[0].Message.ShouldContain(
			nameof(OrderSummary), customMessage: "the warning must name which projection was slow");
	}

	[Fact]
	public async Task NotWarnWhenTheApplyIsInsideTheThreshold()
	{
		// LIVENESS. Without this arm an implementation that warned unconditionally would satisfy every
		// other arm here, and a host would warn on every batch it ever applied.
		var warnings = await RunAsync(applyTakes: TimeSpan.FromMilliseconds(10), perProjection: null);

		warnings.ShouldBeEmpty();
	}

	[Fact]
	public async Task PreferAProjectionsOwnThresholdOverTheGlobalOne()
	{
		// A projection that declares it is expected to be slow must not warn at a duration the global
		// threshold would reject. Asserting only the opposite direction (below) would also pass against
		// an implementation that took the MINIMUM of the two.
		var warnings = await RunAsync(
			applyTakes: TimeSpan.FromMilliseconds(250),
			perProjection: TimeSpan.FromSeconds(1));

		warnings.ShouldBeEmpty();
	}

	[Fact]
	public async Task WarnOnAProjectionsOwnTighterThresholdTheGlobalOneWouldAllow()
	{
		// The other direction of the same preference, and the one that separates "prefers the
		// per-projection value" from "takes the maximum of the two".
		var warnings = await RunAsync(
			applyTakes: TimeSpan.FromMilliseconds(50),
			perProjection: TimeSpan.FromMilliseconds(5));

		warnings.Count.ShouldBe(1);
		warnings[0].Message.ShouldContain("5", customMessage: "the warning must quote the threshold it used");
	}

	private static async Task<List<(int EventId, string Message)>> RunAsync(
		TimeSpan applyTakes,
		TimeSpan? perProjection)
	{
		var clock = new FakeTimeProvider();
		var logger = new RecordingLogger();

		var query = A.Fake<IGlobalStreamQuery>();
		A.CallTo(() => query.GetHeadPositionAsync(A<CancellationToken>._)).Returns(1L);
		var stored = new StoredEvent(
			"e1", "order-1", "Order", "OrderCreated", [], null, 1, DateTimeOffset.UtcNow)
		{ GlobalPosition = 1 };
		A.CallTo(() => query.ReadAllAsync(A<GlobalStreamPosition>._, A<int>._, A<CancellationToken>._))
			.ReturnsLazily((GlobalStreamPosition position, int _, CancellationToken _) =>
				new ValueTask<IReadOnlyList<StoredEvent>>(
					position.Position >= 1 ? [] : new[] { stored }));

		var serializer = A.Fake<IEventSerializer>();
		A.CallTo(() => serializer.ResolveType(A<string>._)).Returns(typeof(TestOrderPlaced));
		A.CallTo(() => serializer.DeserializeEvent(A<byte[]>._, A<Type>._)).Returns(new TestOrderPlaced());

		var registry = new InMemoryProjectionRegistry();
		registry.Register(new ProjectionRegistration(
			typeof(OrderSummary),
			ProjectionMode.Async,
			new MultiStreamProjection<OrderSummary>(),
			// The delegate IS the elapsed time: it advances the clock the host is measuring with.
			inlineApply: (_, _, _, _) =>
			{
				clock.Advance(applyTakes);
				return Task.CompletedTask;
			},
			options: perProjection is { } t ? new ProjectionOptions { WarningThreshold = t } : null));

		var services = new ServiceCollection();
		_ = services.AddSingleton(query);
		await using var provider = services.BuildServiceProvider();

		using var host = new AsyncProjectionProcessingHost(
			registry,
			serializer,
			new InMemorySubscriptionCheckpointStore(),
			Options.Create(new GlobalStreamProjectionOptions { SubscriptionScope = "unit-test", CheckpointInterval = 1 }),
			provider,
			logger,
			Options.Create(new EventNotificationOptions
			{
				ProjectionApplyWarningThreshold = GlobalThreshold,
			}),
			clock);

		_ = await host.ProcessPendingAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

		return logger.Warnings;
	}

	// Records only warnings, which is the whole assertion surface here. A FakeItEasy fake of
	// ILogger<T> cannot serve: the generated Log<TState> call formats through a delegate, so the
	// rendered message -- the thing these arms read -- never materialises.
	private sealed class RecordingLogger : ILogger<AsyncProjectionProcessingHost>
	{
		public List<(int EventId, string Message)> Warnings { get; } = [];

		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull => null;

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
				Warnings.Add((eventId.Id, formatter(state, exception)));
			}
		}
	}
}
