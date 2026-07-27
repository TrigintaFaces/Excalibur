// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Middleware.Auth;

namespace Excalibur.Dispatch.Tests.DependencyInjection;

/// <summary>
/// Binds the failure message produced when a globally-registered middleware cannot be resolved.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> <c>UseMiddleware&lt;T&gt;()</c> — and every helper built on it, including
/// <c>UseAuthorization()</c> — reaches the pipeline through a <em>factory</em> overload. A factory has no
/// static type, so the builder previously reported the entry as an anonymous
/// "(middleware supplied by a factory delegate)" while the real type name appeared only inside the
/// container's nested message. The published guidance now directs consumers down exactly this path, so
/// its message is the primary way a misconfiguration gets diagnosed.
/// </para>
/// <para>
/// <b>Both arms (testing-patterns §3).</b> SAFETY — an unresolvable required middleware refuses the build
/// and the entry NAMES the middleware. LIVENESS — a fully-wired registration still builds, so a builder
/// that refused everything could not pass the safety arm.
/// </para>
/// <para>
/// <b>This asserts the CONTRACT, not the prose.</b> Reword the message freely and these stay green;
/// stop naming the middleware, or reintroduce the anonymous label on a path where the type is known,
/// and they go red.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait(TraitNames.Component, TestComponents.DependencyInjection)]
public sealed class GlobalMiddlewareResolutionFailureShould : IDisposable
{
	private ServiceProvider? _serviceProvider;

	public void Dispose() => _serviceProvider?.Dispose();

	/// <summary>
	/// SAFETY — a globally-registered middleware whose dependency is missing must refuse the build, and the
	/// entry must name the middleware rather than an anonymous factory placeholder.
	/// </summary>
	[Fact]
	public void NameTheMiddlewareWhenAGloballyRegisteredOneCannotBeResolved()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddOptions();

		// Registered globally, but IAuthorizationService is deliberately absent — the shape a consumer
		// reaches by following the published "register middleware explicitly" guidance.
		_ = services.AddDispatch(dispatch => dispatch.UseMiddleware<AuthorizationMiddleware>());

		_serviceProvider = services.BuildServiceProvider();

		var exception = Should.Throw<InvalidOperationException>(
			() => _serviceProvider.GetRequiredService<IDispatchPipeline>());

		exception.Message.ShouldContain(
			nameof(AuthorizationMiddleware),
			Case.Sensitive,
			"the entry must name the middleware — the builder knows the type and must not discard it");

		exception.Message.ShouldNotContain(
			"supplied by a factory delegate",
			Case.Sensitive,
			"the anonymous placeholder is for genuinely typeless factories, never for a known type");
	}

	/// <summary>
	/// LIVENESS — a fully-wired global registration still resolves. Without this arm, a builder that threw
	/// for every configuration would satisfy the safety arm above.
	/// </summary>
	[Fact]
	public void ResolveThePipelineWhenTheGlobalMiddlewareIsFullyWired()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddOptions();
		_ = services.AddDispatch();

		_serviceProvider = services.BuildServiceProvider();

		_ = _serviceProvider.GetRequiredService<IDispatchPipeline>().ShouldNotBeNull();
	}
}
