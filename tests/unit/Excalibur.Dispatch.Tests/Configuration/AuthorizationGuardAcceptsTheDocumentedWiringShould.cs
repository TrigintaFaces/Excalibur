// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Middleware.Auth;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Configuration;

/// <summary>
/// The LIVENESS half of the authorization startup guard: a host that wires authorization the documented
/// way must START.
/// </summary>
/// <remarks>
/// <para>
/// The guard's safety half — refuse a host whose profile declares the authorization stage with no
/// authorization wired — is covered elsewhere and is easy to satisfy. <b>A guard that refuses every host
/// satisfies it too</b>, which is how this shipped: the guard read
/// <c>GetServices&lt;IDispatchMiddleware&gt;()</c>, while <c>UseAuthorization()</c> resolves to
/// <c>UseMiddleware&lt;AuthorizationMiddleware&gt;()</c> and registers the middleware by its CONCRETE
/// TYPE. The composed pipeline is the union of both collections; the guard inspected only the half the
/// documented route does not populate, so a correctly wired host was refused startup and told to wire the
/// thing it had just wired.
/// </para>
/// <para>
/// The first arm therefore goes through the <b>real</b> <c>UseAuthorization()</c> rather than replicating
/// what it registers. Replicating it would assert that the guard accepts a shape this test chose, which is
/// the proxy that let the original defect through — the question is whether the guard accepts what the
/// SHIPPED extension method actually produces.
/// </para>
/// <para>
/// A guard that refuses a correctly wired host is worse than no guard: it teaches people to delete it, and
/// the next genuine bypass then ships unopposed.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Feature", "Authorization")]
public sealed class AuthorizationGuardAcceptsTheDocumentedWiringShould
{
	/// <summary>
	/// LIVENESS: the documented wiring route must satisfy the guard. RED before the fix.
	/// </summary>
	[Fact]
	public void StartAHostThatWiredAuthorizationTheDocumentedWay()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Registered BEFORE AddDispatch so the framework's TryAddSingleton defers to it: this pins the
		// profile to one that DECLARES the authorization stage, which is what arms the guard at all.
		_ = services.AddSingleton(RegistryDeclaring(authorization: true));

		// The documented route, called for real.
		_ = services.AddDispatch(static builder => builder.UseAuthorization());

		using var provider = services.BuildServiceProvider();

		Should.NotThrow(
			() => new AuthorizationWiringPrerequisiteValidator(provider).Validate(),
			"UseAuthorization() is the wiring this guard's own error message tells the consumer to use, so "
			+ "a host that called it must start");
	}

	/// <summary>
	/// SAFETY: the guard still refuses a host that declares the stage and never wired it.
	/// </summary>
	/// <remarks>
	/// Without this, the liveness arm above is satisfied by deleting the guard outright.
	/// </remarks>
	[Fact]
	public void RefuseAHostThatDeclaresAuthorizationAndNeverWiredIt()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(RegistryDeclaring(authorization: true));
		_ = services.AddDispatch(static _ => { }); // the stage is declared; nothing wires it

		using var provider = services.BuildServiceProvider();

		_ = Should.Throw<InvalidOperationException>(
			() => new AuthorizationWiringPrerequisiteValidator(provider).Validate());
	}

	/// <summary>
	/// CONTROL: a profile that declares no authorization stage is unaffected either way.
	/// </summary>
	/// <remarks>
	/// This pins the guard to consumer INTENT. Without it, a guard that simply never fires would pass both
	/// arms above only if the safety arm were also removed — and a guard that fires for every application
	/// would pass neither. It is the arm that keeps the other two honest about what arms the check.
	/// </remarks>
	[Fact]
	public void IgnoreAHostWhoseProfileDeclaresNoAuthorization()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(RegistryDeclaring(authorization: false));
		_ = services.AddDispatch(static _ => { });

		using var provider = services.BuildServiceProvider();

		Should.NotThrow(() => new AuthorizationWiringPrerequisiteValidator(provider).Validate());
	}

	#region Helpers

	private static IPipelineProfileRegistry RegistryDeclaring(bool authorization)
	{
		var profile = A.Fake<IPipelineProfile>();
		IReadOnlyList<MiddlewareEntry> entries = authorization
			? [new MiddlewareEntry(typeof(AuthorizationMiddleware), MiddlewareCriticality.Required)]
			: [];
		_ = A.CallTo(() => profile.MiddlewareEntries).Returns(entries);

		var registry = A.Fake<IPipelineProfileRegistry>();
		_ = A.CallTo(() => registry.GetDefaultProfileName()).Returns("TestProfile");
		_ = A.CallTo(() => registry.GetProfileNames()).Returns(["TestProfile"]);
		_ = A.CallTo(() => registry.GetProfile("TestProfile")).Returns(profile);

		return registry;
	}

	#endregion
}
