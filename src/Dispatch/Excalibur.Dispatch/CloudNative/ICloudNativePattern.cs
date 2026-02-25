// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.CloudNative;

/// <summary>
/// Base interface for all cloud-native patterns in the Excalibur Dispatch system. Consolidates patterns that were previously scattered
/// across CloudNative and CloudProviders namespaces.
/// </summary>
public interface ICloudNativePattern
{
	/// <summary>
	/// Gets name of the pattern implementation.
	/// </summary>
	/// <value>
	/// Name of the pattern implementation.
	/// </value>
	string Name { get; }

	/// <summary>
	/// Gets configuration options for the pattern.
	/// </summary>
	/// <value>
	/// Configuration options for the pattern.
	/// </value>
	IReadOnlyDictionary<string, object> Configuration { get; }

	/// <summary>
	/// Gets the current health status of the pattern.
	/// </summary>
	/// <value>
	/// The current health status of the pattern.
	/// </value>
	PatternHealthStatus HealthStatus { get; }

	/// <summary>
	/// Gets metrics about the pattern's performance and usage.
	/// </summary>
	PatternMetrics GetMetrics();

	/// <summary>
	/// Initialize the pattern with the provided configuration.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task InitializeAsync(IReadOnlyDictionary<string, object> configuration, CancellationToken cancellationToken);

	/// <summary>
	/// Start the pattern's operation.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task StartAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Stop the pattern's operation gracefully.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	Task StopAsync(CancellationToken cancellationToken);
}
