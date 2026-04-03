// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Jobs.Abstractions;

namespace Excalibur.Jobs.Core;

/// <summary>
/// Factory interface for creating instances of <see cref="IJobOptionsHostedWatcherService" />.
/// </summary>
/// <remarks>
/// This factory is responsible for instantiating hosted watcher services for configurable jobs, ensuring that jobs and their configurations
/// are managed appropriately at runtime.
/// </remarks>
public interface IJobOptionsHostedWatcherServiceFactory
{
	/// <summary>
	/// Creates an instance of <see cref="IJobOptionsHostedWatcherService" /> for the specified job and configuration type.
	/// </summary>
	/// <typeparam name="TJob"> The type of the job to be managed by the hosted watcher service. Must implement <see cref="IConfigurableJob{TOptions}" />. </typeparam>
	/// <typeparam name="TOptions"> The type of the job configuration. Must implement <see cref="IJobOptions" />. </typeparam>
	/// <returns> A new instance of <see cref="IJobOptionsHostedWatcherService" /> configured for the specified job and configuration. </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown if the necessary dependencies or parameters required to create the service are null.
	/// </exception>
	Task<IJobOptionsHostedWatcherService> CreateAsync<TJob, TOptions>()
		where TJob : IConfigurableJob<TOptions>
		where TOptions : class, IJobOptions;
}
