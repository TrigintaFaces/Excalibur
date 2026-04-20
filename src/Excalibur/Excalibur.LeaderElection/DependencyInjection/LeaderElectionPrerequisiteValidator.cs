// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Hosting;

namespace Excalibur.Dispatch.LeaderElection.DependencyInjection;

/// <summary>
/// Startup-time prerequisite validator that fails loud at <see cref="IHost.StartAsync"/>
/// if the consumer called <c>AddLeaderElection(...)</c> without also registering a
/// concrete <see cref="ILeaderElection"/> provider (e.g., by omitting
/// <c>.UseInMemory()</c> / <c>.UseSqlServer(...)</c> / <c>.UseRedis(...)</c>).
/// </summary>
/// <remarks>
/// <para>
/// Per S809 bd-x6rg45 + COMPASS msg 2189: minimal-wiring validators must fail at host
/// start, not at first acquire. Registering this as an <see cref="IHostedService"/>
/// places the probe in the host's startup pipeline ahead of any domain workload.
/// </para>
/// <para>
/// AOT-safe: the probe uses <c>IServiceProvider.GetService(typeof(ILeaderElection))</c>
/// — no reflection, no assembly scanning.
/// </para>
/// </remarks>
internal sealed class LeaderElectionPrerequisiteValidator : IHostedService
{
	private readonly IServiceProvider _services;

	public LeaderElectionPrerequisiteValidator(IServiceProvider services)
	{
		_services = services ?? throw new ArgumentNullException(nameof(services));
	}

	public Task StartAsync(CancellationToken cancellationToken)
	{
		if (_services.GetService(typeof(ILeaderElection)) is null)
		{
			throw new InvalidOperationException(
				"Excalibur leader election is missing its required ILeaderElection implementation. " +
				"Call a provider extension inside AddLeaderElection(...) — for example " +
				"le => le.UseInMemory(), le => le.UseSqlServer(sql => sql.ConnectionString(...)), " +
				"or le => le.UseRedis(r => r.ConnectionString(...)) — before host startup.");
		}

		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
