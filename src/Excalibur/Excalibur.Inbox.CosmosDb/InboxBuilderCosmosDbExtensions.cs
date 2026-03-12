// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.CosmosDb;
using Excalibur.Inbox.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring CosmosDB provider on <see cref="IInboxBuilder"/>.
/// </summary>
public static class InboxBuilderCosmosDbExtensions
{
	/// <summary>
	/// Configures the inbox to use Azure Cosmos DB storage.
	/// </summary>
	/// <param name="builder">The inbox builder.</param>
	/// <param name="configure">Action to configure the CosmosDB inbox options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static IInboxBuilder UseCosmosDb(
		this IInboxBuilder builder,
		Action<CosmosDbInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddCosmosDbInboxStore(configure);

		return builder;
	}
}
