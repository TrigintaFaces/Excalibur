// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.ElasticSearch.Outbox;

/// <summary>
/// Configuration options for the Elasticsearch outbox store.
/// </summary>
public sealed class ElasticsearchOutboxOptions
{
	/// <summary>
	/// Gets or sets the index name for outbox entries.
	/// </summary>
	/// <value>The index name for outbox entries.</value>
	[Required]
	public string IndexName { get; set; } = "excalibur-outbox";

	/// <summary>
	/// Gets or sets the default batch size for retrieving unsent messages.
	/// </summary>
	/// <value>The default batch size. Defaults to 100.</value>
	[Range(1, 10000)]
	public int DefaultBatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the refresh policy for index operations.
	/// </summary>
	/// <value>The refresh policy. Defaults to "wait_for" for consistency.</value>
	public string RefreshPolicy { get; set; } = "wait_for";

	/// <summary>
	/// Gets or sets the TTL for sent messages in days.
	/// </summary>
	/// <value>The TTL for sent messages. Set to 0 for no expiration. Defaults to 7 days.</value>
	[Range(0, int.MaxValue)]
	public int SentMessageRetentionDays { get; set; } = 7;
}
