// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Inbox.DependencyInjection;

/// <summary>
/// Startup-time prerequisite validator that fails loud at <see cref="IHost.StartAsync"/>
/// if the consumer called <c>AddInbox(...)</c> without registering a concrete
/// <see cref="IInboxStore"/> provider (e.g., by omitting <c>.UseSqlServer(...)</c>,
/// <c>.UsePostgres(...)</c>, <c>.UseInMemory()</c>, or a consumer-supplied store).
/// </summary>
/// <remarks>
/// <para>
/// Per S809 bd-x6rg45 + COMPASS msg 2189: minimal-wiring validators must fail at host
/// start, not at first message deduplication. Registering this as an <see cref="IHostedService"/>
/// places the probe in the host's startup pipeline ahead of any domain workload.
/// </para>
/// <para>
/// AOT-safe: the probe uses <c>IServiceProvider.GetKeyedService&lt;IInboxStore&gt;("default")</c>
/// — no reflection, no assembly scanning.
/// </para>
/// </remarks>
internal sealed class InboxPrerequisiteValidator : IHostedService
{
	private readonly IServiceProvider _services;

	public InboxPrerequisiteValidator(IServiceProvider services)
	{
		_services = services ?? throw new ArgumentNullException(nameof(services));
	}

	public Task StartAsync(CancellationToken cancellationToken)
	{
		if (_services.GetKeyedService<IInboxStore>("default") is null)
		{
			throw new InvalidOperationException(
				"Excalibur inbox is missing the required IInboxStore implementation. " +
				"Call a provider extension inside AddInbox(...) — for example " +
				"i => i.UseSqlServer(sql => sql.ConnectionString(...)), " +
				"i => i.UsePostgres(...), or i => i.UseInMemory() — before host startup.");
		}

		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
