// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.CloudNative;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Outbox.DependencyInjection;

/// <summary>
/// Single source of truth for the invariant that <strong>at most one delivery mechanism is authorised
/// to publish any given outbox message</strong>, and for the message that names the remedy.
/// </summary>
/// <remarks>
/// <para>
/// A change-feed subscription and a claim-based drain are <em>alternatives, not layers</em>. The
/// trigger path does not observe the claim's lease, so a host that runs both publishes some messages
/// twice, with no fault raised anywhere. The interleaving needs no crash, no pause and no clock skew:
/// the feed delivers a message and its handler begins publishing; the poller claims the same message
/// successfully, because the handler is not a claimant and the claim's exclusion set therefore says
/// nothing about it; both publish.
/// </para>
/// <para>
/// <strong>The obvious repair does not work, and is recorded here so it is not attempted.</strong>
/// Projecting the lease fields into the change-feed document lets the handler read them, but a read
/// is not a conditional write: reorder the interleaving so the handler reads an unleased message
/// before the poller claims it, and both still publish. That is check-then-act split at the
/// interleaving point, and a lease observed outside the atomic step that compares it is stale by the
/// time it is acted on. Only exclusivity of the mechanisms themselves closes it.
/// </para>
/// <para>
/// The check therefore refuses the composition at host start rather than trying to make two
/// publishers agree at run time. Its predicate is the pair of <em>registrations</em>, because that is
/// the decision a consumer actually makes and the only point at which both are visible together.
/// </para>
/// <para>
/// <strong>Known limit, stated rather than implied:</strong> this sees what the container was told
/// about. A consumer who constructs a subscription directly and starts it themselves — never
/// registering it — is outside the container's knowledge and cannot be refused here. That host is
/// still wrong, and the documented guarantee still governs it; the refusal simply cannot reach it.
/// </para>
/// </remarks>
internal static class OutboxDeliveryExclusivity
{
	/// <summary>
	/// The message shown when both delivery mechanisms are registered for one outbox. It names both
	/// registrations and asks for a choice, because either one alone is correct and the framework
	/// cannot know which the consumer meant.
	/// </summary>
	internal const string BothMechanismsRegisteredMessage =
		"Two outbox delivery mechanisms are registered for the same outbox, and they are alternatives " +
		"rather than layers: a change-feed subscription (IChangeFeedSubscription<CloudOutboxMessage>) " +
		"and a claim-based drain (the OutboxBackgroundService hosted service registered by AddOutbox). " +
		"The change-feed trigger does not observe the drain's claim lease, so a message delivered by " +
		"the feed can also be claimed and published by the drain — publishing it twice, with no error " +
		"raised. Register exactly one: keep the change-feed subscription and disable the background " +
		"drain, or keep the drain and do not register the subscription. Both are supported; running " +
		"both is not.";

	/// <summary>
	/// Returns <see langword="true"/> when the host has registered both mechanisms for one outbox.
	/// </summary>
	/// <param name="services">The built provider to probe.</param>
	/// <remarks>
	/// AOT-safe: two <c>GetService</c> probes, no reflection and no assembly scanning. Resolving the
	/// subscription contract does not construct a hosted service, and the drain is detected through a
	/// marker registered beside it rather than by resolving <c>IEnumerable&lt;IHostedService&gt;</c>,
	/// which would construct every hosted service in the host merely to ask whether one was present.
	/// </remarks>
	internal static bool BothMechanismsRegistered(IServiceProvider services) =>
		services.GetService<OutboxDrainMarker>() is not null
		&& services.GetService<IChangeFeedSubscription<CloudOutboxMessage>>() is not null;
}

/// <summary>
/// Marker registered beside the claim-based drain so its presence can be observed without
/// constructing it.
/// </summary>
/// <remarks>
/// The same shape ASP.NET Core uses to detect whether a feature's registration call was made. The
/// alternative — resolving <c>IEnumerable&lt;IHostedService&gt;</c> and inspecting it — would
/// instantiate every hosted service in the host as a side effect of a startup probe, which is a
/// heavier and less predictable act than the question warrants.
/// </remarks>
internal sealed class OutboxDrainMarker;
