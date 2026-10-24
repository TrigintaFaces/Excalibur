// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Drives EVERY registered projection-processing mode from the one pull entry point.
/// </summary>
/// <remarks>
/// <para>
/// <b>The pull API exists for hosts that cannot run a background service -- serverless above all -- and
/// it used to resolve to the ASYNC host alone.</b> Inline projections run on their own ordered host, so
/// in such a host nothing ever drove them: the background loop that normally recovers an incomplete
/// fold is an <c>IHostedService</c>, which a host-less container never starts. A timed-out fold was then
/// recovered only by the NEXT save, and the last save before the process went idle was never recovered
/// at all.
/// </para>
/// <para>
/// That contradicted the pull API's own stated purpose -- preventing a function app from discovering at
/// deploy time that its projections never advance -- for exactly the mode whose consumers were promised
/// read-your-writes. One entry point now drives everything registered, so a host that calls it is
/// complete regardless of which modes it uses.
/// </para>
/// <para>
/// A mode that is not registered resolves to <see langword="null"/> and is skipped, so this costs
/// nothing for a host that uses only one of them.
/// </para>
/// </remarks>
internal sealed class CompositeProjectionProcessor : IProjectionProcessor
{
	private readonly AsyncProjectionProcessingHost? _async;
	private readonly InlineProjectionCoordinator? _inline;

	public CompositeProjectionProcessor(
		AsyncProjectionProcessingHost? asyncHost,
		InlineProjectionCoordinator? inlineCoordinator)
	{
		_async = asyncHost;
		_inline = inlineCoordinator;
	}

	/// <summary>The composed async host, or null when no async processing is registered.</summary>
	/// <remarks>
	/// Exposed for the arm that binds "one cursor owner": the pull API and the background worker must
	/// drive the SAME host instance so a manual drive and a background pass serialise against each other
	/// through its page gate rather than racing. That property used to be asserted as reference identity
	/// between the interface and the host, which stopped being expressible once the interface became a
	/// composite -- but the property itself is unchanged and still worth binding.
	/// </remarks>
	internal AsyncProjectionProcessingHost? AsyncHost => _async;

	/// <inheritdoc />
	public async Task<ProjectionProcessingResult> ProcessPendingAsync(CancellationToken cancellationToken)
	{
		long applied = 0;
		var outcome = ProjectionProcessingOutcome.CaughtUp;
		var reason = ProjectionHaltReason.None;

		if (_async is not null)
		{
			var result = await _async.ProcessPendingAsync(cancellationToken).ConfigureAwait(false);
			applied += result.EventsApplied;
			outcome = result.Outcome;
			reason = result.HaltReason;
		}

		if (_inline is not null)
		{
			var result = await _inline.ProcessPendingAsync(cancellationToken).ConfigureAwait(false);
			applied += result.EventsApplied;

			// THE FIRST NON-CAUGHT-UP OUTCOME WINS. Reporting the last one would let a mode that happened
			// to finish cleanly mask a halt in the other, and a caller acting on "caught up" when half its
			// projections are stuck is the dishonest success this subsystem refuses everywhere else.
			// Both modes are still driven first: neither is abandoned because the other stopped.
			if (outcome == ProjectionProcessingOutcome.CaughtUp)
			{
				outcome = result.Outcome;
				reason = result.HaltReason;
			}
		}

		return new ProjectionProcessingResult(applied, outcome, reason);
	}
}

/// <summary>Registers the one pull entry point, from either mode's registration path.</summary>
/// <remarks>
/// Called by BOTH the async processor registration and the Inline coordinator registration, because
/// either may be the only one a consumer uses. It must be idempotent and order-independent: the
/// composite resolves each mode lazily, so whichever path registers first produces the same object
/// graph. Resolving the modes eagerly here instead would make the result depend on call order -- the
/// first-wins displacement trap that <c>TryAdd</c> sets for anything carrying a guarantee.
/// </remarks>
internal static class ProjectionProcessorRegistration
{
	internal static void EnsureRegistered(IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<IProjectionProcessor>(sp => new CompositeProjectionProcessor(
			sp.GetService<AsyncProjectionProcessingHost>(),
			sp.GetService<InlineProjectionCoordinator>()));
	}
}
