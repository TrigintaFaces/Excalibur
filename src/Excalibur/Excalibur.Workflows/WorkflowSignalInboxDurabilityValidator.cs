// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Workflows;

/// <summary>
/// Opt-in startup guard registered by <c>RequireDurableSignalInbox()</c>. Fails host start when a durable
/// <see cref="IWorkflowSignalInbox"/> is required but only the in-process default is wired — i.e. the
/// <see cref="IWorkflowSignalDurability"/> capability marker is absent.
/// </summary>
/// <remarks>
/// <para>
/// In-memory inbox + durable journal is a <em>legitimate</em> (limited) configuration, so this guard is
/// opt-in: a consumer who needs restart-durable signals calls <c>RequireDurableSignalInbox()</c> to turn the
/// silent-signal-loss state into a startup failure. Without that opt-in the in-process default boots
/// normally.
/// </para>
/// <para>
/// The check inspects service <em>registration</em> via <see cref="IServiceProviderIsService"/> and never
/// resolves the inbox: the guard is a singleton holding the root provider, and probing registration returns
/// the same verdict whether or not scope validation is enabled, while constructing nothing. AOT-safe: no
/// reflection. Presence of the marker is the door; the restart-durable dedup lock is the lock.
/// </para>
/// </remarks>
internal sealed class WorkflowSignalInboxDurabilityValidator : IHostedService, IStartupPrerequisiteValidator
{
    private readonly IServiceProvider _services;

    public WorkflowSignalInboxDurabilityValidator(IServiceProvider services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
    	Validate();
    	return Task.CompletedTask;
    }

    public void Validate()
    {
        // Probe REGISTRATION, never RESOLUTION — the marker is a registration-time presence signal emitted
        // by the durable provider's Add* call, inseparable from the durable wiring it attests.
        var isService = _services.GetRequiredService<IServiceProviderIsService>();
        if (!isService.IsService(typeof(IWorkflowSignalDurability)))
        {
            // Fail fast. RequireDurableSignalInbox() was called but the wired inbox is the in-process
            // default, which loses admitted-but-undrained signals and their deduplication keys on restart —
            // so a producer's post-restart redelivery is admitted a second time. AddSqlServerWorkflowSignalInbox()
            // (or another durable provider) is the one-line remedy, so no consumer is stranded by this throw.
            throw new InvalidOperationException(
                "RequireDurableSignalInbox() requires a durable IWorkflowSignalInbox, but only the in-process " +
                "default is registered. The in-memory inbox loses admitted signals and their deduplication keys " +
                "on restart. Call AddSqlServerWorkflowSignalInbox(...) (or another durable provider) to wire a " +
                "durable signal inbox.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
