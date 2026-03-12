// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox;
using Excalibur.Outbox.ElasticSearch;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Elasticsearch provider on <see cref="IOutboxBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide fluent provider selection by adding
/// provider-specific configuration to the core <see cref="IOutboxBuilder"/> interface.
/// </para>
/// </remarks>
public static class OutboxBuilderElasticsearchExtensions
{
	/// <summary>
	/// Configures the outbox to use Elasticsearch storage.
	/// </summary>
	/// <param name="builder">The outbox builder.</param>
	/// <param name="configure">Action to configure the Elasticsearch outbox options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburOutbox(outbox =&gt;
	/// {
	///     outbox.UseElasticSearch(options =&gt;
	///     {
	///         options.ConnectionString = "http://localhost:9200";
	///         options.IndexName = "outbox-messages";
	///     })
	///     .EnableBackgroundProcessing();
	/// });
	/// </code>
	/// </example>
	public static IOutboxBuilder UseElasticSearch(
		this IOutboxBuilder builder,
		Action<ElasticsearchOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddElasticsearchOutboxStore(configure);

		return builder;
	}
}
