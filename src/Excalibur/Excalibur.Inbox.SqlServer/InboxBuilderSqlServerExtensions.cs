// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.DependencyInjection;
using Excalibur.Inbox.SqlServer;

using Microsoft.Data.SqlClient;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring SQL Server provider on <see cref="IInboxBuilder"/>.
/// </summary>
public static class InboxBuilderSqlServerExtensions
{
	/// <summary>
	/// Configures the inbox to use SQL Server storage.
	/// </summary>
	/// <param name="builder">The inbox builder.</param>
	/// <param name="configure">Action to configure the SQL Server inbox options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static IInboxBuilder UseSqlServer(
		this IInboxBuilder builder,
		Action<SqlServerInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddSqlServerInboxStore(configure);

		return builder;
	}

	/// <summary>
	/// Configures the inbox to use SQL Server storage with a connection string.
	/// </summary>
	/// <param name="builder">The inbox builder.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static IInboxBuilder UseSqlServer(
		this IInboxBuilder builder,
		string connectionString)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		_ = builder.Services.AddSqlServerInboxStore(connectionString);

		return builder;
	}

	/// <summary>
	/// Configures the inbox to use SQL Server storage with a connection factory.
	/// </summary>
	/// <param name="builder">The inbox builder.</param>
	/// <param name="connectionFactoryProvider">A factory function that creates SQL connections.</param>
	/// <param name="configure">Action to configure the SQL Server inbox options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static IInboxBuilder UseSqlServer(
		this IInboxBuilder builder,
		Func<IServiceProvider, Func<SqlConnection>> connectionFactoryProvider,
		Action<SqlServerInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(connectionFactoryProvider);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddSqlServerInboxStore(connectionFactoryProvider, configure);

		return builder;
	}
}
