// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Excalibur.Data.Abstractions.Execution;
using Excalibur.Data.Postgres.Execution;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Postgres data executors.
/// </summary>
public static class PostgresDataExecutorServiceCollectionExtensions
{
	public static IServiceCollection AddPostgresDataExecutors(
		this IServiceCollection services,
		Func<IDbConnection> connectionFactory)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactory);

		services.TryAddTransient(_ => connectionFactory());
		services.TryAddTransient<IDataExecutor, PostgresDataExecutor>();
		services.TryAddTransient<IQueryExecutor, PostgresQueryExecutor>();
		return services;
	}
}
