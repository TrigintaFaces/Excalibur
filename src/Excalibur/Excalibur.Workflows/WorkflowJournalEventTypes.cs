// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Frozen;

namespace Excalibur.Workflows;

/// <summary>
/// Resolves a stored workflow journal event's type name back to its concrete CLR type. The workflow engine
/// owns this closed set of framework journal types, so it resolves them itself rather than depending on the
/// consumer's event-serializer type registry (which a workflow host is not required to populate).
/// </summary>
/// <remarks>
/// The event store persists the type's <see cref="System.Type.AssemblyQualifiedName"/>; this map is keyed on
/// the assembly-independent <see cref="System.Type.FullName"/> so resolution is stable across assembly
/// version changes — matching is done on the full type name portion of the stored name, ignoring the
/// assembly/version qualifier.
/// </remarks>
internal static class WorkflowJournalEventTypes
{
    private static readonly FrozenDictionary<string, Type> ByFullName =
        new[]
        {
            typeof(WorkflowStarted),
            typeof(ActivityScheduled),
            typeof(ActivityCompleted),
            typeof(ActivityFailed),
            typeof(TimerCreated),
            typeof(TimerFired),
            typeof(WorkflowTimeRead),
            typeof(WorkflowGuidCreated),
            typeof(SignalReceived),
            typeof(WorkflowCompleted),
        }.ToFrozenDictionary(t => t.FullName!, StringComparer.Ordinal);

    /// <summary>
    /// Resolves a stored event type name (assembly-qualified or full) to its concrete journal event type.
    /// </summary>
    /// <param name="storedEventType">The stored <c>StoredEvent.EventType</c> name.</param>
    /// <returns>The concrete CLR type for the journal event.</returns>
    /// <exception cref="InvalidOperationException">The name is not a known journal event type.</exception>
    internal static Type Resolve(string storedEventType)
    {
        // The store persists the assembly-qualified name ("Ns.Type, Assembly, Version=..."); match on the
        // full type name portion (before the first comma) so a version bump does not break replay.
        var comma = storedEventType.IndexOf(',', StringComparison.Ordinal);
        var fullName = comma >= 0 ? storedEventType[..comma].Trim() : storedEventType;

        if (!ByFullName.TryGetValue(fullName, out var type))
        {
            throw new InvalidOperationException(
                $"Unknown workflow journal event type '{storedEventType}'. The instance journal may be "
                + "corrupt or was written by an incompatible engine version.");
        }

        return type;
    }
}
