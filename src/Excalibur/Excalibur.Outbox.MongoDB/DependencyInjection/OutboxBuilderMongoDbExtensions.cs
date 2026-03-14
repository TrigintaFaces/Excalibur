// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.MongoDB;
using Excalibur.Outbox;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring MongoDB outbox stores on <see cref="IOutboxBuilder"/>.
/// </summary>
public static class OutboxBuilderMongoDbExtensions
{
	/// <summary>
	/// Configures the outbox builder to use MongoDB for outbox storage.
	/// </summary>
	/// <param name="builder">The outbox builder.</param>
	/// <param name="configure">Optional action to configure MongoDB outbox options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburOutbox(outbox =&gt;
	/// {
	///     outbox.UseMongoDB(options =&gt;
	///     {
	///         options.ConnectionString = "mongodb://localhost:27017";
	///         options.DatabaseName = "myapp";
	///     });
	/// });
	/// </code>
	/// </example>
	public static IOutboxBuilder UseMongoDB(
		this IOutboxBuilder builder,
		Action<MongoDbOutboxOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddMongoDbOutboxStore(configure ?? (_ => { }));

		return builder;
	}
}
