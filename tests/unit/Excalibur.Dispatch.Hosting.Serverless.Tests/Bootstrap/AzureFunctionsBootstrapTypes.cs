// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;

using Microsoft.Azure.Functions.Worker;

// Deliberate alias to sidestep a name collision between
// Microsoft.Azure.Functions.Worker.TraceContext (abstract base we derive) and
// another TraceContext type pulled in transitively by test dependencies.
using AfTraceContext = Microsoft.Azure.Functions.Worker.TraceContext;

namespace Excalibur.Dispatch.Hosting.Serverless.Tests.Bootstrap;

// Test-only bootstrap shims originally declared inside AzureFunctionsHostProvider
// (bd-15oadf). They are never instantiated by production code — their sole purpose
// was to provide a minimal FunctionContext-compatible surface for behavioural tests
// in AzureFunctionsHostProviderShould. Living in the test project avoids widening
// the prod package's internal/public surface for test-only types, preserves the
// original default behaviour the tests assert against, and lets the bootstrap tests
// use direct instantiation instead of reflection.

/// <summary>
/// Test-only default implementation of <see cref="IInvocationFeatures"/>. Stores
/// features in a type-keyed dictionary; retains the concurrency-friendly
/// <see cref="ConcurrentDictionary{TKey, TValue}"/> from the original prod shape
/// so any behavioural assertions that exercised concurrent set/get still match.
/// </summary>
internal sealed class DefaultInvocationFeatures : IInvocationFeatures
{
	private readonly ConcurrentDictionary<Type, object> _features = new();

	public T? Get<T>()
	{
		if (_features.TryGetValue(typeof(T), out var value) && value is T typed)
		{
			return typed;
		}

		return default;
	}

	public void Set<T>(T instance)
	{
		if (instance is null)
		{
			_ = _features.TryRemove(typeof(T), out _);
			return;
		}

		_features[typeof(T)] = instance;
	}

	public IEnumerator<KeyValuePair<Type, object>> GetEnumerator() => _features.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Test-only default <see cref="AfTraceContext"/>. Emits a synthetic W3C traceparent
/// so distributed-tracing assertions have a non-null value outside a live invocation.
/// </summary>
internal sealed class DefaultTraceContext : AfTraceContext
{
	// W3C traceparent format: "00-<32-hex-traceid>-<16-hex-spanid>-<2-hex-flags>"
	private readonly string _traceParent = BuildTraceParent();

	public override string TraceParent => _traceParent;

	// Base declares TraceState as non-nullable in the test project's resolved
	// Microsoft.Azure.Functions.Worker reference; emit the canonical empty string
	// to preserve "no trace state" semantics without triggering CS8764.
	public override string TraceState => string.Empty;

	private static string BuildTraceParent()
	{
		var traceId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture); // 32 hex
		var spanId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..16]; // 16 hex
		return string.Create(CultureInfo.InvariantCulture, $"00-{traceId}-{spanId}-00");
	}
}

/// <summary>
/// Test-only default <see cref="RetryContext"/> — reports zero retries attempted
/// against the default max-retry policy (3).
/// </summary>
internal sealed class DefaultRetryContext : RetryContext
{
	public override int RetryCount => 0;

	public override int MaxRetryCount => 3;
}

/// <summary>
/// Test-only default <see cref="FunctionContext"/>. The <see cref="Items"/>
/// <i>property</i> is read-only (assigning via the property setter throws
/// <see cref="NotSupportedException"/>), but the returned dictionary instance is
/// stable and mutable — matching the Azure Functions
/// <see cref="FunctionContext.Items"/> contract of a per-invocation mutable bag.
/// Regression guard for bd-2d8fjd: previously returned a fresh dictionary on every
/// get, silently losing any writes.
/// </summary>
internal sealed class DefaultFunctionContext : FunctionContext
{
	private readonly Dictionary<object, object> _items = new();

	public override string InvocationId { get; } = Guid.NewGuid().ToString();

	public override string FunctionId =>
		Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") ?? "DefaultFunction";

	/// <summary>
	/// Gets the function name, derived from WEBSITE_SITE_NAME when running under
	/// Azure Functions and falling back to a default otherwise.
	/// </summary>
	public string FunctionName =>
		Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") ?? "DefaultFunction";

	public override AfTraceContext TraceContext { get; } = new DefaultTraceContext();

	public override BindingContext BindingContext => null!;

	public override RetryContext RetryContext { get; } = new DefaultRetryContext();

	public override IServiceProvider InstanceServices { get; set; } = null!;

	public override FunctionDefinition FunctionDefinition => null!;

	public override IDictionary<object, object> Items
	{
		get => _items;
		set => throw new NotSupportedException(
			"DefaultFunctionContext is a read-only bootstrap context; Items cannot be assigned.");
	}

	public override IInvocationFeatures Features => null!;
}
