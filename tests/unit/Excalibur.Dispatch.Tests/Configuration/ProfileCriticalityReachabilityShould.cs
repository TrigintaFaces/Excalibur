using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Tests.Configuration;

/// <summary>
/// Binds the reachability property for profile-declared middleware criticality.
/// </summary>
/// <remarks>
/// <para>
/// The pipeline's fail-closed machinery is fully written and currently unreachable. <c>UseProfile</c>
/// binds every profile entry with a hardcoded <c>Optional</c> criticality, and it is the only producer,
/// so the <c>Required</c> branch that collects unresolved middleware and throws can never be taken from
/// a profile. A security middleware a profile declares but nothing registers is therefore skipped in
/// silence, and the pipeline is built without it.
/// </para>
/// <para>
/// WHY THIS ASSERTS REACHABILITY RATHER THAN POPULATION. The intuitive assertion is "the profile
/// materializes every middleware it declares". That assertion is satisfied by a still-broken fix:
/// register the missing middleware while criticality stays hardcoded optional and the count goes green
/// while the pipeline remains fail-open by construction, because deleting any one registration later
/// silently skips it again. Population asserts the instance; only reachability asserts the mechanism.
/// </para>
/// <para>
/// PAIRED ARMS. The safety arm alone is satisfied by a builder that refuses everything, which would
/// brick every consumer, so it is paired with a liveness arm proving a correctly registered profile
/// still builds. A failing safety arm means the bypass is open; a failing liveness arm means the fix
/// over-corrected.
/// </para>
/// <para>
/// This type deliberately binds only the observable behaviour of the public builder surface. It does
/// not reference any type the fix is expected to introduce: a test compiled against an API that does
/// not exist yet fails the whole assembly and takes every unrelated test down with it.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProfileCriticalityReachabilityShould
{
	/// <summary>
	/// A middleware that is never registered in the container, so resolving it yields null.
	/// </summary>
	private sealed class UnregisteredSecurityMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => null;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken) => nextDelegate(message, context, cancellationToken);
	}

	/// <summary>
	/// A middleware that IS registered, used to prove the builder still succeeds.
	/// </summary>
	private sealed class RegisteredHarmlessMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => null;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken) => nextDelegate(message, context, cancellationToken);
	}

	private static ServiceProvider EmptyContainer() => new ServiceCollection().BuildServiceProvider();

	/// <summary>
	/// SAFETY. A profile that declares a middleware nothing can resolve must fail the build loudly.
	/// </summary>
	/// <remarks>
	/// RED until profile entries can carry criticality: today <c>UseProfile</c> hardcodes optional, the
	/// null resolution is logged and skipped, and <c>Build()</c> returns a pipeline that silently lacks
	/// the declared protection. That silent success is the bypass, expressed as a passing build.
	/// </remarks>
	[Fact]
	public void FailTheBuildWhenAProfileDeclaresMiddlewareNothingCanResolve()
	{
		var profile = new PipelineProfile("reachability-safety", MessageKinds.All);
		profile.AddMiddleware<UnregisteredSecurityMiddleware>(1);

		using var provider = EmptyContainer();
		var builder = new PipelineBuilder("reachability-safety", provider).UseProfile(profile);

		var build = () => builder.Build();

		build.ShouldThrow<InvalidOperationException>(
			"a profile that declares a middleware the container cannot resolve must fail closed at build "
			+ "time rather than returning a pipeline that silently omits it");
	}

	/// <summary>
	/// LIVENESS. A profile whose declared middleware resolves must still build.
	/// </summary>
	/// <remarks>
	/// Without this arm the safety arm above is satisfied by a builder that refuses every profile, which
	/// would fail closed by failing entirely. This is the arm that catches the over-correction.
	/// </remarks>
	[Fact]
	public void StillBuildWhenEveryDeclaredMiddlewareResolves()
	{
		var profile = new PipelineProfile("reachability-liveness", MessageKinds.All);
		profile.AddMiddleware<RegisteredHarmlessMiddleware>(1);

		var services = new ServiceCollection();
		services.AddSingleton<RegisteredHarmlessMiddleware>();
		using var provider = services.BuildServiceProvider();

		var builder = new PipelineBuilder("reachability-liveness", provider).UseProfile(profile);

		var pipeline = builder.Build();

		pipeline.ShouldNotBeNull(
			"a profile whose declared middleware all resolve must build normally; failing here would mean "
			+ "the fail-closed change bricks every correctly configured consumer");
	}

	/// <summary>
	/// SAFETY. The shipped strict profile must fail closed when its security middleware cannot be
	/// materialized.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This binds the decision itself rather than the mechanism that implements it. The strict profile
	/// declares throttling, authentication, input sanitization and authorization; the framework
	/// registers none of them. Today the pipeline is built without all four and reports success, so a
	/// host that selected the strictest available profile for external and partner traffic receives
	/// unauthenticated, unauthorized, unsanitized dispatch and is told its protection is in place.
	/// </para>
	/// <para>
	/// The profile's own shipped documentation promises full validation, authentication and
	/// authorization. Silently building without them contradicts a published contract, which is why
	/// this arm asserts a throw rather than a skip. It is deliberately indifferent to HOW criticality
	/// is expressed: any implementation under which the strict profile can be built while its security
	/// middleware are absent fails here, whatever the seam looks like.
	/// </para>
	/// </remarks>
	[Fact]
	public void FailClosedWhenTheShippedStrictProfileCannotMaterializeItsSecurityMiddleware()
	{
		var strict = DefaultPipelineProfiles.CreateStrictProfile();

		using var provider = EmptyContainer();
		var builder = new PipelineBuilder("strict-fail-closed", provider).UseProfile(strict);

		var build = () => builder.Build();

		build.ShouldThrow<InvalidOperationException>(
			"the strict profile declares authentication, authorization, input sanitization and "
			+ "throttling, and its own documentation promises them; if none of them can be resolved "
			+ "the pipeline must refuse to start rather than silently serve external traffic with no "
			+ "security middleware at all");
	}

	/// <summary>
	/// LIVENESS. The shipped default profile must still build with no middleware registered.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the arm that fails if fail-closed is applied indiscriminately. The default profile is
	/// what a consumer gets from a bare registration with no configuration at all, and it declares
	/// seven middleware the framework does not register. If those are treated as required, every
	/// zero-configuration host stops starting — the framework would refuse to run out of the box.
	/// </para>
	/// <para>
	/// Paired with the strict-profile arm above, the two express the whole decision: the profile a
	/// consumer selects deliberately for hostile traffic fails closed, and the profile a consumer
	/// gets by default keeps working. An implementation that made every entry required would satisfy
	/// the strict arm and fail here, which is precisely the discrimination this arm exists to provide.
	/// </para>
	/// </remarks>
	[Fact]
	public void StillBuildTheShippedDefaultProfileWhenNoMiddlewareIsRegistered()
	{
		var @default = DefaultPipelineProfiles.CreateDefaultProfile();

		using var provider = EmptyContainer();
		var builder = new PipelineBuilder("default-liveness", provider).UseProfile(@default);

		var pipeline = builder.Build();

		pipeline.ShouldNotBeNull(
			"the default profile is what a consumer gets with no configuration; if it refuses to "
			+ "build when its declared middleware are unregistered, the framework cannot start out "
			+ "of the box and fail-closed has been applied where it does not belong");
	}

	/// <summary>
	/// LIVENESS. The shipped internal-event profile must still build with no middleware registered.
	/// </summary>
	/// <remarks>
	/// Present because covering only the default profile would let a partial fix turn this suite green
	/// while other shipped profiles remain unable to start. This profile declares no authentication,
	/// authorization, sanitization or throttling, so it carries no fail-closed obligation; treating its
	/// entries as required would brick it for the same reason it would brick the default profile, and
	/// nothing would report that until a consumer selected it.
	/// </remarks>
	[Fact]
	public void StillBuildTheShippedInternalEventProfileWhenNoMiddlewareIsRegistered()
	{
		var internalEvents = DefaultPipelineProfiles.CreateInternalEventProfile();

		using var provider = EmptyContainer();
		var builder = new PipelineBuilder("internal-event-liveness", provider).UseProfile(internalEvents);

		builder.Build().ShouldNotBeNull(
			"the internal-event profile declares no security middleware, so it has no reason to fail "
			+ "closed; if it cannot build unregistered then fail-closed has been applied to every "
			+ "shipped profile indiscriminately rather than to the ones that promise protection");
	}

	/// <summary>
	/// LIVENESS. The shipped batch profile must still build with no middleware registered.
	/// </summary>
	/// <remarks>
	/// The second of the two shipped profiles that no arm covered. Same reasoning as the internal-event
	/// profile: no security middleware is declared, so nothing here justifies refusing to start.
	/// </remarks>
	[Fact]
	public void StillBuildTheShippedBatchProfileWhenNoMiddlewareIsRegistered()
	{
		var batch = DefaultPipelineProfiles.CreateBatchProfile();

		using var provider = EmptyContainer();
		var builder = new PipelineBuilder("batch-liveness", provider).UseProfile(batch);

		builder.Build().ShouldNotBeNull(
			"the batch profile declares no security middleware; refusing to build it unregistered "
			+ "would stop a consumer who selected batching from starting, with no security benefit");
	}

	/// <summary>
	/// LIVENESS. An entry declared explicitly optional is skipped when it cannot be materialized.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The arm that proves criticality is genuinely per-entry rather than uniform. Every other arm here
	/// is satisfied by an implementation that treats one criticality as universal — the shipped profiles
	/// could each happen to want the value they get, and nothing would notice the distinction had been
	/// lost. This one constructs the case directly: one entry, declared optional by the caller, whose
	/// middleware is absent from the container.
	/// </para>
	/// <para>
	/// If a profile author can say "optional" and be overruled, the declaration is decorative and the
	/// contract belongs to whoever wrote the default rather than to whoever wrote the profile.
	/// </para>
	/// </remarks>
	[Fact]
	public void SkipAnEntryDeclaredExplicitlyOptionalWhenItsMiddlewareCannotBeResolved()
	{
		var profile = new PipelineProfile("explicit-optional", MessageKinds.All);
		profile.AddMiddleware<UnregisteredSecurityMiddleware>(1, MiddlewareCriticality.Optional);

		using var provider = EmptyContainer();
		var builder = new PipelineBuilder("explicit-optional", provider).UseProfile(profile);

		builder.Build().ShouldNotBeNull(
			"an entry the profile author declared optional must be skipped when it cannot resolve; if "
			+ "this throws, the declared criticality was ignored and 'optional' is not expressible");
	}

	/// <summary>
	/// LIVENESS. The shipped direct profile must still build with no middleware registered.
	/// </summary>
	/// <remarks>
	/// The fifth and last shipped profile. It declares no middleware today, so it cannot currently fail
	/// — which is exactly why it is worth naming. The synthetic empty-profile arm below covers the
	/// <em>shape</em> of a profile with no entries; it does not cover <em>this</em> profile, so a future
	/// entry added here would be unasserted. Binding the shipped factory means the day someone gives
	/// this profile a middleware, it inherits the same guarantee as the other four rather than silently
	/// becoming the one nothing watches.
	/// </remarks>
	[Fact]
	public void StillBuildTheShippedDirectProfileWhenNoMiddlewareIsRegistered()
	{
		var direct = DefaultPipelineProfiles.CreateDirectProfile();

		using var provider = EmptyContainer();
		var builder = new PipelineBuilder("direct-liveness", provider).UseProfile(direct);

		builder.Build().ShouldNotBeNull(
			"the direct profile is a shipped profile like any other; asserting it by name means a "
			+ "middleware added to it later is covered on the day it is added, not the day it breaks");
	}

	/// <summary>
	/// A profile implemented directly against the interface, as a consumer outside this assembly would
	/// write one, returning exactly the entries it is handed.
	/// </summary>
	/// <remarks>
	/// This fixture inherits no first-party profile base. A test that reached the interface's contract
	/// through <see cref="PipelineProfile" /> would be re-testing that class, which constructs every entry
	/// correctly and so can never produce the value under test here. Only an implementation written from
	/// scratch can hand back an entry this assembly did not build.
	/// </remarks>
	private sealed class ExternallyImplementedProfile(string name, params MiddlewareEntry[] entries) : IPipelineProfile
	{
		public string Name { get; } = name;

		public string Description => "A profile implemented directly by a consumer.";

		public IReadOnlyList<MiddlewareEntry> MiddlewareEntries { get; } = entries;

		public bool IsStrict => false;

		public MessageKinds SupportedMessageKinds => MessageKinds.All;

		public IReadOnlyList<Type> GetApplicableMiddleware(MessageKinds messageKind) =>
			MiddlewareEntries.Select(static e => e.MiddlewareType).ToList();

		public IReadOnlyList<Type> GetApplicableMiddleware(
			MessageKinds messageKind,
			IReadOnlySet<DispatchFeatures> enabledFeatures) => GetApplicableMiddleware(messageKind);
	}

	/// <summary>
	/// SAFETY. An entry that never ran the constructor must not be resolved to a criticality nobody stated.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The constructor default is <see cref="MiddlewareCriticality.Required" /> and always was, which is
	/// precisely why the previous arms cannot reach this defect: every one of them constructs its entries.
	/// A struct's constructor is bypassable, so an external profile can hand back an all-zeroes entry —
	/// from a default value, or from an array slot a filter left unwritten — with no compiler complaint.
	/// </para>
	/// <para>
	/// This arm binds <c>default(MiddlewareEntry)</c> itself. It is RED against any build where the zero
	/// value of the criticality enum is a real criticality, because the entry is then silently skippable
	/// security middleware that no author ever declared optional.
	/// </para>
	/// </remarks>
	[Fact]
	public void FailTheBuildWhenAProfileDeclaresAnEntryThatStatesNoCriticality()
	{
		// default(MiddlewareEntry), stated explicitly: a bare `default` here binds to the params array
		// itself and hands the builder a null list, which fails for an unrelated reason and would have
		// made this arm pass without ever exercising the entry.
		var profile = new ExternallyImplementedProfile("external-unstated", default(MiddlewareEntry));

		using var provider = EmptyContainer();
		var builder = new PipelineBuilder("external-unstated", provider);

		var useThenBuild = () => builder.UseProfile(profile).Build();

		useThenBuild.ShouldThrow<InvalidOperationException>(
			"an entry produced without running MiddlewareEntry's constructor states no criticality and "
			+ "names no middleware; building from it would let the enum's zero value decide silently "
			+ "whether a middleware may be skipped, which is the omission this contract exists to refuse");
	}

	/// <summary>
	/// SAFETY. The unstated criticality must fail the build on its own, with the type present.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The arm above is not sufficient by itself and must not be trusted alone. <c>default(MiddlewareEntry)</c>
	/// is unsafe in <em>two</em> independent ways — no middleware type, and no stated criticality — so a
	/// builder that rejected it for the missing type alone would satisfy that arm while leaving the
	/// criticality hole completely open. The assertion cannot tell the two apart, which makes it blind in
	/// exactly the direction this fix exists to close.
	/// </para>
	/// <para>
	/// This arm isolates the criticality half: the entry names a real middleware and differs from a valid
	/// one only in that nobody stated whether it may be skipped. It is the arm that goes RED if the zero
	/// value of the enum is ever moved back onto a real criticality, because the entry then reads as a
	/// deliberate declaration that this middleware is optional.
	/// </para>
	/// </remarks>
	[Fact]
	public void FailTheBuildWhenAnEntryNamesItsMiddlewareButStatesNoCriticality()
	{
		var unstated = default(MiddlewareEntry) with { MiddlewareType = typeof(UnregisteredSecurityMiddleware) };

		unstated.MiddlewareType.ShouldNotBeNull(
			"this arm is only meaningful while the type is present; if it is null the assertion below could "
			+ "be satisfied by the missing-type rejection instead of the one under test");

		var profile = new ExternallyImplementedProfile("external-type-without-criticality", unstated);

		using var provider = EmptyContainer();
		var builder = new PipelineBuilder("external-type-without-criticality", provider);

		var useThenBuild = () => builder.UseProfile(profile).Build();

		useThenBuild.ShouldThrow<InvalidOperationException>(
			"an entry that names a middleware but states no criticality must fail the build; if the zero "
			+ "value of the criticality enum is a real criticality, this entry silently becomes that "
			+ "criticality and a middleware nobody declared optional is skipped without a word");
	}

	/// <summary>
	/// LIVENESS. A directly-implemented profile whose entries are properly stated must still build.
	/// </summary>
	/// <remarks>
	/// Without this arm the safety arm above is satisfied by a builder that rejects every externally
	/// implemented profile outright — fail-closed by refusing the whole extension point. This arm states
	/// that implementing <see cref="IPipelineProfile" /> yourself remains a supported thing to do, and
	/// that the rejection is attributable to the unstated entry rather than to the profile's authorship.
	/// </remarks>
	[Fact]
	public void StillBuildAnExternallyImplementedProfileWhoseEntriesStateTheirCriticality()
	{
		var profile = new ExternallyImplementedProfile(
			"external-stated",
			new MiddlewareEntry(typeof(UnregisteredSecurityMiddleware), MiddlewareCriticality.Optional));

		using var provider = EmptyContainer();
		var builder = new PipelineBuilder("external-stated", provider).UseProfile(profile);

		builder.Build().ShouldNotBeNull(
			"a consumer implementing the profile interface directly and stating criticality on every entry "
			+ "must still get a pipeline; if this throws, the fix closed the extension point rather than "
			+ "the hole in it");
	}

	/// <summary>
	/// LIVENESS. An empty profile must build, so the failure is attributable to the unresolved entry.
	/// </summary>
	/// <remarks>
	/// Without this arm, a builder that threw on every profile regardless of content would satisfy the
	/// safety arm for the wrong reason and the suite could not tell the two apart.
	/// </remarks>
	[Fact]
	public void StillBuildWhenAProfileDeclaresNoMiddlewareAtAll()
	{
		var profile = new PipelineProfile("reachability-empty", MessageKinds.All);

		using var provider = EmptyContainer();
		var builder = new PipelineBuilder("reachability-empty", provider).UseProfile(profile);

		var pipeline = builder.Build();

		pipeline.ShouldNotBeNull(
			"an empty profile has nothing unresolvable in it, so it must build; this isolates the safety "
			+ "arm's failure to the unresolved entry rather than to profile use in general");
	}
}
