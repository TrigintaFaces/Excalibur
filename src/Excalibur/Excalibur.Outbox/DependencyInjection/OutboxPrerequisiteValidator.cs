// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Hosting;

namespace Excalibur.Outbox.DependencyInjection;

/// <summary>
/// Startup-time prerequisite validator that fails loud at <see cref="IHost.StartAsync"/>
/// if the consumer called <c>AddOutbox(...)</c> without registering a concrete
/// <see cref="IOutboxStore"/> provider (e.g., by omitting <c>.UseSqlServer(...)</c>,
/// <c>.UsePostgres(...)</c>, <c>.UseMongoDB(...)</c>, or a consumer-supplied store).
/// </summary>
/// <remarks>
/// <para>
/// Per S809 bd-x6rg45 + COMPASS msg 2189: minimal-wiring validators must fail at host
/// start, not at first message enqueue. Registering this as an <see cref="IHostedService"/>
/// places the probe in the host's startup pipeline ahead of any domain workload.
/// </para>
/// <para>
/// AOT-safe: the probe uses <c>IServiceProvider.GetService(typeof(IOutboxStore))</c>
/// — no reflection, no assembly scanning.
/// </para>
/// </remarks>
internal sealed class OutboxPrerequisiteValidator : IHostedService
{
	private readonly IServiceProvider _services;

	public OutboxPrerequisiteValidator(IServiceProvider services)
	{
		_services = services ?? throw new ArgumentNullException(nameof(services));
	}

	public Task StartAsync(CancellationToken cancellationToken)
	{
		if (_services.GetService(typeof(IOutboxStore)) is null)
		{
			throw new InvalidOperationException(
				"Excalibur outbox is missing the required IOutboxStore implementation. " +
				"Call a provider extension inside AddOutbox(...) — for example " +
				"o => o.UseSqlServer(sql => sql.ConnectionString(...)), " +
				"o => o.UsePostgres(...), or o => o.UseMongoDB(...) — before host startup.");
		}

		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
