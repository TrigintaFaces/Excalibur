// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Data.Persistence;

/// <summary>
/// Startup-time prerequisite validator that fails loud at <see cref="IHost.StartAsync"/>
/// if the consumer called <c>AddPersistence(...)</c> without registering a concrete
/// <see cref="IPersistenceProvider"/> (e.g., by omitting <c>.UseSqlServer(...)</c>,
/// <c>.UsePostgres(...)</c>, <c>.UseInMemory()</c>, or a consumer-supplied provider).
/// </summary>
/// <remarks>
/// <para>
/// minimal-wiring validators must fail at host
/// start, not at first data access. Registering this as an <see cref="IHostedService"/>
/// places the probe in the host's startup pipeline ahead of any domain workload.
/// </para>
/// <para>
/// AOT-safe: the probe uses <c>IServiceProvider.GetKeyedService&lt;IPersistenceProvider&gt;("default")</c>
/// — no reflection, no assembly scanning.
/// </para>
/// </remarks>
internal sealed class PersistencePrerequisiteValidator : IHostedService
{
	private readonly IServiceProvider _services;

	public PersistencePrerequisiteValidator(IServiceProvider services)
	{
		_services = services ?? throw new ArgumentNullException(nameof(services));
	}

	public Task StartAsync(CancellationToken cancellationToken)
	{
		if (_services.GetKeyedService<IPersistenceProvider>("default") is null)
		{
			throw new InvalidOperationException(
				"Excalibur persistence is missing the required IPersistenceProvider implementation. " +
				"Call a provider extension after AddPersistence(...) — for example " +
				"services.AddSqlServerPersistence(...), services.AddPostgresPersistence(...), " +
				"or services.AddInMemoryPersistence() — before host startup.");
		}

		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
