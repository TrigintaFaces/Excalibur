// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch;

/// <summary>
/// Configuration options for the Elasticsearch dead letter handler.
/// </summary>
public sealed class ElasticsearchDeadLetterOptions
{
	/// <summary>
	/// Gets or sets the prefix for dead letter indices.
	/// </summary>
	/// <value>
	/// The prefix for dead letter indices.
	/// </value>
	public string DeadLetterIndexPrefix { get; set; } = "dead-letters";

	/// <summary>
	/// Gets or sets the maximum retry count before giving up.
	/// </summary>
	/// <value>
	/// The maximum retry count before giving up.
	/// </value>
	public int MaxRetryCount { get; set; } = 3;

	/// <summary>
	/// Gets or sets the retention period for dead letter documents.
	/// </summary>
	/// <value>
	/// The retention period for dead letter documents.
	/// </value>
	public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(30);
}
