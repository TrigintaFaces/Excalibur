// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Middleware.Auth;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Fails closed at startup when the selected pipeline profile declares the authorization stage but no
/// <see cref="AuthorizationMiddleware"/> is resolvable, which would let messages routed through that
/// profile bypass authorization silently.
/// </summary>
/// <remarks>
/// <para>
/// <b>It implements both lifecycle contracts deliberately.</b> An <see cref="IHostedService"/> alone runs
/// only when something calls <c>IHost.StartAsync</c>, so a consumer who builds a provider directly — a
/// serverless entry point, a manual <c>BuildServiceProvider()</c> — would never trigger it and the
/// guarantee would be silently inert for exactly the hosts least able to afford it. Pairing it with
/// <see cref="IStartupPrerequisiteValidator"/> lets <c>ValidateStartupGates()</c> reach it on the
/// host-less path.
/// </para>
/// <para>
/// <b>The property this guards is intent, not configuration shape:</b> it fires only where the consumer
/// has expressed an intent to authorize. Keying on the DEFAULT profile is one implementation of that —
/// the framework always seeds a Strict profile that declares authorization, so keying on "any registered
/// profile" would fire for every application that never asked for authorization at all.
/// </para>
/// <para>
/// It reads <see cref="IPipelineProfileRegistry"/> — the same declaration the pipeline builder consults.
/// This guard and profile selection must never read different collections: if one could see an
/// authorization entry the other could not, the check would pass against a list the built pipeline never
/// used.
/// </para>
/// </remarks>
internal sealed class AuthorizationWiringPrerequisiteValidator(IServiceProvider serviceProvider)
	: IHostedService, IStartupPrerequisiteValidator
{
	/// <inheritdoc />
	public void Validate()
	{
		var profileRegistry = serviceProvider.GetService<IPipelineProfileRegistry>();
		var defaultProfileName = profileRegistry?.GetDefaultProfileName();
		var defaultProfile = defaultProfileName is null ? null : profileRegistry!.GetProfile(defaultProfileName);

		if (defaultProfile is null
			|| !defaultProfile.MiddlewareEntries.Any(static e => e.MiddlewareType == typeof(AuthorizationMiddleware)))
		{
			return;
		}

		if (!IsAuthorizationWired())
		{
			throw new InvalidOperationException(
				"The selected pipeline profile declares the authorization stage (AuthorizationMiddleware), but no " +
				"AuthorizationMiddleware is registered or resolvable in the dispatch pipeline. Messages routed " +
				"through that profile would silently bypass authorization. Register the authorization middleware " +
				"(e.g. AddDispatch(builder => builder.UseAuthorization())) or remove the authorization stage from " +
				"the profile.");
		}
	}

	/// <summary>
	/// Answers whether authorization is reachable in the pipeline the host will actually compose.
	/// </summary>
	/// <returns>
	/// <see langword="true" /> when an <see cref="AuthorizationMiddleware" /> will be in the composed
	/// pipeline by either supported wiring route.
	/// </returns>
	/// <remarks>
	/// <para>
	/// <b>The composed pipeline is a UNION of two collections, and this must inspect both.</b>
	/// <c>UseAuthorization()</c> resolves to <c>UseMiddleware&lt;AuthorizationMiddleware&gt;()</c>, which
	/// registers the middleware by its CONCRETE TYPE and adds it to the builder's own global list. It
	/// never enters the <see cref="IDispatchMiddleware" /> enumerable. Reading only that enumerable asked
	/// about the half that the documented wiring route does not populate, so a host that called
	/// <c>UseAuthorization()</c> correctly was refused startup and told to do the thing it had just done.
	/// </para>
	/// <para>
	/// The concrete-type question is asked through <see cref="IServiceProviderIsService" /> rather than by
	/// resolving the middleware. That answers whether it is REGISTERED without constructing it or its
	/// dependencies, which matters because this validator is a singleton holding the root provider:
	/// resolving a scoped middleware from the root is the captive-dependency fault this guard exists
	/// alongside, and a startup check must not introduce it.
	/// </para>
	/// <para>
	/// The enumerable is still consulted, second, because registering the middleware under
	/// <see cref="IDispatchMiddleware" /> is also supported and is not visible to the concrete-type
	/// question. It is only reached when the cheaper question has already said no.
	/// </para>
	/// </remarks>
	private bool IsAuthorizationWired()
	{
		var isService = serviceProvider.GetService<IServiceProviderIsService>();
		if (isService?.IsService(typeof(AuthorizationMiddleware)) == true)
		{
			return true;
		}

		return serviceProvider.GetServices<IDispatchMiddleware>()
			.Any(static m => m.Unwrap() is AuthorizationMiddleware);
	}

	/// <inheritdoc />
	public Task StartAsync(CancellationToken cancellationToken)
	{
		Validate();
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
