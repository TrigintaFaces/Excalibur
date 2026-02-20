// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;

using Excalibur.EventSourcing.InMemory;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Integration.Tests.Observability;

/// <summary>
/// Integration tests for observability features including logging, metrics, and tracing.
/// </summary>
public sealed class ObservabilityIntegrationShould : IntegrationTestBase
{
	#region Logging Integration Tests

	[Fact]
	public void LoggingProvider_CapturesStructuredLogs()
	{
		// Arrange
		var logMessages = new List<(LogLevel Level, string Message)>();
		var services = new ServiceCollection();
		_ = services.AddLogging(builder =>
		{
			_ = builder.AddProvider(new TestLoggerProvider(logMessages));
			_ = builder.SetMinimumLevel(LogLevel.Debug);
		});
		using var provider = services.BuildServiceProvider();
		var logger = provider.GetRequiredService<ILogger<ObservabilityIntegrationShould>>();

		// Act
		logger.LogInformation("Test message with {Parameter}", "value");

		// Assert
		logMessages.ShouldContain(log => log.Message.Contains("Test message"));
	}

	[Fact]
	public void LoggingProvider_FiltersLogsByLevel()
	{
		// Arrange
		var logMessages = new List<(LogLevel Level, string Message)>();
		var services = new ServiceCollection();
		_ = services.AddLogging(builder =>
		{
			_ = builder.AddProvider(new TestLoggerProvider(logMessages));
			_ = builder.SetMinimumLevel(LogLevel.Warning);
		});
		using var provider = services.BuildServiceProvider();
		var logger = provider.GetRequiredService<ILogger<ObservabilityIntegrationShould>>();

		// Act
		logger.LogDebug("Debug message");
		logger.LogInformation("Info message");
		logger.LogWarning("Warning message");

		// Assert - Only warning and above should be captured
		logMessages.Count.ShouldBe(1);
		logMessages[0].Level.ShouldBe(LogLevel.Warning);
	}

	#endregion

	#region Activity/Tracing Integration Tests

	[Fact]
	public void ActivitySource_CreatesTraces()
	{
		// Arrange
		using var activitySource = new ActivitySource("Test.Observability");
		var capturedActivities = new List<Activity>();
		using var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == "Test.Observability",
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
			ActivityStarted = activity => capturedActivities.Add(activity)
		};
		ActivitySource.AddActivityListener(listener);

		// Act
		using var activity = activitySource.StartActivity("TestOperation");
		_ = (activity?.SetTag("test.key", "test.value"));

		// Assert
		capturedActivities.ShouldNotBeEmpty();
		var testActivity = capturedActivities.FirstOrDefault(a => a.OperationName == "TestOperation");
		_ = testActivity.ShouldNotBeNull();
		testActivity.GetTagItem("test.key").ShouldBe("test.value");
	}

	[Fact]
	public void ActivitySource_PropagatesContext()
	{
		// Arrange
		using var activitySource = new ActivitySource("Test.Context");
		var capturedActivities = new List<Activity>();
		using var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == "Test.Context",
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
			ActivityStarted = activity => capturedActivities.Add(activity)
		};
		ActivitySource.AddActivityListener(listener);

		// Act - Create parent and child activities
		using var parentActivity = activitySource.StartActivity("Parent");
		using var childActivity = activitySource.StartActivity("Child");

		// Assert - Child should have parent as context
		childActivity?.ParentId.ShouldBe(parentActivity?.Id);
	}

	#endregion

	#region Metrics Integration Tests

	[Fact]
	public async Task HealthCheck_ReportsHealthStatus()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddHealthChecks()
			.AddCheck("test-check", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());
		await using var provider = services.BuildServiceProvider();
		var healthCheckService = provider.GetRequiredService<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService>();

		// Act
		var report = await healthCheckService.CheckHealthAsync(TestCancellationToken).ConfigureAwait(false);

		// Assert
		report.Status.ShouldBe(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy);
	}

	#endregion

	#region In-Memory Event Store Integration Tests

	[Fact]
	public async Task InMemoryEventStore_PersistsEvents()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddInMemoryEventStore();
		await using var provider = services.BuildServiceProvider();
		var eventStore = provider.GetRequiredService<Excalibur.EventSourcing.Abstractions.IEventStore>();

		var aggregateId = Guid.NewGuid().ToString();
		var aggregateType = "TestAggregate";
		var events = new List<IDomainEvent>
		{
			new TestDomainEvent(aggregateId, "Event1"),
			new TestDomainEvent(aggregateId, "Event2")
		};

		// Act
		_ = await eventStore.AppendAsync(aggregateId, aggregateType, events, -1, TestCancellationToken).ConfigureAwait(false);
		var loadedEvents = await eventStore.LoadAsync(aggregateId, aggregateType, TestCancellationToken).ConfigureAwait(false);

		// Assert
		_ = loadedEvents.ShouldNotBeNull();
		loadedEvents.Count.ShouldBe(2);
	}

	#endregion

	#region Test Helpers

	private sealed class TestLoggerProvider(List<(LogLevel Level, string Message)> logMessages) : ILoggerProvider
	{
		public ILogger CreateLogger(string categoryName) => new TestLogger(logMessages);

		public void Dispose()
		{
			GC.SuppressFinalize(this);
		}
	}

	private sealed class TestLogger(List<(LogLevel Level, string Message)> logMessages) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			logMessages.Add((logLevel, formatter(state, exception)));
		}
	}

	private sealed record TestDomainEvent(string AggregateId, string Data) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public long Version { get; init; }
		public string EventType => GetType().Name;
		public IDictionary<string, object>? Metadata { get; init; }
	}

	#endregion
}
