// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Tests.Projections;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

/// <summary>
/// A value a consumer configures must reach the component that reads it, resolved through the real
/// container rather than handed in by a test.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is separate from the arms that exercise the threshold itself.</b> Those construct the
/// host directly and pass it an options instance, which establishes that the code reads whatever it is
/// given — and says nothing about whether a consumer's <c>Configure</c> call ever arrives. That gap is
/// the one this option fell into before: it was declared, published in the API baseline, and validated
/// at startup, so every signal said it was in use while nothing read it. A test that supplies the value
/// itself would have passed throughout.
/// </para>
/// <para>
/// <b>The assertion is deliberately the NEGATIVE one.</b> The configured threshold is far above the
/// default and the apply is slow enough to breach the default but not the configured value, so silence
/// is only possible if the configured value arrived. Asserting a warning instead would pass on the
/// default and prove nothing about configuration.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class ConfiguredApplyWarningThresholdShould
{
	[Fact]
	public async Task ReachTheHostThroughTheContainerRatherThanTheDefault()
	{
		var clock = new FakeTimeProvider();
		var logger = new WarningRecorder();

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
			// Breaches the 100ms DEFAULT by a wide margin; well inside the configured 5s.
			inlineApply: (_, _, _, _) =>
			{
				clock.Advance(TimeSpan.FromMilliseconds(900));
				return Task.CompletedTask;
			}));

		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(query);
		_ = services.AddSingleton(serializer);
		_ = services.AddSingleton<IProjectionRegistry>(registry);
		_ = services.AddSingleton<TimeProvider>(clock);
		_ = services.AddSingleton<ILogger<AsyncProjectionProcessingHost>>(logger);

		// The production registration path, and the consumer's own Configure call alongside it.
		_ = services.AddExcalibur(x => x.AddEventSourcing(es =>
		{
			_ = es.UseInMemory();
			_ = es.EnableProjectionProcessing(options => options.SubscriptionScope = "warning-threshold/events/views");
		}));
		_ = services.Configure<EventNotificationOptions>(
			o => o.ProjectionApplyWarningThreshold = TimeSpan.FromSeconds(5));

		await using var provider = services.BuildServiceProvider();

		var processor = provider.GetRequiredService<IProjectionProcessor>();
		_ = await processor.ProcessPendingAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);

		logger.Warnings.ShouldNotContain(
			w => w.Contains("warning threshold", StringComparison.Ordinal),
			customMessage:
				"a 900ms apply warned, so the host used the 100ms default -- the configured 5s never arrived");
	}

	private sealed class WarningRecorder : ILogger<AsyncProjectionProcessingHost>
	{
		public List<string> Warnings { get; } = [];

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
				Warnings.Add(formatter(state, exception));
			}
		}
	}
}
