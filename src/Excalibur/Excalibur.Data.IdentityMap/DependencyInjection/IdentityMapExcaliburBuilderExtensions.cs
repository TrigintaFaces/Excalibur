// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.IdentityMap.Builders;
using Excalibur.Hosting.Builders;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring identity map store on <see cref="IExcaliburBuilder"/>.
/// </summary>
public static class IdentityMapExcaliburBuilderExtensions
{
	/// <summary>
	/// Adds identity map store services to the Excalibur builder.
	/// </summary>
	/// <param name="builder">The Excalibur builder.</param>
	/// <param name="configure">The identity map builder configuration action.</param>
	/// <returns>The Excalibur builder for method chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddExcalibur(excalibur =>
	/// {
	///     excalibur.AddIdentityMap(identity =>
	///     {
	///         identity.UseSqlServer(sql =>
	///         {
	///             sql.ConnectionString(connectionString);
	///         });
	///     });
	/// });
	/// </code>
	/// </example>
	public static IExcaliburBuilder AddIdentityMap(
		this IExcaliburBuilder builder,
		Action<IIdentityMapBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		builder.Services.AddIdentityMap(configure);

		return builder;
	}
}
