// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.DependencyInjection;
using Excalibur.Inbox.Postgres;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Postgres provider on <see cref="IInboxBuilder"/>.
/// </summary>
public static class InboxBuilderPostgresExtensions
{
	/// <summary>
	/// Configures the inbox to use Postgres storage.
	/// </summary>
	/// <param name="builder">The inbox builder.</param>
	/// <param name="configure">Action to configure the Postgres inbox options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static IInboxBuilder UsePostgres(
		this IInboxBuilder builder,
		Action<PostgresInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddPostgresInboxStore(configure);

		return builder;
	}

	/// <summary>
	/// Configures the inbox to use Postgres storage with a connection string.
	/// </summary>
	/// <param name="builder">The inbox builder.</param>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static IInboxBuilder UsePostgres(
		this IInboxBuilder builder,
		string connectionString)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		_ = builder.Services.AddPostgresInboxStore(connectionString);

		return builder;
	}
}
