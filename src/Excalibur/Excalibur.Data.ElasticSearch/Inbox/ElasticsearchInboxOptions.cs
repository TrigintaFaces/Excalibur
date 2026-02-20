// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.ElasticSearch.Inbox;

/// <summary>
/// Configuration options for the Elasticsearch inbox store.
/// </summary>
public sealed class ElasticsearchInboxOptions
{
	/// <summary>
	/// Gets or sets the index name for inbox entries.
	/// </summary>
	/// <value>The index name for inbox entries.</value>
	[Required]
	public string IndexName { get; set; } = "excalibur-inbox";

	/// <summary>
	/// Gets or sets the refresh policy for index operations.
	/// </summary>
	/// <value>The refresh policy. Defaults to "wait_for" for consistency.</value>
	public string RefreshPolicy { get; set; } = "wait_for";

	/// <summary>
	/// Gets or sets the TTL for processed entries in days.
	/// </summary>
	/// <value>The TTL for processed entries. Set to 0 for no expiration. Defaults to 7 days.</value>
	[Range(0, int.MaxValue)]
	public int RetentionDays { get; set; } = 7;
}
