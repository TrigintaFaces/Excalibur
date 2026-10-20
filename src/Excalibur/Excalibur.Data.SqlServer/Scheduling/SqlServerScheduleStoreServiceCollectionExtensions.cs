// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.SqlServer.Scheduling;
using Excalibur.Dispatch.Delivery;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the configured durable SQL Server schedule authority.</summary>
public static class SqlServerScheduleStoreServiceCollectionExtensions
{
	/// <summary>Configures SQL Server storage and its inseparable schedule durability attestation.</summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Connection and lease configuration.</param>
	/// <returns>The service collection.</returns>
	/// <remarks>
	/// Provision the shipped schedule schema first. Resolve IScheduleStore and cast that same authority for execution capabilities.
	/// Connection and timeout settings are validated at host startup without connecting to the database.
	/// </remarks>
	public static IServiceCollection AddSqlServerScheduleStore(this IServiceCollection services,
		Action<SqlServerScheduleStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);
		_ = services.AddOptions<SqlServerScheduleStoreOptions>()
			.Configure(configure)
			.Validate(static options => !string.IsNullOrWhiteSpace(options.ConnectionString),
				"ConnectionString must not be blank.")
			.Validate(static options => options.LeaseDuration > TimeSpan.Zero,
				"LeaseDuration must be positive.")
			.Validate(static options => options.CommandTimeoutSeconds > 0,
				"CommandTimeoutSeconds must be positive and bounded.")
			.Validate(static options => options.LockTimeoutMilliseconds >= 0,
				"LockTimeoutMilliseconds must be nonnegative and bounded.")
			.ValidateOnStart();
		return services.AddDurableScheduleStore<SqlServerScheduleStore>();
	}
}
