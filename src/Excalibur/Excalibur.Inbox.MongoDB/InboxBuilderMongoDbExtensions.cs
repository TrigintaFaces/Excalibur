// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.DependencyInjection;
using Excalibur.Inbox.MongoDB;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring MongoDB provider on <see cref="IInboxBuilder"/>.
/// </summary>
public static class InboxBuilderMongoDbExtensions
{
	/// <summary>
	/// Configures the inbox to use MongoDB storage.
	/// </summary>
	/// <param name="builder">The inbox builder.</param>
	/// <param name="configure">Action to configure the MongoDB inbox options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static IInboxBuilder UseMongoDB(
		this IInboxBuilder builder,
		Action<MongoDbInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddMongoDbInboxStore(configure);

		return builder;
	}
}
