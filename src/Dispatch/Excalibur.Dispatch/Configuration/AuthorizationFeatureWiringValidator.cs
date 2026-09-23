// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Middleware.Auth;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Fails start-up when the pipeline was wired for authorization but the feature that activates it is off.
/// </summary>
/// <remarks>
/// The composition explicitly asked for authorization -- the middleware is in the global set -- while the
/// feature flag that turns the stage on is disabled. The synthesizer would drop the stage, so
/// authorization-required messages would reach their handlers unauthorized. Fail loud and name the flag,
/// mirroring how ASP.NET Core refuses a pipeline that is missing its authorization wiring.
///
/// This runs at start-up rather than while the composition is still being written, because the composition
/// is not finished until the provider is built: a consumer chaining off the value AddDispatch returns adds
/// middleware after any registration-time check has already run. A guard that reads a half-written
/// composition reports on a state nobody ships.
///
/// It is distinct from the wiring validator beside it, which catches the opposite mistake -- a profile that
/// declares the authorization stage with no middleware registered to serve it.
/// </remarks>
/// <param name="state"> The composition the builders accumulated into. </param>
internal sealed class AuthorizationFeatureWiringValidator(DispatchBuilderState state)
	: IHostedService, IStartupPrerequisiteValidator
{
	/// <inheritdoc />
	public void Validate()
	{
		if (!state.GlobalMiddleware.Contains(typeof(AuthorizationMiddleware))
			|| state.Options.Features.EnableAuthorization)
		{
			return;
		}

		throw new InvalidOperationException(
			"AuthorizationMiddleware is registered in the dispatch pipeline (via UseAuthorization()), but the " +
			"Authorization feature is disabled (DispatchOptions.Features.EnableAuthorization = false). This would " +
			"silently drop the authorization stage and allow authorization-required Action messages to bypass " +
			"authorization. Enable the Authorization feature, or remove UseAuthorization() if authorization is " +
			"intentionally not used.");
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
