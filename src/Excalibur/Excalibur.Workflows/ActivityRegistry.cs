// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Frozen;

namespace Excalibur.Workflows;

/// <summary>
/// A type-erased activity invocation: resolves the concrete activity from the service provider, executes
/// it with the given (boxed) input, and returns the (boxed) result.
/// </summary>
/// <param name="serviceProvider">The scope from which to resolve the activity.</param>
/// <param name="input">The boxed activity input.</param>
/// <param name="cancellationToken">A token to observe for cancellation.</param>
/// <returns>The boxed activity result, or <see langword="null"/> when the activity produces none.</returns>
internal delegate ValueTask<object?> ActivityInvoker(
    IServiceProvider serviceProvider,
    object input,
    CancellationToken cancellationToken);

/// <summary>
/// A registered activity: its name and type-erased invoker. Registered as a DI singleton and aggregated by
/// <see cref="ActivityRegistry"/>.
/// </summary>
/// <param name="Name">The activity name.</param>
/// <param name="Invoker">The type-erased invoker.</param>
internal sealed record ActivityDescriptor(string Name, ActivityInvoker Invoker);

/// <summary>
/// Resolves registered activity names to their invokers. Built once from the registered
/// <see cref="ActivityDescriptor"/> set at composition time.
/// </summary>
internal sealed class ActivityRegistry
{
    private readonly FrozenDictionary<string, ActivityInvoker> _invokers;

    public ActivityRegistry(IEnumerable<ActivityDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        var map = new Dictionary<string, ActivityInvoker>(StringComparer.Ordinal);
        foreach (var descriptor in descriptors)
        {
            if (!map.TryAdd(descriptor.Name, descriptor.Invoker))
            {
                throw new InvalidOperationException(
                    $"An activity named '{descriptor.Name}' is registered more than once.");
            }
        }

        _invokers = map.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>
    /// Resolves a registered activity invoker.
    /// </summary>
    /// <param name="name">The activity name.</param>
    /// <returns>The registered <see cref="ActivityInvoker"/>.</returns>
    /// <exception cref="InvalidOperationException">No activity is registered under the name.</exception>
    internal ActivityInvoker Resolve(string name)
    {
        if (!_invokers.TryGetValue(name, out var invoker))
        {
            throw new InvalidOperationException(
                $"No activity is registered under the name '{name}'. Register it with AddActivity(...).");
        }

        return invoker;
    }
}
