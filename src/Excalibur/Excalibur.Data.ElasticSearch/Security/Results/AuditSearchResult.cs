// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents the result of an audit event search operation.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AuditSearchResult" /> class.
/// </remarks>
/// <param name="searchId"> The unique identifier for this search operation. </param>
/// <param name="totalMatches"> The total number of events matching the search criteria. </param>
/// <param name="events"> The audit events returned by the search. </param>
public sealed class AuditSearchResult(Guid searchId, long totalMatches, IEnumerable<SecurityAuditEvent> events)
{
	/// <summary>
	/// Gets the unique identifier for this search operation.
	/// </summary>
	/// <value>
	/// The unique identifier for this search operation.
	/// </value>
	public Guid SearchId { get; } = searchId;

	/// <summary>
	/// Gets the total number of events matching the search criteria.
	/// </summary>
	/// <value>
	/// The total number of events matching the search criteria.
	/// </value>
	public long TotalMatches { get; } = totalMatches;

	/// <summary>
	/// Gets the audit events returned by the search.
	/// </summary>
	/// <value>
	/// The audit events returned by the search.
	/// </value>
	public IReadOnlyList<SecurityAuditEvent> Events { get; } = events?.ToList() ?? throw new ArgumentNullException(nameof(events));

	/// <summary>
	/// Gets the timestamp when this search was performed.
	/// </summary>
	/// <value>
	/// The timestamp when this search was performed.
	/// </value>
	public DateTimeOffset SearchedAt { get; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets the search execution time in milliseconds.
	/// </summary>
	/// <value>
	/// The search execution time in milliseconds.
	/// </value>
	public long ExecutionTimeMs { get; init; }

	/// <summary>
	/// Gets any warnings encountered during the search.
	/// </summary>
	/// <value>
	/// Any warnings encountered during the search.
	/// </value>
	public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

	/// <summary>
	/// Gets the search result metadata.
	/// </summary>
	/// <value>
	/// The search result metadata.
	/// </value>
	public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>(StringComparer.Ordinal);

	/// <summary>
	/// Gets a value indicating whether the search results were truncated.
	/// </summary>
	/// <value>
	/// A value indicating whether the search results were truncated.
	/// </value>
	public bool IsTruncated { get; init; }

	/// <summary>
	/// Gets the search continuation token for pagination.
	/// </summary>
	/// <value>
	/// The search continuation token for pagination.
	/// </value>
	public string? ContinuationToken { get; init; }
}
