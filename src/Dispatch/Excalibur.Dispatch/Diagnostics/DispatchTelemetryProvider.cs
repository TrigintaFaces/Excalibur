// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Diagnostics;

/// <summary>
/// Default implementation of IDispatchTelemetryProvider.
/// </summary>
internal sealed class DispatchTelemetryProvider : IDispatchTelemetryProvider, IDisposable
{
	private readonly DispatchTelemetryOptions _options;
	private readonly IMeterFactory? _meterFactory;
	private readonly ConcurrentDictionary<string, ActivitySource> _activitySources = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<string, Meter> _meters = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<string, ActivitySource> _noOpActivitySources = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<string, Meter> _noOpMeters = new(StringComparer.Ordinal);
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="DispatchTelemetryProvider" /> class.
	/// </summary>
	/// <param name="options"> The telemetry options. </param>
	public DispatchTelemetryProvider(IOptions<DispatchTelemetryOptions> options)
		: this(options, meterFactory: null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DispatchTelemetryProvider" /> class using an <see cref="IMeterFactory"/> for DI-managed meter lifecycle.
	/// </summary>
	/// <param name="options"> The telemetry options. </param>
	/// <param name="meterFactory"> Optional meter factory for DI-managed meter lifecycle. If null, creates unmanaged meters. </param>
	public DispatchTelemetryProvider(IOptions<DispatchTelemetryOptions> options, IMeterFactory? meterFactory)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_options.Validate();
		_meterFactory = meterFactory;
	}

	/// <inheritdoc />
	public ActivitySource GetActivitySource(string componentName)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(componentName);

		if (!_options.EnableTracing)
		{
			// Return a cached no-op ActivitySource when tracing is disabled
			return _noOpActivitySources.GetOrAdd(componentName,
				name => new ActivitySource($"{name}.NoOp", _options.ServiceVersion));
		}

		return _activitySources.GetOrAdd(componentName, name => new ActivitySource(name, _options.ServiceVersion));
	}

	/// <inheritdoc />
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "Meters are cached in ConcurrentDictionary and disposed in Dispose()")]
	public Meter GetMeter(string componentName)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		ArgumentNullException.ThrowIfNull(componentName);

		if (!_options.EnableMetrics)
		{
			// Return a cached no-op meter that doesn't collect metrics
			return _noOpMeters.GetOrAdd(componentName,
				name => _meterFactory?.Create(name) ?? new Meter(name, _options.ServiceVersion));
		}

		return _meters.GetOrAdd(componentName,
			name => _meterFactory?.Create(name) ?? new Meter(name, _options.ServiceVersion));
	}

	/// <inheritdoc />
	public DispatchTelemetryOptions GetOptions()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		return _options;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		// Snapshot before clearing to avoid TOCTOU
		var activitySources = _activitySources.Values.ToArray();
		var meters = _meters.Values.ToArray();
		var noOpActivitySources = _noOpActivitySources.Values.ToArray();
		var noOpMeters = _noOpMeters.Values.ToArray();

		_activitySources.Clear();
		_meters.Clear();
		_noOpActivitySources.Clear();
		_noOpMeters.Clear();

		foreach (var activitySource in activitySources)
		{
			activitySource.Dispose();
		}

		foreach (var activitySource in noOpActivitySources)
		{
			activitySource.Dispose();
		}

		foreach (var meter in meters)
		{
			meter.Dispose();
		}

		foreach (var meter in noOpMeters)
		{
			meter.Dispose();
		}
	}
}
