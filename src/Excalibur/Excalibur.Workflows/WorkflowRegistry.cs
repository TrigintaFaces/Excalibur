// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Frozen;

namespace Excalibur.Workflows;

/// <summary>
/// A registered workflow: its name, definition version, and body. Registered as a DI singleton and
/// aggregated by <see cref="WorkflowRegistry"/>.
/// </summary>
/// <param name="Name">The workflow name.</param>
/// <param name="Version">The definition version. Versions start at 1.</param>
/// <param name="Body">The workflow body delegate.</param>
internal sealed record WorkflowDescriptor(string Name, int Version, WorkflowBody Body);

/// <summary>
/// Resolves registered workflow definitions by (name, version) to their <see cref="WorkflowBody"/>
/// delegates. Built once from the registered <see cref="WorkflowDescriptor"/> set at composition time as an
/// explicit static map — no reflection — so version resolution is native-AOT-safe.
/// </summary>
/// <remarks>
/// Multiple versions of the same workflow name may be registered simultaneously. A new instance binds the
/// latest registered version (<see cref="ResolveLatest"/>); an in-flight instance replays against the exact
/// version pinned in its journal (<see cref="Resolve"/>), so a definition upgrade never changes how an
/// already-running instance replays.
/// </remarks>
internal sealed class WorkflowRegistry
{
    private readonly FrozenDictionary<(string Name, int Version), WorkflowBody> _bodies;
    private readonly FrozenDictionary<string, int> _latestVersionByName;

    public WorkflowRegistry(IEnumerable<WorkflowDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        var map = new Dictionary<(string, int), WorkflowBody>();
        var latest = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var descriptor in descriptors)
        {
            if (descriptor.Version < 1)
            {
                throw new InvalidOperationException(
                    $"Workflow '{descriptor.Name}' has invalid definition version {descriptor.Version}; "
                    + "versions start at 1.");
            }

            if (!map.TryAdd((descriptor.Name, descriptor.Version), descriptor.Body))
            {
                throw new InvalidOperationException(
                    $"Workflow '{descriptor.Name}' version {descriptor.Version} is registered more than once.");
            }

            if (!latest.TryGetValue(descriptor.Name, out var current) || descriptor.Version > current)
            {
                latest[descriptor.Name] = descriptor.Version;
            }
        }

        _bodies = map.ToFrozenDictionary();
        _latestVersionByName = latest.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>
    /// Resolves the latest registered version of a workflow — the version a new instance binds.
    /// </summary>
    /// <param name="name">The workflow name.</param>
    /// <returns>The latest registered version and its <see cref="WorkflowBody"/>.</returns>
    /// <exception cref="InvalidOperationException">No workflow is registered under the name.</exception>
    internal (int Version, WorkflowBody Body) ResolveLatest(string name)
    {
        if (!_latestVersionByName.TryGetValue(name, out var version))
        {
            throw new InvalidOperationException(
                $"No workflow is registered under the name '{name}'. Register it with AddWorkflow(...).");
        }

        return (version, _bodies[(name, version)]);
    }

    /// <summary>
    /// Resolves a specific registered definition version — the version an in-flight instance replays against.
    /// </summary>
    /// <param name="name">The workflow name.</param>
    /// <param name="version">The pinned definition version from the instance's journal.</param>
    /// <returns>The registered <see cref="WorkflowBody"/> for that exact version.</returns>
    /// <exception cref="WorkflowVersionNotRegisteredException">
    /// The pinned version is no longer registered — resolution fails loud rather than silently replaying
    /// against a different definition.
    /// </exception>
    internal WorkflowBody Resolve(string name, int version)
    {
        if (!_bodies.TryGetValue((name, version), out var body))
        {
            throw new WorkflowVersionNotRegisteredException(name, version);
        }

        return body;
    }
}
