// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing;

/// <summary>
/// Thrown when a materialized view replay reaches an event it cannot interpret, and halts rather than
/// advancing past it.
/// </summary>
/// <remarks>
/// Advancing past an un-interpretable event would leave a permanent, silent gap in the view: it would read
/// as healthy and be wrong, and nothing would report it. A halted view is loudly stopped and recoverable,
/// so that is the failure the framework chooses.
///
/// This is not a special rule for views. Every other replay loop in this package already halts at an event
/// it cannot interpret, and treats an erasure tombstone as the only structural skip -- a deserialization
/// failure is never read as "assume erased", because that masks genuine corruption as erasure.
///
/// Recovery is a registration, not a code change. When the stored name belongs to a type that has since
/// moved namespace or assembly, register the old name against the current type; to deliberately pass over
/// an event, register the name against a type no view builder handles, which routes nowhere and applies
/// nothing. Both are per-type and declared in configuration, rather than a global switch that would also
/// license skipping the next genuine corruption.
/// </remarks>
public sealed class MaterializedViewPoisonEventException : InvalidOperationException
{
	/// <summary> Initializes a new instance of the <see cref="MaterializedViewPoisonEventException"/> class. </summary>
	public MaterializedViewPoisonEventException()
	{
	}

	/// <summary> Initializes a new instance of the <see cref="MaterializedViewPoisonEventException"/> class. </summary>
	/// <param name="message"> The message that describes the error. </param>
	public MaterializedViewPoisonEventException(string message)
		: base(message)
	{
	}

	/// <summary> Initializes a new instance of the <see cref="MaterializedViewPoisonEventException"/> class. </summary>
	/// <param name="message"> The message that describes the error. </param>
	/// <param name="innerException"> The exception that caused this one. </param>
	public MaterializedViewPoisonEventException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary> Initializes a new instance of the <see cref="MaterializedViewPoisonEventException"/> class. </summary>
	/// <param name="viewName"> The view whose replay halted, or <see langword="null"/> during a rebuild of all views. </param>
	/// <param name="eventId"> The identifier of the event that could not be interpreted. </param>
	/// <param name="eventType"> The stored type name, verbatim, as it appears in the event store. </param>
	/// <param name="globalPosition"> The global stream position the replay halted at. </param>
	/// <param name="innerException"> The failure raised while resolving or deserializing the event. </param>
	public MaterializedViewPoisonEventException(
		string? viewName,
		string eventId,
		string eventType,
		long globalPosition,
		Exception innerException)
		: base(
			$"Materialized view replay halted at global position {globalPosition}: event '{eventId}' has stored type "
			+ $"'{eventType}', which cannot be resolved or deserialized"
			+ (viewName is null ? "." : $" (view '{viewName}').")
			+ " The replay did NOT advance past it, because doing so would leave a permanent gap in a view that "
			+ "would then read as healthy while being wrong. Register the stored type name against the type it "
			+ "belongs to now if it moved namespace or assembly, or against a type no builder handles to pass over "
			+ "it deliberately.",
			innerException)
	{
		ViewName = viewName;
		EventId = eventId;
		EventType = eventType;
		GlobalPosition = globalPosition;
	}

	/// <summary> Gets the view whose replay halted, or <see langword="null"/> during a rebuild of all views. </summary>
	public string? ViewName { get; }

	/// <summary> Gets the identifier of the event that could not be interpreted. </summary>
	public string? EventId { get; }

	/// <summary> Gets the stored type name, verbatim, as it appears in the event store. </summary>
	public string? EventType { get; }

	/// <summary> Gets the global stream position the replay halted at. </summary>
	public long GlobalPosition { get; }
}
