// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;

using Microsoft.Extensions.Hosting;

namespace Excalibur.Saga.DependencyInjection;

/// <summary>
/// Startup-time prerequisite validator that fails loud at <see cref="IHost.StartAsync"/>
/// if the consumer called <c>AddSagas(...)</c> without registering a concrete
/// <see cref="ISagaStateStore"/> provider (e.g., by omitting <c>.UseInMemory()</c>,
/// <c>.UseSqlServer(...)</c>, <c>.UseMongoDB(...)</c>, or a consumer-supplied store).
/// </summary>
/// <remarks>
/// <para>
/// Per S809 bd-x6rg45 + COMPASS msg 2189: minimal-wiring validators must fail at host
/// start, not at first saga step. Registering this as an <see cref="IHostedService"/>
/// places the probe in the host's startup pipeline ahead of any domain workload.
/// </para>
/// <para>
/// AOT-safe: the probe uses <c>IServiceProvider.GetService(typeof(ISagaStateStore))</c>
/// — no reflection, no assembly scanning.
/// </para>
/// </remarks>
internal sealed class SagaPrerequisiteValidator : IHostedService
{
	private readonly IServiceProvider _services;

	public SagaPrerequisiteValidator(IServiceProvider services)
	{
		_services = services ?? throw new ArgumentNullException(nameof(services));
	}

	public Task StartAsync(CancellationToken cancellationToken)
	{
		if (_services.GetService(typeof(ISagaStateStore)) is null)
		{
			throw new InvalidOperationException(
				"Excalibur sagas are missing the required ISagaStateStore implementation. " +
				"Call a provider extension inside AddSagas(...) — for example " +
				"s => s.UseInMemory(), s => s.UseSqlServer(sql => sql.ConnectionString(...)), " +
				"or s => s.UseMongoDB(...) — before host startup.");
		}

		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
