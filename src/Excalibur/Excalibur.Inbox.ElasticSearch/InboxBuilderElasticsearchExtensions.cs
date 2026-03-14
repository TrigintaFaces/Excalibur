// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.DependencyInjection;
using Excalibur.Inbox.ElasticSearch;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Elasticsearch provider on <see cref="IInboxBuilder"/>.
/// </summary>
public static class InboxBuilderElasticsearchExtensions
{
	/// <summary>
	/// Configures the inbox to use Elasticsearch storage.
	/// </summary>
	/// <param name="builder">The inbox builder.</param>
	/// <param name="configure">Action to configure the Elasticsearch inbox options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static IInboxBuilder UseElasticSearch(
		this IInboxBuilder builder,
		Action<ElasticsearchInboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddElasticsearchInboxStore(configure);

		return builder;
	}
}
