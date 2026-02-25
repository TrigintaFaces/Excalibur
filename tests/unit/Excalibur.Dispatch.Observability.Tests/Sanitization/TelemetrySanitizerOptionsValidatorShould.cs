// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Sanitization;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Tests.Sanitization;

/// <summary>
/// Tests for <see cref="TelemetrySanitizerOptionsValidator"/> verifying that
/// a startup warning is emitted when IncludeRawPii=true in non-Development environments (S566.6).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Sanitization")]
public sealed class TelemetrySanitizerOptionsValidatorShould
{
	[Fact]
	public void ReturnSuccessWhenIncludeRawPiiFalse()
	{
		// Arrange
		var env = A.Fake<IHostEnvironment>();
		A.CallTo(() => env.EnvironmentName).Returns("Production");
		var logger = NullLogger<TelemetrySanitizerOptionsValidator>.Instance;
		var validator = new TelemetrySanitizerOptionsValidator(env, logger);
		var options = new TelemetrySanitizerOptions { IncludeRawPii = false };

		// Act
		var result = validator.Validate(null, options);

		// Assert — always returns success (warning-only validator)
		result.ShouldBe(ValidateOptionsResult.Success);
	}

	[Fact]
	public void ReturnSuccessWhenIncludeRawPiiTrueInDevelopment()
	{
		// Arrange
		var env = A.Fake<IHostEnvironment>();
		A.CallTo(() => env.EnvironmentName).Returns("Development");
		var logger = NullLogger<TelemetrySanitizerOptionsValidator>.Instance;
		var validator = new TelemetrySanitizerOptionsValidator(env, logger);
		var options = new TelemetrySanitizerOptions { IncludeRawPii = true };

		// Act
		var result = validator.Validate(null, options);

		// Assert — no warning needed in Development
		result.ShouldBe(ValidateOptionsResult.Success);
	}

	[Fact]
	public void ReturnSuccessButLogWarningWhenIncludeRawPiiTrueInProduction()
	{
		// Arrange
		var env = A.Fake<IHostEnvironment>();
		A.CallTo(() => env.EnvironmentName).Returns("Production");
		var loggerFactory = new TrackingLoggerFactory();
		var logger = loggerFactory.CreateLogger<TelemetrySanitizerOptionsValidator>();
		var validator = new TelemetrySanitizerOptionsValidator(env, logger);
		var options = new TelemetrySanitizerOptions { IncludeRawPii = true };

		// Act
		var result = validator.Validate(null, options);

		// Assert — returns success (does not block startup)
		result.ShouldBe(ValidateOptionsResult.Success);

		// Assert — warning was logged
		loggerFactory.LogEntries.ShouldContain(
			entry => entry.LogLevel == LogLevel.Warning &&
					 entry.Message.Contains("IncludeRawPii", StringComparison.Ordinal) &&
					 entry.Message.Contains("Production", StringComparison.Ordinal));
	}

	[Fact]
	public void ReturnSuccessButLogWarningWhenIncludeRawPiiTrueInStaging()
	{
		// Arrange
		var env = A.Fake<IHostEnvironment>();
		A.CallTo(() => env.EnvironmentName).Returns("Staging");
		var loggerFactory = new TrackingLoggerFactory();
		var logger = loggerFactory.CreateLogger<TelemetrySanitizerOptionsValidator>();
		var validator = new TelemetrySanitizerOptionsValidator(env, logger);
		var options = new TelemetrySanitizerOptions { IncludeRawPii = true };

		// Act
		var result = validator.Validate(null, options);

		// Assert
		result.ShouldBe(ValidateOptionsResult.Success);
		loggerFactory.LogEntries.ShouldContain(
			entry => entry.LogLevel == LogLevel.Warning &&
					 entry.Message.Contains("Staging", StringComparison.Ordinal));
	}

	[Fact]
	public void NotLogWarningWhenIncludeRawPiiFalseInProduction()
	{
		// Arrange
		var env = A.Fake<IHostEnvironment>();
		A.CallTo(() => env.EnvironmentName).Returns("Production");
		var loggerFactory = new TrackingLoggerFactory();
		var logger = loggerFactory.CreateLogger<TelemetrySanitizerOptionsValidator>();
		var validator = new TelemetrySanitizerOptionsValidator(env, logger);
		var options = new TelemetrySanitizerOptions { IncludeRawPii = false };

		// Act
		_ = validator.Validate(null, options);

		// Assert — no warning entries
		loggerFactory.LogEntries.ShouldNotContain(
			entry => entry.LogLevel == LogLevel.Warning);
	}

	[Fact]
	public void ThrowOnNullOptions()
	{
		// Arrange
		var env = A.Fake<IHostEnvironment>();
		A.CallTo(() => env.EnvironmentName).Returns("Production");
		var logger = NullLogger<TelemetrySanitizerOptionsValidator>.Instance;
		var validator = new TelemetrySanitizerOptionsValidator(env, logger);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => validator.Validate(null, null!));
	}

	#region Test Helpers

	private sealed record LogEntry(LogLevel LogLevel, string? Message);

	private sealed class TrackingLoggerFactory : ILoggerFactory
	{
		public List<LogEntry> LogEntries { get; } = [];

		public ILogger CreateLogger(string categoryName) => new TrackingLogger(this);

		public void AddProvider(ILoggerProvider provider) { }
		public void Dispose() { }

		public ILogger<T> CreateLogger<T>() => new Logger<T>(this);
	}

	private sealed class TrackingLogger(TrackingLoggerFactory factory) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			factory.LogEntries.Add(new LogEntry(logLevel, formatter(state, exception)));
		}
	}

	#endregion
}
