// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch;

/// <summary>
/// Represents a dead letter document stored in Elasticsearch.
/// </summary>
/// <typeparam name="T"> The type of the original document. </typeparam>
public sealed class DeadLetterDocument<T>
	where T : class
{
	/// <summary>
	/// Gets or sets the original document that failed processing.
	/// </summary>
	/// <value>
	/// The original document that failed processing.
	/// </value>
	public T? OriginalDocument { get; set; }

	/// <summary>
	/// Gets or sets the error message.
	/// </summary>
	/// <value>
	/// The error message.
	/// </value>
	public string ErrorMessage { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the error type.
	/// </summary>
	/// <value>
	/// The error type.
	/// </value>
	public string ErrorType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the timestamp when the dead letter was created.
	/// </summary>
	/// <value>
	/// The timestamp when the dead letter was created.
	/// </value>
	public DateTimeOffset Timestamp { get; set; }

	/// <summary>
	/// Gets or sets the number of retry attempts.
	/// </summary>
	/// <value>
	/// The number of retry attempts.
	/// </value>
	public int RetryCount { get; set; }
}
