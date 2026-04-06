// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using System.Diagnostics.CodeAnalysis;
using Excalibur.Data.SqlServer.ErrorHandling;
using Excalibur.Dispatch.ErrorHandling;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering SQL Server data services.
/// </summary>
public static class SqlServerDataServiceCollectionExtensions
{
	/// <summary>
	/// Registers provider-neutral data execution/query interfaces with SQL Server implementations. Caller supplies an IDbConnection factory
	/// (scoped/transient) via DI.
	/// </summary>
	public static IServiceCollection AddSqlServerDataExecutors(
		this IServiceCollection services,
		Func<IDbConnection> connectionFactory)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactory);

		services.TryAddTransient(_ => connectionFactory());
		return services;
	}

	/// <summary>
	/// Registers the SQL Server dead letter store.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSqlServerDeadLetterStore(
		this IServiceCollection services,
		string connectionString)
	{
		return services.AddSqlServerDeadLetterStore(options => options.ConnectionString = connectionString);
	}

	/// <summary>
	/// Registers the SQL Server dead letter store with full configuration options.
	/// Uses IOptions pattern for configuration consistency with other SqlServer stores.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the dead letter options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSqlServerDeadLetterStore(
		this IServiceCollection services,
		Action<SqlServerDeadLetterOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		// Register options via IOptions pattern with ValidateOnStart
		_ = services.AddOptions<SqlServerDeadLetterOptions>()
			.Configure(configure)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SqlServerDeadLetterOptions>, SqlServerDeadLetterOptionsValidator>());

		// Register dead letter store (uses IOptions pattern via DI)
		services.TryAddSingleton<SqlServerDeadLetterStore>();
		services.TryAddSingleton<IDeadLetterStore>(sp => sp.GetRequiredService<SqlServerDeadLetterStore>());
		services.TryAddSingleton<IDeadLetterStoreAdmin>(sp => sp.GetRequiredService<SqlServerDeadLetterStore>());

		return services;
	}

	/// <summary>
	/// Registers the SQL Server dead letter store using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind options from.</param>
	/// <returns>The service collection for chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddSqlServerDeadLetterStore(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		// Register options via IOptions pattern with ValidateOnStart
		_ = services.AddOptions<SqlServerDeadLetterOptions>()
			.Bind(configuration)
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SqlServerDeadLetterOptions>, SqlServerDeadLetterOptionsValidator>());

		// Register dead letter store (uses IOptions pattern via DI)
		services.TryAddSingleton<SqlServerDeadLetterStore>();
		services.TryAddSingleton<IDeadLetterStore>(sp => sp.GetRequiredService<SqlServerDeadLetterStore>());
		services.TryAddSingleton<IDeadLetterStoreAdmin>(sp => sp.GetRequiredService<SqlServerDeadLetterStore>());

		return services;
	}
}
