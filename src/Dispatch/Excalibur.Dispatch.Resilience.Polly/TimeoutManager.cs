// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Implementation of centralized timeout management.
/// </summary>
public partial class TimeoutManager : ITimeoutManager
{
	private readonly TimeoutManagerOptions _options;
	private readonly ILogger<TimeoutManager> _logger;
	private readonly ConcurrentDictionary<string, TimeSpan> _customTimeouts;

	/// <summary>
	/// Initializes a new instance of the <see cref="TimeoutManager" /> class.
	/// </summary>
	/// <param name="options"> The timeout manager options. </param>
	/// <param name="logger"> The logger instance. </param>
	public TimeoutManager(IOptions<TimeoutManagerOptions> options, ILogger<TimeoutManager> logger)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_customTimeouts = new ConcurrentDictionary<string, TimeSpan>(_options.OperationTimeouts, StringComparer.Ordinal);

		// Register well-known operation types
		RegisterWellKnownTimeouts();
	}

	/// <inheritdoc />
	public TimeSpan DefaultTimeout => _options.DefaultTimeout;

	/// <inheritdoc />
	public TimeSpan GetTimeout(string operationName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

		// Check custom timeouts first
		if (_customTimeouts.TryGetValue(operationName, out var customTimeout))
		{
			LogTimeoutRetrieved(operationName, customTimeout.TotalMilliseconds);
			return customTimeout;
		}

		// Check for operation type patterns
		var timeout = GetTimeoutByPattern(operationName);
		LogTimeoutRetrieved(operationName, timeout.TotalMilliseconds);
		return timeout;
	}

	/// <inheritdoc />
	public void RegisterTimeout(string operationName, TimeSpan timeout)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

		_ = _customTimeouts.AddOrUpdate(
			operationName,
			static (_, state) => state,
			static (_, _, state) => state,
			timeout);
		LogTimeoutRegistered(operationName, timeout.TotalMilliseconds);
	}

	/// <summary>
	/// Checks if an operation is approaching its timeout.
	/// </summary>
	/// <param name="operationName"> The operation name. </param>
	/// <param name="elapsed"> The elapsed time. </param>
	/// <returns> True if the operation is slow. </returns>
	public bool IsSlowOperation(string operationName, TimeSpan elapsed)
	{
		var timeout = GetTimeout(operationName);
		var threshold = timeout.TotalMilliseconds * _options.SlowOperationThreshold;

		if (elapsed.TotalMilliseconds >= threshold)
		{
			if (_options.LogTimeoutWarnings)
			{
				LogSlowOperationDetected(operationName, elapsed.TotalMilliseconds, timeout.TotalMilliseconds);
			}

			return true;
		}

		return false;
	}

	private void RegisterWellKnownTimeouts()
	{
		// Database operations
		RegisterTimeout("Database.Query", _options.DatabaseTimeout);
		RegisterTimeout("Database.Command", _options.DatabaseTimeout);
		RegisterTimeout("Database.Transaction", TimeSpan.FromSeconds(30));

		// HTTP operations
		RegisterTimeout("Http.Get", _options.HttpTimeout);
		RegisterTimeout("Http.Post", _options.HttpTimeout);
		RegisterTimeout("Http.Put", _options.HttpTimeout);
		RegisterTimeout("Http.Delete", _options.HttpTimeout);

		// Message queue operations
		RegisterTimeout("Queue.Send", _options.MessageQueueTimeout);
		RegisterTimeout("Queue.Receive", _options.MessageQueueTimeout);
		RegisterTimeout("Queue.Process", _options.MessageQueueTimeout);

		// Cache operations
		RegisterTimeout("Cache.Get", _options.CacheTimeout);
		RegisterTimeout("Cache.Set", _options.CacheTimeout);
		RegisterTimeout("Cache.Delete", _options.CacheTimeout);
	}

	private TimeSpan GetTimeoutByPattern(string operationName)
	{
		// Pattern matching for operation types
		if (operationName.StartsWith("Database.", StringComparison.OrdinalIgnoreCase) ||
			operationName.Contains("Sql", StringComparison.OrdinalIgnoreCase) ||
			operationName.Contains("Query", StringComparison.OrdinalIgnoreCase))
		{
			return _options.DatabaseTimeout;
		}

		if (operationName.StartsWith("Http.", StringComparison.OrdinalIgnoreCase) ||
			operationName.Contains("Api", StringComparison.OrdinalIgnoreCase) ||
			operationName.Contains("Rest", StringComparison.OrdinalIgnoreCase))
		{
			return _options.HttpTimeout;
		}

		if (operationName.StartsWith("Queue.", StringComparison.OrdinalIgnoreCase) ||
			operationName.Contains("Message", StringComparison.OrdinalIgnoreCase) ||
			operationName.Contains("Bus", StringComparison.OrdinalIgnoreCase))
		{
			return _options.MessageQueueTimeout;
		}

		if (operationName.StartsWith("Cache.", StringComparison.OrdinalIgnoreCase) ||
			operationName.Contains("Redis", StringComparison.OrdinalIgnoreCase) ||
			operationName.Contains("Memory", StringComparison.OrdinalIgnoreCase))
		{
			return _options.CacheTimeout;
		}

		return _options.DefaultTimeout;
	}

	// Source-generated logging methods
	[LoggerMessage(ResilienceEventId.TimeoutRetrieved, LogLevel.Debug,
		"Retrieved timeout for operation '{OperationName}': {Timeout}ms")]
	private partial void LogTimeoutRetrieved(string operationName, double timeout);

	[LoggerMessage(ResilienceEventId.TimeoutRegistered, LogLevel.Information,
		"Registered custom timeout for operation '{OperationName}': {Timeout}ms")]
	private partial void LogTimeoutRegistered(string operationName, double timeout);

	[LoggerMessage(ResilienceEventId.SlowOperationDetected, LogLevel.Warning,
		"Operation '{OperationName}' is approaching timeout: {Elapsed}ms / {Timeout}ms")]
	private partial void LogSlowOperationDetected(string operationName, double elapsed, double timeout);
}
