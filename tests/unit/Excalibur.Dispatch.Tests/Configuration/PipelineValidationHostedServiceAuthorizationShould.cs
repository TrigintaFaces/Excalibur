// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Middleware.Auth;
using Excalibur.Dispatch.Options.Middleware;
using Excalibur.Dispatch.Telemetry;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Tests.Configuration;

/// <summary>
/// ssn7a3/z5g5lt — author≠impl fail-closed security lock (TestsDeveloper) for
/// <see cref="PipelineValidationHostedService"/>. When a pipeline profile DECLARES the authorization stage
/// (its <c>MiddlewareTypes</c> contains <see cref="AuthorizationMiddleware"/>) but no
/// <see cref="AuthorizationMiddleware"/> instance is resolvable in the pipeline, messages routed through
/// that profile would silently bypass authorization — a fail-OPEN security gap. The startup guard MUST
/// fail CLOSED (throw at <c>StartAsync</c>). It must NOT over-fire: an application whose profiles declare no
/// authorization stage is unaffected (AC3 — opt-in complexity, no false positive).
/// </summary>
/// <remarks>
/// <b>RED mutants:</b> drop the profile-declares-auth guard ⇒ the declared-but-absent case silently passes
/// (fail-open, RED). Widen it to fire without checking the declaration ⇒ the no-declaration case throws (RED).
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Feature", "Authorization")]
public sealed class PipelineValidationHostedServiceAuthorizationShould
{
	[Fact]
	public async Task FailClosed_WhenProfileDeclaresAuthorization_ButNoAuthorizationMiddlewareResolvable()
	{
		// A profile declares the authorization stage, but only a NON-auth middleware is registered.
		var service = CreateService(
			profileDeclaresAuthorization: true,
			middlewares: [A.Fake<IDispatchMiddleware>()]);

		_ = await Should.ThrowAsync<InvalidOperationException>(
			async () => await service.StartAsync(CancellationToken.None));
	}

	[Fact]
	public async Task Pass_WhenProfileDeclaresAuthorization_AndAuthorizationMiddlewareIsResolvable()
	{
		// The declared authorization stage IS satisfied by a real AuthorizationMiddleware instance.
		var service = CreateService(
			profileDeclaresAuthorization: true,
			middlewares: [CreateAuthorizationMiddleware()]);

		// Must NOT throw — authorization is present, so nothing is bypassed.
		await service.StartAsync(CancellationToken.None);
	}

	[Fact]
	public async Task Pass_WhenNoProfileDeclaresAuthorization_EvenWithoutAuthMiddleware()
	{
		// No profile expects authorization → a non-authorizing app is unaffected (no false positive).
		var service = CreateService(
			profileDeclaresAuthorization: false,
			middlewares: [A.Fake<IDispatchMiddleware>()]);

		await service.StartAsync(CancellationToken.None);
	}

	// SENTINEL REVIEW_CODE BLOCKING (ssn7a3 over-fire) — the real-seeding path the fake-registry unit tests
	// above never exercised. `AddDefaultDispatchPipelines()` seeds the always-registered, auto-selectable
	// Strict profile, which DECLARES AuthorizationMiddleware — but auth middleware is only registered via the
	// opt-in UseAuthorization(). A consumer on the documented simple path (default pipelines, no auth) must
	// still BOOT: the guard must key off intent, not the mere presence of the seeded Strict profile.
	// This boots a real service graph and runs every IHostedService.StartAsync (as an IHost would).
	// RED = the over-fire is real (StartAsync throws); GREEN once the guard is scoped correctly.
	[Fact]
	public async Task AddDefaultDispatchPipelines_WithoutAuthorization_BootsWithoutThrowing()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		// A non-auth middleware so the pipeline isn't empty (isolates the AUTH guard from the empty-pipeline
		// check, which uses the same legacy GetServices<IDispatchMiddleware>() path).
		_ = services.AddSingleton<IDispatchMiddleware>(A.Fake<IDispatchMiddleware>());
		_ = services.AddDefaultDispatchPipelines(); // seeds the REAL registry (Strict declares auth); NO UseAuthorization().

		using var provider = services.BuildServiceProvider();

		foreach (var hosted in provider.GetServices<IHostedService>())
		{
			// Must NOT throw — a non-authorizing app on the default path must start cleanly (AC3).
			await hosted.StartAsync(CancellationToken.None);
		}
	}

	private static PipelineValidationHostedService CreateService(
		bool profileDeclaresAuthorization,
		IReadOnlyList<IDispatchMiddleware> middlewares)
	{
		var profile = A.Fake<IPipelineProfile>();
		IReadOnlyList<MiddlewareEntry> declaredEntries = profileDeclaresAuthorization
			? [new MiddlewareEntry(typeof(AuthorizationMiddleware), MiddlewareCriticality.Required)]
			: [];
		_ = A.CallTo(() => profile.MiddlewareEntries).Returns(declaredEntries);

		var registry = A.Fake<IPipelineProfileRegistry>();
		_ = A.CallTo(() => registry.GetProfileNames()).Returns(["Strict"]);
		_ = A.CallTo(() => registry.GetProfile("Strict")).Returns(profile);
		// Rec 1 (SA ruling): the guard keys off the DEFAULT profile (consumer intent), not any registered
		// profile. Model "Strict" as the selected default — so a default profile that declares auth with no
		// resolvable middleware fails closed, while one declaring no auth boots.
		_ = A.CallTo(() => registry.GetDefaultProfileName()).Returns("Strict");

		var services = new ServiceCollection();
		foreach (var middleware in middlewares)
		{
			// Register under the IDispatchMiddleware service type so GetServices<IDispatchMiddleware>()
			// resolves it (the guard's `m is AuthorizationMiddleware` still sees the concrete runtime type).
			_ = services.AddSingleton<IDispatchMiddleware>(middleware);
		}

		_ = services.AddSingleton(registry);

		return new PipelineValidationHostedService(
			services.BuildServiceProvider(), NullLogger<PipelineValidationHostedService>.Instance);
	}

	private static AuthorizationMiddleware CreateAuthorizationMiddleware() =>
		new(
			MsOptions.Create(new AuthorizationOptions()),
			A.Fake<IAuthorizationService>(),
			A.Fake<ITelemetrySanitizer>(),
			NullLogger<AuthorizationMiddleware>.Instance);
}
