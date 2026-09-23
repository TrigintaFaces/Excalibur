// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Hosting.AspNetCore.Tests.Authorization;

/// <summary>
/// Builds the authorization services a real ASP.NET Core host would have, so an arm can ask what a
/// CONSUMER's configuration does rather than what a mock was told to say.
/// </summary>
/// <remarks>
/// The middleware under test composes policies through <see cref="IAuthorizationPolicyProvider"/> and
/// evaluates them through <see cref="IAuthorizationService"/> — both the host's own. Faking either one
/// would test the fake: the defect these arms exist for is precisely that a consumer's configuration was
/// not reaching the evaluation, and a fake has no configuration to reach.
/// </remarks>
internal static class TestAuthorizationHost
{
	/// <summary>
	/// A provider over a default, unhardened <see cref="AuthorizationOptions"/> — the shape a host has when
	/// it has called <c>AddAuthorization()</c> and configured nothing further.
	/// </summary>
	public static IAuthorizationPolicyProvider PolicyProvider { get; } = Build(static _ => { }).Provider;

	/// <summary>
	/// Builds a real authorization stack over the supplied configuration.
	/// </summary>
	/// <param name="configure">The consumer's own <c>AddAuthorization</c> configuration.</param>
	/// <returns>The provider and service the host would resolve, plus the provider scope that owns them.</returns>
	public static (IAuthorizationPolicyProvider Provider, IAuthorizationService Service, ServiceProvider Container) Build(
		Action<AuthorizationOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddAuthorization(configure);

		var container = services.BuildServiceProvider();

		return (
			container.GetRequiredService<IAuthorizationPolicyProvider>(),
			container.GetRequiredService<IAuthorizationService>(),
			container);
	}
}
