// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Middleware.Auth;

using Excalibur.Dispatch.Telemetry;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Tests.ErrorHandling;

/// <summary>
/// Binds the composition contract of <c>AddDeadLetterOnExhaustion()</c> against a real
/// <see cref="ServiceProvider"/> built through the production registration path.
/// </summary>
/// <remarks>
/// <para>
/// Every arm resolves from a container built by the public extension method. Constructing
/// <see cref="DeadLetterOnExhaustionMiddleware"/> by hand would prove only that the middleware works when it
/// is handed its dependencies — which was never in doubt, and which is precisely how the defect these arms
/// lock survived: the shipped quickstart could not be activated at all, because the no-op default was
/// registered by TYPE and <see cref="NullDeadLetterQueue"/>'s constructor is private.
/// </para>
/// <para>
/// <b>Both arms (testing-patterns §3).</b> SAFETY — a host that enables routing without a store is refused,
/// with a message naming the missing registration. LIVENESS — a host that registers a store resolves and can
/// route, so a registration that refused everything could not pass.
/// </para>
/// <para>
/// <b>⚠ THE <c>PinTheDefect_*</c> METHODS ARE NOT A SPECIFICATION.</b> Despite this type's <c>…Should</c>
/// name, those methods assert <b>current, defective</b> behaviour so that it is recorded in an executable
/// form rather than only in a tracker: the pipeline builder swallows a middleware's activation failure and
/// silently omits it (bd-h46qhj for the dead-letter case, bd-qfz8g4 and bd-quzmvo for the general and
/// authorization cases). Nobody intends the behaviour they assert.
/// </para>
/// <para>
/// <b>When a <c>PinTheDefect_*</c> method fails, that is the signal the defect has been FIXED — it is not a
/// regression.</b> Flip the assertion to match the corrected seam; do not delete the method and do not revert
/// the change that broke it. They are written this way deliberately, because a <c>Skip</c> would pass by
/// being skipped and disappear, and an unmarked failing test would redden the suite for everyone until an
/// architecture decision lands.
/// </para>
/// <para>
/// <b>The consumer documentation pins the same defect and has no forcing function — it will stay green while
/// going stale, so it must change in the SAME commit that flips these methods.</b> Published guidance
/// currently tells consumers that selecting a profile does not guarantee its middleware run, and instructs
/// them to resolve security-critical middleware at startup to detect the omission themselves. Once the seam
/// no longer swallows the failure, that advice is obsolete and its warnings become false. The pages carrying
/// it are <c>docs-site/docs/pipeline/profiles.md</c>, <c>docs-site/docs/patterns/dead-letter.md</c> and
/// <c>docs-site/docs/whats-new.md</c>. This paragraph exists because these tests are the only artifact that
/// fails when the defect is fixed; the obligation is recorded where the notification actually fires.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "ErrorHandling")]
public sealed class DeadLetterOnExhaustionRegistrationShould
{
	private static ServiceProvider Build(Action<IServiceCollection>? configure = null)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		configure?.Invoke(services);
		_ = services.AddDeadLetterOnExhaustion();
		return services.BuildServiceProvider();
	}

	/// <summary>
	/// SAFETY — the documented quickstart with no store registered must refuse, and the refusal must tell the
	/// developer what to register. A container error naming an internal type the consumer has never heard of
	/// satisfies "it throws" while failing the developer.
	/// </summary>
	[Fact]
	public void RefuseToResolveTheMiddlewareWhenNoDeadLetterQueueIsRegistered()
	{
		using var provider = Build();

		var exception = Should.Throw<InvalidOperationException>(
			() => provider.GetRequiredService<DeadLetterOnExhaustionMiddleware>());

		exception.Message.ShouldContain(
			"IDeadLetterQueue",
			Case.Sensitive,
			"the refusal must name the abstraction the developer has to register");

		exception.Message.ShouldNotContain(
			"A suitable constructor",
			Case.Sensitive,
			"the failure must be a stated composition error, not a container activation error naming a type "
			+ "the consumer never asked for");
	}

	/// <summary>
	/// LIVENESS — a host that registers a real store resolves the middleware. Without this arm a registration
	/// that refused every configuration would pass the safety arm above.
	/// </summary>
	[Fact]
	public void ResolveTheMiddlewareWhenADeadLetterQueueIsRegistered()
	{
		using var provider = Build(services =>
			services.AddSingleton<IDeadLetterQueue>(new RecordingDeadLetterQueue()));

		provider.GetRequiredService<DeadLetterOnExhaustionMiddleware>().ShouldNotBeNull();
	}

	/// <summary>
	/// LIVENESS — the documented opt-out resolves. The escape hatch the refusal message recommends has to work,
	/// or the message sends the developer into a second failure.
	/// </summary>
	[Fact]
	public void ResolveTheMiddlewareWhenTheHostExplicitlyOptsIntoDiscarding()
	{
		using var provider = Build(services =>
			services.AddSingleton<IDeadLetterQueue>(NullDeadLetterQueue.Instance));

		provider.GetRequiredService<DeadLetterOnExhaustionMiddleware>().ShouldNotBeNull();
	}

	/// <summary>
	/// A store registered by the consumer must win. <c>TryAdd</c> semantics are order-sensitive, so this is
	/// asserted in both orders rather than in whichever one the current implementation happens to favour.
	/// </summary>
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void PreferTheConsumerRegisteredQueueRegardlessOfRegistrationOrder(bool registerQueueFirst)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		var queue = new RecordingDeadLetterQueue();

		if (registerQueueFirst)
		{
			_ = services.AddSingleton<IDeadLetterQueue>(queue);
			_ = services.AddDeadLetterOnExhaustion();
		}
		else
		{
			_ = services.AddDeadLetterOnExhaustion();
			_ = services.AddSingleton<IDeadLetterQueue>(queue);
		}

		using var provider = services.BuildServiceProvider();

		provider.GetRequiredService<IDeadLetterQueue>().ShouldBeSameAs(queue);
		provider.GetRequiredService<DeadLetterOnExhaustionMiddleware>().ShouldNotBeNull();
	}

	/// <summary>
	/// The pipeline resolves profile middleware with <c>GetService</c>, not <c>GetRequiredService</c>, so an
	/// unregistered middleware becomes <see langword="null"/> and is skipped rather than throwing
	/// (<c>PipelineBuilder.cs:168-181</c> — "makes 'profile middleware not DI-registered → throw'
	/// structurally inexpressible").
	/// </summary>
	/// <remarks>
	/// A registered <em>factory</em> is a different case from an unregistered service: the factory runs, so a
	/// throw inside it is not converted to <see langword="null"/>. This arm pins whichever behaviour is real,
	/// because the refusal introduced for this bead and the pipeline's documented fail-open intent pull in
	/// opposite directions, and the answer decides whether that conflict exists at all.
	/// </remarks>
	[Fact]
	public void SurfaceTheRefusalThroughThePipelineFailOpenResolutionPath()
	{
		using var provider = Build();

		_ = Should.Throw<InvalidOperationException>(
			() => provider.GetService(typeof(DeadLetterOnExhaustionMiddleware)),
			"a registered factory runs even under GetService, so the refusal is NOT softened to null - "
			+ "the pipeline's skip-when-unregistered fail-open does not extend to a registered "
			+ "factory that refuses");
	}

	/// <summary>
	/// The path a real consumer takes: the middleware reaches the pipeline, not a direct
	/// <c>GetService</c> call. <c>PipelineBuilder.Build()</c> catches
	/// <see cref="InvalidOperationException"/> from a middleware factory and skips the middleware
	/// (<c>PipelineBuilder.cs:219-223</c>), so this arm establishes whether the refusal reaches the
	/// developer here or is absorbed into a log line.
	/// </summary>
	[Fact]
	public void PinTheDefect_SilentlyOmitTheDeadLetterMiddlewareOnThePipelinePath()
	{
		using var provider = Build();

		var builder = new PipelineBuilder("dlq-probe", provider);
		_ = builder.Use<DeadLetterOnExhaustionMiddleware>();

		// DEFECT FIXED — arm flipped, not deleted, per the instruction this method carried while it pinned.
		// Build() no longer absorbs the refusal: an explicitly-registered middleware (Use<T>) is Required, so
		// a store-less host now fails CLOSED at composition time instead of silently discarding exhausted
		// messages at runtime.
		var ex = Should.Throw<InvalidOperationException>(
			() => builder.Build(),
			"a host that enabled dead-letter routing without a store must fail at build, not discard silently");

		// The message is the deliverable, not just the throw: a consumer who never wired a queue learns
		// WHICH middleware failed and WHAT to do. Asserting the contract, not the prose — a reworded
		// message stays green, a message that stops naming the middleware or the remedy goes red.
		ex.Message.ShouldContain(nameof(DeadLetterOnExhaustionMiddleware));
		ex.Message.ShouldContain("How to fix");
	}

	/// <summary>
	/// DIAGNOSTIC, pending assignment — not a dead-letter arm. Establishes whether the same swallow reaches
	/// <c>AuthorizationMiddleware</c>, whose exclusion from the Default profile is justified in
	/// <c>DefaultPipelineProfiles.cs:59-63</c> as avoiding "a silent authorization bypass", with the stated
	/// mitigation that "authorization is opt-in via the Strict profile, which the consumer deliberately
	/// selects" — while Strict adds it at <c>:92</c>.
	/// </summary>
	[Fact]
	public void PinTheDefect_SilentlyOmitAuthorizationWhenItsServiceIsUnregistered()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddOptions();

		// Deliberately NO IAuthorizationService — a host that selected a security profile but never wired auth.
		using var provider = services.BuildServiceProvider();

		var builder = new PipelineBuilder("strict-probe", provider);
		_ = builder.Use<AuthorizationMiddleware>();

		// DEFECT FIXED ON THIS PATH — arm flipped, not deleted. An explicitly-registered AuthorizationMiddleware
		// is Required, so a pipeline can no longer be built without the authorization service it declares.
		//
		// SCOPE, STATED PRECISELY: this closes the bypass for middleware added via Use<T>/UseAt<T>. The
		// PROFILE-sourced half is now closed too — profile entries carry a criticality and a Required entry
		// that cannot be resolved fails the build; the arm below was flipped when that landed.
		//
		// ONE GAP REMAINS, and it is a different mechanism: Required proves a middleware RESOLVES, not that
		// it APPLIES. Authentication and Authorization are [AppliesTo(Action)], so an Event dispatched
		// through a profile that accepts Events bypasses both while still satisfying the build-time check.
		var ex = Should.Throw<InvalidOperationException>(
			() => builder.Build(),
			"a pipeline that declares authorization must not build without it");

		ex.Message.ShouldContain(nameof(AuthorizationMiddleware));
		ex.Message.ShouldContain("How to fix");
	}

	/// <summary>
	/// DEFECT CLOSED — this arm pinned the profile-sourced bypass until it was fixed, and now enforces the fix.
	/// Middleware added by SELECTING a profile once registered as <c>Optional</c> and was silently null-skipped
	/// when its service was unregistered; profile entries now carry a criticality, so a <c>Required</c> entry
	/// that cannot be resolved fails the build and names the service it needed.
	/// </summary>
	/// <remarks>
	/// This is the consumer-facing path — nobody hand-builds a pipeline and forgets authorization; they select a
	/// profile and never wire its services. It was pinned rather than left RED so the suite stayed honest, and
	/// pinned rather than skipped because a skipped arm passes by being skipped and disappears. When the fix
	/// landed the arm went RED exactly as its own instruction said it must, and was flipped rather than deleted.
	/// <para>
	/// STILL NOT COVERED, and a different mechanism: <c>Required</c> proves a middleware RESOLVES, not that it
	/// APPLIES. This arm dispatches nothing, so it cannot observe applicability at all.
	/// </para>
	/// </remarks>
	[Fact]
	public void PinTheDefect_StillOmitProfileSourcedSecurityMiddleware()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddOptions();

		// Deliberately NO IAuthorizationService — a host that selected a security profile but never wired auth.
		using var provider = services.BuildServiceProvider();

		var profile = new PipelineProfile("probe-strict", MessageKinds.All);
		profile.AddMiddleware<AuthorizationMiddleware>(1);

		var builder = new PipelineBuilder("profile-probe", provider);
		_ = builder.UseProfile(profile);

		// FLIPPED, not deleted — as this test's own remark instructed. Profile entries now carry a
		// criticality, and an unresolvable Required entry fails the build instead of being skipped.
		var ex = Should.Throw<InvalidOperationException>(
			() => builder.Build(),
			"the profile half is fixed: a profile-sourced security middleware that cannot be resolved must fail the build");

		ex.Message.ShouldContain(nameof(AuthorizationMiddleware));
		ex.Message.ShouldContain("How to fix");
	}

	/// <summary>
	/// DIAGNOSTIC — does the container-level startup check that works for the dead-letter middleware also work
	/// for authorization? The published authorization guidance currently has no mitigation that survives the
	/// pipeline's catch, so whether this one generalises decides what that page can honestly recommend.
	/// </summary>
	[Fact]
	public void Diagnostic_LetAContainerLevelStartupCheckDetectTheMissingAuthorizationService()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddOptions();

		// AuthorizationMiddleware has NO DI registration of its own, so resolving it on a bare container
		// throws "No service for type ... has been registered" whether or not IAuthorizationService is
		// present. Asserting only that it throws would pass identically on a correctly-wired host - a check
		// that cannot fail is not a check. The consumer must register the TYPE first; only then does the
		// resolution reach its dependencies, which is the case the mitigation actually rests on.
		_ = services.AddSingleton<AuthorizationMiddleware>();

		using var provider = services.BuildServiceProvider();

		// The container is never asked to build a pipeline, so PipelineBuilder's catch is not in the path.
		var exception = Should.Throw<InvalidOperationException>(
			() => provider.GetRequiredService<AuthorizationMiddleware>());

		exception.Message.ShouldContain(
			"IAuthorizationService",
			Case.Sensitive,
			"the refusal must name the MISSING AUTHORIZATION SERVICE. A throw that merely reports the "
			+ "middleware type is unregistered proves nothing about authorization being wired, and would "
			+ "fire on a fully-configured host");
	}

	/// <summary>
	/// LIVENESS for the arm above — the check must go QUIET on a correctly-wired host. Without this, a
	/// "cure" that throws unconditionally satisfies the safety half and would be published as guidance that
	/// fails for every consumer who followed it correctly.
	/// </summary>
	[Fact]
	public void Diagnostic_LetTheSameStartupCheckPassWhenAuthorizationIsFullyWired()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddOptions();
		_ = services.AddSingleton(A.Fake<IAuthorizationService>());
		_ = services.AddSingleton(A.Fake<ITelemetrySanitizer>());
		_ = services.AddSingleton<AuthorizationMiddleware>();

		using var provider = services.BuildServiceProvider();

		provider.GetRequiredService<AuthorizationMiddleware>().ShouldNotBeNull(
			"a correctly-wired host must resolve cleanly - a startup check that throws either way is not a "
			+ "diagnostic, it is an outage the consumer inflicted on themselves by following our docs");
	}

	/// <summary>
	/// Implements <see cref="IDeadLetterQueue"/> directly, inheriting no first-party base that could supply the
	/// member under test (testing-patterns §3, fixture-shape corollary).
	/// </summary>
	private sealed class RecordingDeadLetterQueue : IDeadLetterQueue
	{
		public Task<Guid> EnqueueAsync<T>(
			T message,
			DeadLetterReason reason,
			CancellationToken cancellationToken,
			Exception? exception = null,
			IDictionary<string, string>? metadata = null) =>
			Task.FromResult(Guid.NewGuid());

		public Task<IReadOnlyList<DeadLetterEntry>> GetEntriesAsync(
			CancellationToken cancellationToken,
			DeadLetterQueryFilter? filter = null,
			int limit = 100) =>
			Task.FromResult<IReadOnlyList<DeadLetterEntry>>(Array.Empty<DeadLetterEntry>());

		public Task<DeadLetterEntry?> GetEntryAsync(Guid entryId, CancellationToken cancellationToken) =>
			Task.FromResult<DeadLetterEntry?>(null);

		public Task<bool> ReplayAsync(Guid entryId, CancellationToken cancellationToken) =>
			Task.FromResult(true);

		public Task<long> GetCountAsync(
			CancellationToken cancellationToken,
			DeadLetterQueryFilter? filter = null) =>
			Task.FromResult(0L);
	}
}
