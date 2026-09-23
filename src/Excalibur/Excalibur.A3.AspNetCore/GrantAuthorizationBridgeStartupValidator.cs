// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3.Authentication;
using Excalibur.Dispatch;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.A3.AspNetCore;

/// <summary>
/// Startup guard that fails loud when the pieces grant authorization needs to see the request are not
/// all present.
/// </summary>
/// <remarks>
/// Without this guard the missing-bridge failure is silent and misleading: every request is denied, and
/// the denial is indistinguishable from a caller who was correctly identified and simply lacks the
/// grant. Each check below is decidable from the composed container, so the misconfiguration surfaces at
/// host start with the call that fixes it, rather than as a uniform 403 in production.
/// </remarks>
internal sealed class GrantAuthorizationBridgeStartupValidator(IServiceProvider services)
	: IHostedService, IStartupPrerequisiteValidator
{
	/// <inheritdoc />
	public Task StartAsync(CancellationToken cancellationToken)
	{
		Validate();
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public void Validate()
	{
		using var scope = services.CreateScope();
		var scoped = scope.ServiceProvider;

		if (scoped.GetService<IHttpContextAccessor>() is null)
		{
			throw new InvalidOperationException(
				"Grant authorization cannot read the current request: no IHttpContextAccessor is registered. " +
				"Call AddHttpGrantAuthorization() on the service collection, which registers it.");
		}

		if (scoped.GetService<IAuthenticationToken>() is null)
		{
			throw new InvalidOperationException(
				"Grant authorization cannot resolve the caller: no IAuthenticationToken is registered, so every " +
				"request would be denied as unidentified. Call AddHttpGrantAuthorization() to bridge the " +
				"authenticated request principal, or register an IAuthenticationToken implementation of your own.");
		}

		if (scoped.GetService<ITenantContext>() is null)
		{
			throw new InvalidOperationException(
				"Grant authorization cannot resolve the tenant: no ITenantContext is registered, so every request " +
				"would be denied as untenanted. Call AddHttpGrantAuthorization() to resolve the tenant from the " +
				"request principal, or call AddTenantContext() and establish the tenant in your own middleware.");
		}

		if (scoped.GetService<Authorization.IAuthorizationPolicyProvider>() is null)
		{
			throw new InvalidOperationException(
				"Grant authorization cannot evaluate grants: no A3 authorization policy provider is registered. " +
				"Register the A3 grant-evaluation services (AddExcaliburA3 or AddExcaliburA3Core) alongside " +
				"AddHttpGrantAuthorization().");
		}

		if (scoped.GetService<IAuthorizationPolicyProvider>() is not GrantAuthorizationPolicyProvider)
		{
			throw new InvalidOperationException(
				"Grant policy names would not be recognized: the registered IAuthorizationPolicyProvider is not the " +
				"grant-aware provider, so a policy name such as \"grant:Approve:Order:{id}\" would not resolve. " +
				"Call AddHttpGrantAuthorization() after any other registration that replaces the policy provider.");
		}
	}

	/// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
