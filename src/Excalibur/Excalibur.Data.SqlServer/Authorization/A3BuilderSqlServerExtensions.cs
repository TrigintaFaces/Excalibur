// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.A3;
using Excalibur.Data.SqlServer.Authorization;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring SQL Server stores on <see cref="IA3Builder"/>.
/// </summary>
public static class A3BuilderSqlServerExtensions
{
	/// <summary>
	/// Registers SQL Server grant and activity group stores, with the activity-group table in the default <c>authz</c> schema.
	/// </summary>
	/// <param name="builder">The A3 builder.</param>
	/// <returns>The builder for chaining.</returns>
	public static IA3Builder UseSqlServer(this IA3Builder builder) => builder.UseSqlServer(static _ => { });

	/// <summary>
	/// Registers SQL Server grant and activity group stores, configuring where the activity-group table lives.
	/// </summary>
	/// <param name="builder">The A3 builder.</param>
	/// <param name="configure">Configures the stores; see <see cref="SqlServerAuthorizationOptions"/>.</param>
	/// <returns>The builder for chaining.</returns>
	/// <remarks>
	/// Two applications that share a database must each configure their own
	/// <see cref="SqlServerAuthorizationOptions.SchemaName"/>: the tables carry no application identifier, so a
	/// catalogue replace applies to every row of the activity-group table. An invalid schema name is
	/// refused at startup.
	/// </remarks>
	public static IA3Builder UseSqlServer(this IA3Builder builder, Action<SqlServerAuthorizationOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddOptions<SqlServerAuthorizationOptions>()
			.Configure(configure)
			.ValidateOnStart();

		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SqlServerAuthorizationOptions>, SqlServerAuthorizationOptionsValidator>());

		return builder
			.UseGrantStore<SqlServerGrantStore>()
			.UseActivityGroupStore<SqlServerActivityGroupStore>();
	}
}
