using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Hosting;

/// <summary>
///     Provides extension methods for adding memory-related health checks to the application's health monitoring system.
/// </summary>
public static class HealthChecksBuilderExtensions
{
	/// <summary>
	///     Adds health checks to monitor process-allocated memory and working set memory.
	/// </summary>
	/// <param name="healthChecks"> The <see cref="IHealthChecksBuilder" /> to configure. </param>
	/// <returns> The configured <see cref="IHealthChecksBuilder" /> for chaining additional health checks. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="healthChecks" /> is null. </exception>
	/// <remarks>
	///     - Ensures process-allocated memory remains below 200MB.
	///     - Ensures working set memory remains below 2GB.
	///     - Thresholds are currently hardcoded but could be made configurable for better flexibility.
	/// </remarks>
	public static IHealthChecksBuilder AddMemoryHealthChecks(this IHealthChecksBuilder healthChecks)
	{
		ArgumentNullException.ThrowIfNull(healthChecks, nameof(healthChecks));

		// Add a health check for process-allocated memory
		_ = healthChecks
			.AddProcessAllocatedMemoryHealthCheck(
				200 * 1024, // 200MB in kilobytes
				"process_allocated_memory");

		// Add a health check for working set memory
		_ = healthChecks
			.AddWorkingSetHealthCheck(
				2L * 1024 * 1024 * 1024, // 2GB in bytes
				"workingset");

		return healthChecks;
	}
}
