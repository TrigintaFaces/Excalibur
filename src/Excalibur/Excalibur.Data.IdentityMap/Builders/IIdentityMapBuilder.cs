// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.IdentityMap.Builders;

/// <summary>
/// Fluent builder interface for configuring the identity map store.
/// </summary>
/// <remarks>
/// <para>
/// Provider-specific extensions (e.g., <c>UseSqlServer</c>) are provided as extension methods
/// on this interface by the corresponding provider package.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddIdentityMap(identity =>
/// {
///     identity.UseSqlServer(sql =>
///     {
///         sql.ConnectionString(connectionString)
///            .SchemaName("dbo")
///            .TableName("IdentityMap");
///     });
/// });
/// </code>
/// </example>
public interface IIdentityMapBuilder
{
	/// <summary>
	/// Gets the service collection being configured.
	/// </summary>
	/// <value>The <see cref="IServiceCollection"/>.</value>
	IServiceCollection Services { get; }
}
