// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Middleware.Inbox;
using Excalibur.Dispatch.Options.Configuration;
using Excalibur.Dispatch.Serialization;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Tests.Middleware.Inbox;

/// <summary>
/// The inbox stage reports what it did on the context, and the caller that owns finalization reads it.
/// An exit that reports nothing must read as a REFUSAL, never as consent.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the default matters more than any one exit.</b> A drain finalizes an entry unless the stage
/// said it declined — a denylist over three values, whose third value the enumeration itself documents
/// as carrying no information about whether a handler ran. So an exit that forgets to report leaves the
/// caller treating silence as permission: it marks the entry terminally processed and records the
/// identifier as handled, on a dispatch where no handler ran. That is silent, and recording the
/// identifier makes it unrecoverable by retry.
/// </para>
/// <para>
/// <b>What this pins is the direction the mechanism fails in.</b> The stage marks refused on entry and
/// upgrades only beside an actual handler invocation, so a new exit added later — the population this
/// guards, since the current exits are all accounted for — costs a redelivery against an idempotent
/// handler rather than a swallowed message. Loud and recoverable instead of silent and not.
/// </para>
/// <para>
/// <b>What it does NOT cover, stated so nobody reads it as wider than it is.</b> If the stage is never
/// ENTERED, it cannot report anything, and the caller still sees the uninformative value. That happens
/// legitimately when the inbox is disabled, when the message carries no identifier, and when no store
/// or deduplicator is registered — in all three the handler did run through the rest of the pipeline, so
/// finalizing is correct. It also happens when a middleware ordered BEFORE this stage returns a
/// succeeded result without invoking the next delegate, and in that one case the handler did not run.
/// No first-party middleware does that. It is an obligation on a public extension point and it is not
/// closed here.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
public sealed class AnUnmarkedInboxExitMustReadAsRefusedShould
{
	/// <summary>
	/// SAFETY: a suppressed duplicate must report a refusal, so the caller does not finalize it.
	/// </summary>
	/// <returns>The arm.</returns>
	[Fact]
	public async Task ReportRefused_WhenLightModeSuppressesADuplicate()
	{
		var deduplicator = A.Fake<IInMemoryDeduplicator>(o => o.Implements<IClaimableDeduplicator>());

		// THE STATE UNDER TEST: somebody else holds the claim, so this dispatch is suppressed and the
		// handler is never invoked.
		_ = A.CallTo(() => ((IClaimableDeduplicator)deduplicator).TryClaimAsync(
				A<string>._, A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult<LeaseToken?>(null));

		var context = CreateContext();
		var handlerInvoked = false;

		var result = await CreateLightModeMiddleware(deduplicator).InvokeAsync(
			A.Fake<IDispatchMessage>(),
			context,
			(_, _, _) =>
			{
				handlerInvoked = true;
				return new ValueTask<IMessageResult>(Succeeded());
			},
			CancellationToken.None);

		handlerInvoked.ShouldBeFalse("precondition: a suppressed duplicate must not reach the handler");

		result.Succeeded.ShouldBeTrue(
			"and the suppression deliberately surfaces as a SUCCEEDED dispatch, which is exactly why the "
			+ "result cannot be what a caller decides finalization from");

		context.GetOrCreateInboxLeaseFeature().Disposition.ShouldBe(
			InboxLeaseDisposition.Declined,
			"the stage must report the refusal on the context. Left unreported it reads as the "
			+ "uninformative value, and a caller whose guard is a denylist treats that as consent: it "
			+ "finalizes the entry and records the identifier as handled on a dispatch where no handler "
			+ "ran, which nothing downstream can detect and no retry can repair");
	}

	/// <summary>
	/// LIVENESS: an admitted dispatch must report admitted. Without this, a stage that reported a refusal
	/// unconditionally would pass the arm above while no entry was ever finalized.
	/// </summary>
	/// <returns>The arm.</returns>
	[Fact]
	public async Task ReportAdmitted_WhenLightModeRunsTheHandler()
	{
		var deduplicator = A.Fake<IInMemoryDeduplicator>(o => o.Implements<IClaimableDeduplicator>());

		_ = A.CallTo(() => ((IClaimableDeduplicator)deduplicator).TryClaimAsync(
				A<string>._, A<TimeSpan>._, A<CancellationToken>._))
			.Returns(Task.FromResult<LeaseToken?>(new LeaseToken(Guid.NewGuid().ToString("N"))));

		var context = CreateContext();
		var handlerInvoked = false;

		_ = await CreateLightModeMiddleware(deduplicator).InvokeAsync(
			A.Fake<IDispatchMessage>(),
			context,
			(_, _, _) =>
			{
				handlerInvoked = true;
				return new ValueTask<IMessageResult>(Succeeded());
			},
			CancellationToken.None);

		handlerInvoked.ShouldBeTrue("precondition: a won claim must reach the handler");

		context.GetOrCreateInboxLeaseFeature().Disposition.ShouldBe(
			InboxLeaseDisposition.Admitted,
			"a dispatch that reached the handler must report admitted, or the caller never finalizes the "
			+ "entry and it is re-admitted on every drain. Reporting a refusal everywhere would satisfy "
			+ "the safety arm above and strand every message");
	}

	private static InboxMiddleware CreateLightModeMiddleware(IInMemoryDeduplicator deduplicator) =>
		// No store, so the stage takes the light-mode path. That is the branch whose duplicate exit
		// reported nothing before the default was inverted.
		new(
			Microsoft.Extensions.Options.Options.Create(new InboxConfigurationOptions { Enabled = true }),
			inboxStore: null,
			deduplicator,
			new DispatchJsonSerializer(),
			NullLogger<InboxMiddleware>.Instance);

	private static IMessageResult Succeeded()
	{
		var result = A.Fake<IMessageResult>();
		_ = A.CallTo(() => result.Succeeded).Returns(true);
		return result;
	}

	private static IMessageContext CreateContext()
	{
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));
		_ = A.CallTo(() => context.Features).Returns(new Dictionary<Type, object>());
		_ = A.CallTo(() => context.MessageId).Returns("inbox-disposition-msg");
		return context;
	}
}
