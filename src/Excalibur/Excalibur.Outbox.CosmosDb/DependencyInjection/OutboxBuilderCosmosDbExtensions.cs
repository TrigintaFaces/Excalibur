// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox;
using Excalibur.Outbox.CosmosDb;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Cosmos DB outbox stores on <see cref="IOutboxBuilder"/>.
/// </summary>
public static class OutboxBuilderCosmosDbExtensions
{
	/// <summary>
	/// Configures the outbox builder to use Azure Cosmos DB for outbox storage.
	/// </summary>
	/// <param name="builder">The outbox builder.</param>
	/// <param name="configure">Optional action to configure Cosmos DB outbox options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburOutbox(outbox =&gt;
	/// {
	///     outbox.UseCosmosDb(options =&gt;
	///     {
	///         options.ConnectionString = "AccountEndpoint=...;AccountKey=...";
	///         options.DatabaseName = "myapp";
	///         options.ContainerName = "outbox";
	///     });
	/// });
	/// </code>
	/// </example>
	public static IOutboxBuilder UseCosmosDb(
		this IOutboxBuilder builder,
		Action<CosmosDbOutboxOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		if (configure is not null)
		{
			_ = builder.Services.AddCosmosDbOutboxStore(configure);
		}
		else
		{
			_ = builder.Services.AddCosmosDbOutboxStore(_ => { });
		}

		return builder;
	}
}
