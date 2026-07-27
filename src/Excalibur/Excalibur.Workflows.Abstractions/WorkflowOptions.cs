// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization.Metadata;

namespace Excalibur.Workflows;

/// <summary>
/// Configuration options for durable workflow execution.
/// </summary>
public sealed class WorkflowOptions
{
	/// <summary>
	/// Gets or sets the maximum number of journal events a single workflow instance may accumulate before
	/// replay is rejected, guarding against unbounded histories.
	/// </summary>
	/// <value>The maximum replay event count. Defaults to <c>10,000</c>.</value>
	public int MaxReplayEvents { get; set; } = 10_000;

	/// <summary>
	/// Gets or sets a value indicating whether a failed activity's exception message is captured verbatim in
	/// the durable journal.
	/// </summary>
	/// <remarks>
	/// When an activity throws, the failure is recorded to the durable workflow journal so the fault is
	/// replayed deterministically. By default the exception's message is stored verbatim, which aids
	/// diagnosis but means any PII or secret an activity surfaces in its exception text is durably persisted.
	/// Set this to <see langword="false"/> to store a fixed redacted placeholder instead of the message,
	/// keeping sensitive text out of the journal; the failure is still recorded and replayed, only the
	/// message text is withheld.
	/// </remarks>
	/// <value><see langword="true"/> to store the exception message verbatim (default); otherwise
	/// <see langword="false"/> to store a redacted placeholder.</value>
	public bool CaptureActivityFailureDetails { get; set; } = true;

	/// <summary>
	/// Gets or sets a source-generated JSON type-info resolver for the consumer's activity and workflow
	/// payload types, enabling a fully trimming- and native-AOT-safe payload serialization path.
	/// </summary>
	/// <remarks>
	/// Activity inputs/results and workflow inputs/results are arbitrary consumer types the framework cannot
	/// source-generate. Set this to a consumer <see cref="System.Text.Json.Serialization.JsonSerializerContext"/>
	/// (or any <see cref="IJsonTypeInfoResolver"/>) covering those payload types to serialize them without
	/// reflection under trimming/AOT. When left <see langword="null"/>, payloads serialize with reflection-based
	/// <see cref="System.Text.Json.JsonSerializer"/>, which works but is not trim/AOT-safe for unknown types.
	/// </remarks>
	/// <value>The consumer payload resolver, or <see langword="null"/> to use the reflection default.</value>
	public IJsonTypeInfoResolver? PayloadTypeInfoResolver { get; set; }
}
