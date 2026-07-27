// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Caching.AdaptiveTtl;

/// <summary>
/// Default <see cref="ISystemLoadMonitor"/> that derives a normalized system-load signal from
/// managed thread-pool saturation.
/// </summary>
/// <remarks>
/// <para>
/// The .NET BCL exposes no single "system load 0..1" primitive, so this default uses the worker
/// thread-pool utilization — <c>(max - available) / max</c> — as a dependency-free, allocation-free,
/// non-throwing proxy for process pressure. It is intentionally lightweight (no CPU-counter sampling
/// or platform interop) so it is safe to call on the hot caching path.
/// </para>
/// <para>
/// Consumers needing a more precise signal (CPU counters, container cgroup limits, custom metrics)
/// can register their own <see cref="ISystemLoadMonitor"/> before calling
/// <c>AddAdaptiveTtlCache</c>; the default is only registered via <c>TryAdd</c>.
/// </para>
/// </remarks>
internal sealed class DefaultSystemLoadMonitor : ISystemLoadMonitor
{
	/// <inheritdoc />
	public Task<double> GetCurrentLoadAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		ThreadPool.GetMaxThreads(out var maxWorkerThreads, out _);
		ThreadPool.GetAvailableThreads(out var availableWorkerThreads, out _);

		if (maxWorkerThreads <= 0)
		{
			return Task.FromResult(0.0);
		}

		var busyWorkerThreads = maxWorkerThreads - availableWorkerThreads;
		var load = (double)busyWorkerThreads / maxWorkerThreads;

		return Task.FromResult(Math.Clamp(load, 0.0, 1.0));
	}
}
