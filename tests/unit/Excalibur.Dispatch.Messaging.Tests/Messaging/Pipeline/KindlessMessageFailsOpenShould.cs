// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.Messaging.Pipeline;

/// <summary>
/// Demonstrates — by execution rather than by source reading — that a message implementing only the
/// public <see cref="IDispatchMessage"/> base interface classifies as <see cref="MessageKinds.All"/> —
/// so every middleware applies to it — rather than defaulting to <see cref="MessageKinds.Document"/>,
/// the kind that the largest number of security middleware exclude.
/// </summary>
/// <remarks>
/// <para>
/// <b>These arms previously pinned a fail-open default.</b> An unclassifiable message was classified as
/// <c>Document</c> and so bypassed the authentication, authorization, and validation middleware. It now
/// fails closed instead, and these arms were inverted rather than deleted: a deletion and an inversion
/// produce identical green suites, and only one of them keeps the defect detectable.
/// </para>
/// <para>
/// <b>Why this is written by execution.</b> The classification path was confirmed by three independent
/// source reads before this file existed. Three reads of the same code are three reads; none of them ran
/// it. If the strategy's behaviour differs from what the source appears to say — a cache, an override, a
/// short-circuit upstream — these arms fail and the source reading was wrong. That is the point.
/// </para>
/// <para>
/// <b>Both arms (testing-patterns §3).</b> The safety arm shows the kindless type reaches <c>All</c>.
/// The liveness arm shows the same strategy still classifies a well-formed action as <c>Action</c> —
/// without it, a strategy that returned <c>All</c> for every input would satisfy the safety arm and
/// prove nothing.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class KindlessMessageFailsOpenShould
{
	/// <summary>A message a consumer can write today: implements the public base interface, nothing else.</summary>
	private sealed class KindlessMessage : IDispatchMessage;

	/// <summary>A well-formed action, for the control arm.</summary>
	private sealed class WellFormedAction : IDispatchAction;

	/// <summary>
	/// SAFETY — a type implementing only <see cref="IDispatchMessage"/> classifies as
	/// <see cref="MessageKinds.All"/>, so every middleware applies to it. Previously it reached the
	/// <c>None -> Document</c> default and silently bypassed the security middleware.
	/// </summary>
	[Fact]
	public void ClassifyAKindlessMessageAsEveryKind()
	{
		var kinds = DefaultMiddlewareApplicabilityStrategy.DetermineMessageKinds(typeof(KindlessMessage));

		kinds.ShouldBe(
			MessageKinds.All,
			"an unclassifiable message is the one we know least about, so it must receive the most "
			+ "protection — not the Document default, which the most security middleware exclude");
	}

	/// <summary>
	/// CONTROL / LIVENESS — the same strategy classifies a well-formed action correctly. Without this arm,
	/// a strategy that returned Document for every input would satisfy the demonstration arm above.
	/// </summary>
	[Fact]
	public void StillClassifyAWellFormedActionAsAction()
	{
		var kinds = DefaultMiddlewareApplicabilityStrategy.DetermineMessageKinds(typeof(WellFormedAction));

		kinds.ShouldBe(
			MessageKinds.Action,
			"the strategy must still classify correctly — otherwise the Document result above is vacuous");
	}

	/// <summary>
	/// CONSEQUENCE CLOSED — the replacement for the arm that pinned the bypass. It previously asserted that
	/// <c>AuthorizationMiddleware</c> excluded the kind an unclassifiable message was defaulted to. There is
	/// no longer such a kind, so this arm now asserts the other half: authorization applies to a message
	/// that classifies honestly.
	/// </summary>
	/// <remarks>
	/// This arm is a REPLACEMENT rather than a deletion. Deleting it and inverting it produce identical
	/// green suites, but only the replacement fails if authorization is later narrowed back to a kind that
	/// a well-formed message does not land on.
	/// </remarks>
	[Fact]
	public void ApplyAuthorizationToTheKindAKindlessMessageLandsOn()
	{
		var strategy = new DefaultMiddlewareApplicabilityStrategy();
		var kindlessKinds = DefaultMiddlewareApplicabilityStrategy.DetermineMessageKinds(typeof(KindlessMessage));

		// 707x40: source the applicable kinds from the middleware's ApplicableMessageKinds PROPERTY — the
		// production source IMiddlewareApplicabilityStrategy actually reads — NOT the [AppliesTo] attribute
		// via reflection. Reading the attribute would leave this arm green if the property (production) ever
		// diverged from the annotation. The ctor dependencies are never touched when reading the property.
		var authorizationMiddleware = new Excalibur.Dispatch.Middleware.Auth.AuthorizationMiddleware(
			Microsoft.Extensions.Options.Options.Create(new Excalibur.Dispatch.Options.Middleware.AuthorizationOptions()),
			A.Fake<Excalibur.Dispatch.Middleware.Auth.IAuthorizationService>(),
			A.Fake<Excalibur.Dispatch.Telemetry.ITelemetrySanitizer>(),
			Microsoft.Extensions.Logging.Abstractions.NullLogger<Excalibur.Dispatch.Middleware.Auth.AuthorizationMiddleware>.Instance);
		var authorizationApplicability = authorizationMiddleware.ApplicableMessageKinds;

		// Route the question through ShouldApplyMiddleware — the decision function the pipeline itself calls
		// (via IsMiddlewareApplicable) — rather than a bitwise test here: a special case inside
		// ShouldApplyMiddleware would otherwise leave this arm green while production behaved differently.
		strategy.ShouldApplyMiddleware(authorizationApplicability, kindlessKinds).ShouldBeTrue(
			"the bypass is closed: authorization now applies to the kind an unclassifiable message lands "
			+ "on, where it previously excluded it");
	}

	/// <summary>
	/// DETECTION — the fall-through emits its observable signal, naming the offending type.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This arm exists because <b>the fix is otherwise undetectable by the suite</b>. Failing closed to
	/// <c>All</c> is permissive, so no pre-existing test changed behaviour when it landed — zero fixtures
	/// broke. A suite that looks identical with and without the fix cannot notice its removal. The signal
	/// is the one emitted artifact that distinguishes the two worlds, so it is what this binds.
	/// </para>
	/// <para>
	/// It also binds the signal to the <i>type name</i>. A signal that fires without naming the offender
	/// sends the developer to the wrong place: they see a message that was permitted everywhere and the
	/// actual cause is a missing interface on a type this event is the only thing that identifies.
	/// </para>
	/// </remarks>
	[Fact]
	public void EmitAnObservableSignalNamingTheUnclassifiedType()
	{
		using var activity = StartRecordedActivity(out var listener);
		using (listener)
		{
			_ = DefaultMiddlewareApplicabilityStrategy.DetermineMessageKinds(typeof(KindlessMessage));

			var signals = activity.Events.Where(e => e.Name == UnclassifiedSignalName).ToList();

			signals.Count.ShouldBe(
				1,
				"the fall-through must record exactly one signal — silence is how the unprotected-message "
				+ "defect survived, and a duplicate would mean the fall-through ran twice");

			signals[0].Tags.ShouldContain(
				tag => tag.Key == "dispatch.message.type"
					&& string.Equals((string?)tag.Value, typeof(KindlessMessage).FullName, StringComparison.Ordinal),
				"the signal must name the offending type — without it a developer sees only that something "
				+ "was unclassified and has no way to find which type to fix");
		}
	}

	/// <summary>
	/// LIVENESS for the signal — a well-formed message emits NOTHING.
	/// </summary>
	/// <remarks>
	/// Without this arm, a fall-through that recorded the signal on <i>every</i> classification would
	/// satisfy the detection arm above while carrying no information at all. This is the arm that makes
	/// the signal mean "this type is unclassified" rather than "a classification happened".
	/// </remarks>
	[Fact]
	public void NotEmitTheSignalForAWellFormedMessage()
	{
		using var activity = StartRecordedActivity(out var listener);
		using (listener)
		{
			_ = DefaultMiddlewareApplicabilityStrategy.DetermineMessageKinds(typeof(WellFormedAction));

			activity.Events.ShouldNotContain(
				e => e.Name == UnclassifiedSignalName,
				"a classified type must produce no unclassified signal — otherwise the signal reports that a "
				+ "classification occurred rather than that one failed, and it is noise");
		}
	}

	/// <summary>The event name the fall-through records. Duplicated here deliberately: see remarks.</summary>
	/// <remarks>
	/// The production constant is <c>private</c>, so this string is a copy. That is intended — the name is
	/// consumed by dashboards and alerts outside this repository, which cannot import a constant either.
	/// If someone renames it in production, this lock fails, which is the correct outcome for a rename of
	/// something consumers query by string.
	/// </remarks>
	private const string UnclassifiedSignalName = "dispatch.message.unclassified";

	/// <summary>
	/// Starts an Activity that is guaranteed to be recording, so <c>Activity.Current</c> is non-null when
	/// the seam looks for it.
	/// </summary>
	/// <remarks>
	/// The null-check is load-bearing, not defensive. The seam records only when <c>Activity.Current</c> is
	/// non-null; if sampling were misconfigured, <c>StartActivity</c> would return <see langword="null" />,
	/// every "no signal" assertion would pass for the wrong reason, and both arms above would be vacuous.
	/// </remarks>
	private static Activity StartRecordedActivity(out ActivityListener listener)
	{
		var source = new ActivitySource($"{nameof(KindlessMessageFailsOpenShould)}.{Guid.NewGuid():N}");

		listener = new ActivityListener
		{
			ShouldListenTo = candidate => ReferenceEquals(candidate, source),
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
		};

		ActivitySource.AddActivityListener(listener);

		var activity = source.StartActivity("classify");

		activity.ShouldNotBeNull(
			"the listener must sample, or Activity.Current is null, the seam records nothing, and every "
			+ "assertion in this file about signals passes for the wrong reason");

		return activity;
	}
}
