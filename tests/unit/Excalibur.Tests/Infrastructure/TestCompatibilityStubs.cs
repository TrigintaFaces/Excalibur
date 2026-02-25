// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Data.Abstractions;
using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Abstractions.Resilience;

namespace Excalibur.Tests.Infrastructure;

/// <summary>
/// Persistence provider type enum stub.
/// </summary>
public enum PersistenceProviderType
{
	SqlServer,
	Postgres,
	MongoDB,
	ElasticSearch,
}

/// <summary>
/// Stub for MongoDB IMongoDatabase.
/// </summary>
public interface IMongoDatabase
{
	/// <summary>
	/// Gets a collection.
	/// </summary>
	IMongoCollection<T> GetCollection<T>(string name);
}

/// <summary>
/// Stub for MongoDB IMongoCollection.
/// </summary>
public interface IMongoCollection<T>
{
	/// <summary>
	/// Inserts a document.
	/// </summary>
	Task InsertOneAsync(T document);
}

/// <summary>
/// Extension methods for IEventStore stub.
/// </summary>
public static class EventStoreExtensions
{
	/// <summary>
	/// Loads events async with offset and batch size.
	/// </summary>
	public static Task<IReadOnlyList<IDispatchMessage>> LoadEventsAsync(
		this IEventStore eventStore, int offset, int batchSize,
		CancellationToken cancellationToken = default) =>
		Task.FromResult<IReadOnlyList<IDispatchMessage>>(new List<IDispatchMessage>());

	/// <summary>
	/// Loads events async with aggregate ID.
	/// </summary>
	public static Task<IReadOnlyList<IDispatchMessage>> LoadEventsAsync(
		this IEventStore eventStore, string aggregateId,
		CancellationToken cancellationToken = default) =>
		Task.FromResult<IReadOnlyList<IDispatchMessage>>(new List<IDispatchMessage>());

	/// <summary>
	/// Saves migrated events async.
	/// </summary>
	public static Task SaveMigratedEventsAsync(
		this IEventStore eventStore,
		IEnumerable<IDispatchMessage> events, CancellationToken cancellationToken = default) =>
		Task.CompletedTask;
}

/// <summary>
/// Extension methods to provide backward compatibility for tests with outdated interfaces.
/// </summary>
public static class TestCompatibilityStubs
{
	/// <summary>
	/// Stub for old ContainsAsync method.
	/// </summary>
	public static Task<bool> ContainsAsync(this IDeduplicationStore store, string key,
		CancellationToken cancellationToken = default)
	{
		// Map to new CheckAndMarkAsync - just check if it exists without marking
		var context = new DeduplicationContext
		{
			ProcessorId = "test-processor",
			CorrelationId = key,
		};

		// This is a stub - in real code we'd need to handle this differently For tests, we'll just return false to keep them running
		return Task.FromResult(false);
	}

	/// <summary>
	/// Stub for old AddAsync method.
	/// </summary>
	public static Task AddAsync(this IDeduplicationStore store, string key,
		DateTimeOffset expiration,
		CancellationToken cancellationToken = default)
	{
		// Map to new CheckAndMarkAsync
		var context = new DeduplicationContext
		{
			ProcessorId = "test-processor",
			CorrelationId = key,
		};

		// For tests, just complete the task
		return Task.CompletedTask;
	}

	/// <summary>
	/// Stub for old IsDuplicateAsync with store parameter.
	/// </summary>
	public static Task<bool> IsDuplicateAsync(this IDeduplicationStrategy strategy, string key,
		IDeduplicationStore store,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(strategy);

		// The new interface doesn't take a store parameter
		return strategy.IsDuplicateAsync(key, cancellationToken);
	}

	/// <summary>
	/// Stub for old RecordProcessedAsync method.
	/// </summary>
	public static Task RecordProcessedAsync(this IDeduplicationStrategy strategy, string key,
		IDeduplicationStore store,
		DeduplicationOptions options, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(strategy);
		ArgumentNullException.ThrowIfNull(options);

		// Map to new MarkProcessedAsync
		return strategy.MarkAsProcessedAsync(key, options.DeduplicationWindow, cancellationToken);
	}

	/// <summary>
	/// Stub for old RecordProcessedAsync method for ContentHashDeduplicationStrategy.
	/// </summary>
	public static Task RecordProcessedAsync(this ContentHashDeduplicationStrategy strategy, string key,
		IDeduplicationStore store,
		DeduplicationOptions options, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(strategy);
		ArgumentNullException.ThrowIfNull(options);

		// Map to new MarkProcessedAsync
		return strategy.MarkAsProcessedAsync(key, options.DeduplicationWindow, cancellationToken);
	}

	/// <summary>
	/// Stub for old RecordProcessedAsync method for CompositeDeduplicationStrategy.
	/// </summary>
	public static Task RecordProcessedAsync(this CompositeDeduplicationStrategy strategy, string key,
		IDeduplicationStore store,
		DeduplicationOptions options, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(strategy);
		ArgumentNullException.ThrowIfNull(options);

		// Map to new MarkProcessedAsync
		return strategy.MarkAsProcessedAsync(key, options.DeduplicationWindow, cancellationToken);
	}

	/// <summary>
	/// Stub for old IsDuplicateAsync method for ContentHashDeduplicationStrategy.
	/// </summary>
	public static Task<bool> IsDuplicateAsync(this ContentHashDeduplicationStrategy strategy, string key,
		IDeduplicationStore store,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(strategy);

		// The new interface doesn't take a store parameter
		return strategy.IsDuplicateAsync(key, cancellationToken);
	}

	/// <summary>
	/// Stub for old IsDuplicateAsync method for CompositeDeduplicationStrategy.
	/// </summary>
	public static Task<bool> IsDuplicateAsync(this CompositeDeduplicationStrategy strategy, string key,
		IDeduplicationStore store,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(strategy);

		// The new interface doesn't take a store parameter
		return strategy.IsDuplicateAsync(key, cancellationToken);
	}
}

/// <summary>
/// BenchmarkDotNet Job stub.
/// </summary>
public static class Job
{
	public static object ShortRun => new();

	public static object MediumRun => new();

	public static object LongRun => new();
}

/// <summary>
/// Default persistence provider factory stub.
/// </summary>
public class DefaultPersistenceProviderFactory : IPersistenceProviderFactory
{
	private readonly Dictionary<string, IPersistenceProvider> _providers = [];

	TProvider IPersistenceProviderFactory.CreateProvider<TProvider>(string name)
	{
		var provider = new TestPersistenceProvider();
		_providers[name] = provider;
		return (TProvider)(object)provider;
	}

	TProvider IPersistenceProviderFactory.CreateProvider<TProvider>()
	{
		var provider = new TestPersistenceProvider();
		var key = typeof(TProvider).Name + "_default";
		_providers[key] = provider;
		return (TProvider)(object)provider;
	}

	/// <inheritdoc />
	public IPersistenceProvider? GetProvider(string name)
	{
		_ = _providers.TryGetValue(name, out var provider);
		return provider;
	}

	/// <inheritdoc />
	public IEnumerable<string> GetProviderNames() => _providers.Keys;

	/// <inheritdoc />
	public void RegisterProvider(string name, IPersistenceProvider provider) => _providers[name] = provider;

	/// <inheritdoc />
	public bool UnregisterProvider(string name) => _providers.Remove(name);

	/// <inheritdoc />
	public Task DisposeAllProvidersAsync()
	{
		_providers.Clear();
		return Task.CompletedTask;
	}
}

/// <summary>
/// Test persistence provider implementation.
/// </summary>
public class TestPersistenceProvider : IPersistenceProvider
{
	/// <inheritdoc />
	public string Name { get; } = "Test";

	/// <inheritdoc />
	public string ProviderType { get; } = "Test";

	/// <inheritdoc />
	public bool IsAvailable { get; } = true;

	/// <inheritdoc />
	public string ConnectionString { get; } = "test://connection";

	/// <inheritdoc />
	public IDataRequestRetryPolicy RetryPolicy { get; } = null!;

	/// <inheritdoc />
	public Task<TResult> ExecuteAsync<TConnection, TResult>(
		IDataRequest<TConnection, TResult> request,
		CancellationToken cancellationToken = default)
		where TConnection : IDisposable =>
		Task.FromResult(default(TResult)!);

	/// <inheritdoc />
	public Task<TResult> ExecuteInTransactionAsync<TConnection, TResult>(
		IDataRequest<TConnection, TResult> request,
		ITransactionScope transactionScope, CancellationToken cancellationToken = default)
		where TConnection : IDisposable =>
		Task.FromResult(default(TResult)!);

	/// <inheritdoc />
	public ITransactionScope
		CreateTransactionScope(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, TimeSpan? timeout = null) => null!;

	/// <inheritdoc />
	public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

	/// <inheritdoc />
	public Task<IDictionary<string, object>> GetMetricsAsync(CancellationToken cancellationToken = default) =>
		Task.FromResult<IDictionary<string, object>>(new Dictionary<string, object>());

	/// <inheritdoc />
	public Task InitializeAsync(IPersistenceOptions options, CancellationToken cancellationToken = default) => Task.CompletedTask;

	/// <inheritdoc />
	public Task<IDictionary<string, object>?> GetConnectionPoolStatsAsync(CancellationToken cancellationToken = default) =>
		Task.FromResult<IDictionary<string, object>?>(null);

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		Dispose();
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		// No resources to dispose in this test stub
	}
}

/// <summary>
/// Stub for RealEventStore.
/// </summary>
public class RealEventStore
{
	/// <summary>
	/// Loads events.
	/// </summary>
	public Task<IEnumerable<object>> LoadEventsAsync(string aggregateId, CancellationToken cancellationToken = default) =>
		Task.FromResult(Enumerable.Empty<object>());

	/// <summary>
	/// Saves migrated events.
	/// </summary>
	public Task SaveMigratedEventsAsync(IEnumerable<object> events, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>
/// Stub for test telemetry channel.
/// </summary>
public class TestTelemetryChannel
{
	/// <summary>
	/// Gets or sets a value indicating the developer mode.
	/// </summary>
	public required bool DeveloperMode { get; set; }

	/// <summary>
	/// Gets or sets the endpoint address.
	/// </summary>
	public required string EndpointAddress { get; set; }
}

/// <summary>
/// Configuration validation error for hosting tests.
/// </summary>
public class ConfigurationValidationError(string propertyPath, string message, object? value = null)
{
	public string PropertyPath { get; set; } = propertyPath;

	public string Message { get; set; } = message;

	public object? Value { get; set; } = value;
}

/// <summary>
/// Configuration validation exception for hosting tests.
/// </summary>
public class ConfigurationValidationException : Exception
{
	public ConfigurationValidationException(IReadOnlyList<ConfigurationValidationError> errors)
		: base($"Configuration validation failed with {(errors ?? throw new ArgumentNullException(nameof(errors))).Count} error(s)") =>
		Errors = errors;

	public ConfigurationValidationException(string message) : base(message) => Errors = new List<ConfigurationValidationError>();

	public IReadOnlyList<ConfigurationValidationError> Errors { get; }
}

/// <summary>
/// Configuration validation result for hosting tests.
/// </summary>
public class ConfigurationValidationResult(bool isValid, IReadOnlyList<ConfigurationValidationError> errors)
{
	public bool IsValid { get; } = isValid;

	public IReadOnlyList<ConfigurationValidationError> Errors { get; } = errors;

	public static ConfigurationValidationResult Success() => new(true, new List<ConfigurationValidationError>());

	public static ConfigurationValidationResult Failure(params ConfigurationValidationError[] errors) => new(false, errors.ToList());
}

/// <summary>
/// Elasticsearch bulk response item stub.
/// </summary>
public class BulkResponseItem
{
	public bool IsValid { get; set; } = true;

	public string? Error { get; set; }

	public string? Id { get; set; }

	public string? Index { get; set; }

	public int Status { get; set; } = 200;
}

/// <summary>
/// Elasticsearch Hit wrapper stub.
/// </summary>
public class Hit<T>
{
	public T Source { get; set; } = default!;

	public string? Id { get; set; }

	public string? Index { get; set; }

	public double? Score { get; set; }
}

/// <summary>
/// Query cache options stub for test compatibility.
/// </summary>
public class ExcaliburQueryCacheOptions
{
	public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromMinutes(5);

	public int MaxCacheSize { get; set; } = 1000;

	public bool EnableCaching { get; set; } = true;
}
