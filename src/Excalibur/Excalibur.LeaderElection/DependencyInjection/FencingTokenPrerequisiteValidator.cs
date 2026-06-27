// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection.Fencing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.LeaderElection.DependencyInjection;

/// <summary>
/// Startup-time prerequisite validator that fails loud at <see cref="IHost.StartAsync"/> if the
/// consumer called <c>WithFencingTokens()</c> without registering a concrete
/// <see cref="IFencingTokenProvider"/> (ADR-339 Decision 3).
/// </summary>
/// <remarks>
/// <para>
/// An opt-in feature whose required dependency is absent must fail at composition time, not silently
/// degrade. A consumer who opts into fencing and gets <em>no</em> split-brain protection (because no
/// provider resolved) is strictly worse off than one who gets a clear startup failure telling them to
/// register a provider. This is the Microsoft <c>IOptions&lt;T&gt;</c> + <c>ValidateOnStart()</c>
/// fail-fast contract. Mirrors <see cref="LeaderElectionPrerequisiteValidator"/>.
/// </para>
/// <para>
/// AOT-safe: the probe uses <c>IServiceProvider.GetService&lt;IFencingTokenProvider&gt;()</c> — no
/// reflection, no assembly scanning.
/// </para>
/// </remarks>
internal sealed class FencingTokenPrerequisiteValidator : IHostedService
{
	private readonly IServiceProvider _services;

	public FencingTokenPrerequisiteValidator(IServiceProvider services)
	{
		_services = services ?? throw new ArgumentNullException(nameof(services));
	}

	public Task StartAsync(CancellationToken cancellationToken)
	{
		if (_services.GetService<IFencingTokenProvider>() is null)
		{
			throw new InvalidOperationException(
				"Excalibur leader election fencing-token support was enabled via WithFencingTokens() " +
				"but no IFencingTokenProvider implementation is registered. Register a provider before " +
				"host startup — for example services.AddRedisFencingTokenProvider() " +
				"(Excalibur.LeaderElection.Redis), or services.AddFencingTokenSupport<TProvider>().");
		}

		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
