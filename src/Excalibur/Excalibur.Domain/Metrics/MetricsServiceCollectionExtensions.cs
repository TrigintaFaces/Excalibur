// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

using Excalibur.Domain.Metrics;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering metrics services.
/// </summary>
public static class MetricsServiceCollectionExtensions
{
	/// <summary>
	/// Adds Excalibur metrics services to the service collection.
	/// </summary>
	/// <param name="services"> The service collection to add services Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddExcaliburMetrics(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register IMeterFactory if not already registered
		services.TryAddSingleton<IMeterFactory>(static _ => new DefaultMeterFactory());

		// Register IMetrics as singleton
		services.TryAddSingleton<IMetrics, OpenTelemetryMetrics>();

		return services;
	}

	/// <summary>
	/// Default implementation of IMeterFactory for when OpenTelemetry is not configured.
	/// </summary>
	private sealed class DefaultMeterFactory : IMeterFactory
	{
		private readonly ConcurrentDictionary<string, Meter> _meters = new(StringComparer.Ordinal);
		private volatile bool _disposed;

		public Meter Create(MeterOptions options)
		{
			ArgumentNullException.ThrowIfNull(options);
			ObjectDisposedException.ThrowIf(_disposed, this);
			return _meters.GetOrAdd(options.Name ?? "Default", static (name, version) => new Meter(name, version), options.Version);
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;

			// Snapshot values to avoid concurrent modification during disposal
			var meters = _meters.Values.ToArray();
			_meters.Clear();

			foreach (var meter in meters)
			{
				meter.Dispose();
			}
		}
	}
}
