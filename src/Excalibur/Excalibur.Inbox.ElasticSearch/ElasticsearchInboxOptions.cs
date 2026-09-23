// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.ComponentModel.DataAnnotations;
using Excalibur.Data.ElasticSearch.Persistence;

namespace Excalibur.Inbox.ElasticSearch;

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
	/// Gets or sets the refresh policy applied after index WRITE operations.
	/// </summary>
	/// <value>
	/// The refresh policy. Defaults to <see cref="ElasticsearchRefreshPolicy.WaitFor"/>, which makes a write
	/// visible to the next search without forcing an immediate refresh.
	/// </value>
	/// <remarks>
	/// <b>The type is an enumeration rather than a string deliberately.</b> As a string it was compared with
	/// ordinal, case-sensitive equality against two literals, so every value matching neither -- a typo, a
	/// differently-cased spelling, a value borrowed from another vendor's vocabulary -- silently selected the
	/// fall-through policy instead of being refused. A misconfiguration that reads as a valid choice is the
	/// failure this type makes inexpressible.
	/// </remarks>
	public ElasticsearchRefreshPolicy RefreshPolicy { get; set; } = ElasticsearchRefreshPolicy.WaitFor;

	/// <summary>
	/// Gets or sets the TTL for processed entries in days.
	/// </summary>
	/// <value>The TTL for processed entries. Set to 0 for no expiration. Defaults to 7 days.</value>
	[Range(0, int.MaxValue)]
	public int RetentionDays { get; set; } = 7;
	/// <summary>
	/// Gets or sets the maximum number of optimistic-concurrency attempts the store makes when a competing
	/// writer invalidates its read between the read and the conditional write.
	/// </summary>
	/// <value>The attempt bound. Defaults to 5. Must be at least 1.</value>
	/// <remarks>
	/// <para>
	/// Each losing attempt writes nothing, so the entry is left exactly as it was found. When every attempt
	/// loses, the store reports <see cref="Excalibur.Dispatch.InboxMarkFailedOutcome.Undecided"/> rather than throwing — it was
	/// asked, it answered, and the call is safe to re-drive.
	/// </para>
	/// <para>
	/// <b>This bound is configuration, not a constant, because the guarantee it governs must be bindable.</b>
	/// While it was hard-coded, the exhaustion branch could be reached only by losing every race in a row,
	/// so no deterministic arm could assert what the store does there — and a bound that makes its own seam's
	/// required arm unwritable is itself the defect. Lowering it narrows the window a caller tolerates before
	/// being told the outcome is undecided; raising it trades latency under contention for fewer undecided
	/// answers.
	/// </para>
	/// </remarks>
	public int MaxConcurrencyRetries { get; set; } = 5;
}
