// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.A3;
using Excalibur.Data.SqlServer.Authorization;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring SQL Server stores on <see cref="IA3Builder"/>.
/// </summary>
public static class A3BuilderSqlServerExtensions
{
	/// <summary>
	/// Registers SQL Server grant and activity group stores.
	/// </summary>
	/// <param name="builder">The A3 builder.</param>
	/// <returns>The builder for chaining.</returns>
	public static IA3Builder UseSqlServer(this IA3Builder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder
			.UseGrantStore<SqlServerGrantStore>()
			.UseActivityGroupStore<SqlServerActivityGroupStore>();
	}
}
