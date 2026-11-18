// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Net;

using Excalibur.Dispatch.LeaderElection.Fencing;

using k8s;
using k8s.Autorest;

using Microsoft.Extensions.Options;

namespace Excalibur.LeaderElection.Kubernetes;

/// <summary>
/// Kubernetes-backed <see cref="IFencingTokenProvider"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// Unlike the self-minting providers (Postgres/Redis/SqlServer/Mongo/Consul), Kubernetes exposes an
/// API-server-authoritative monotonic counter directly on the Lease: <c>Lease.spec.leaseTransitions</c>,
/// which <see cref="KubernetesLeaderElection"/> advances on every leadership transition. This provider
/// therefore <b>reads</b> that native counter rather than minting its own (avoiding a second source of
/// truth), per the leader-election fencing seam ruling.
/// </para>
/// <para>
/// Validation is fail-closed against the current value (Kleppmann's fencing-token pattern): a token is
/// accepted only when it is at or above the current <c>leaseTransitions</c>, rejecting a stale leader's
/// lower token. Fencing tokens are strictly positive, so a <c>leaseTransitions</c> of zero or absent is
/// <b>unset</b> rather than a mark of zero and refuses every token -- a mark of zero read as a value would
/// admit every non-negative token presented against it.
/// </para>
/// <para>
/// <b>Exhaustion:</b> <c>leaseTransitions</c> is a 32-bit integer. When it has reached
/// <see cref="int.MaxValue"/> the next transition would overflow, so the election relinquishes rather
/// than wrapping. This reader can still report the last authoritative positive transition count.
/// </para>
/// </remarks>
internal sealed class KubernetesFencingTokenProvider : IFencingTokenProvider
{
	private readonly IKubernetes _kubernetesClient;
	private readonly KubernetesLeaderElectionOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="KubernetesFencingTokenProvider"/> class.
	/// </summary>
	/// <param name="kubernetesClient">The Kubernetes client (same cluster as the leader election).</param>
	/// <param name="options">The Kubernetes leader election options (lease naming / namespace).</param>
	public KubernetesFencingTokenProvider(IKubernetes kubernetesClient, IOptions<KubernetesLeaderElectionOptions> options)
	{
		ArgumentNullException.ThrowIfNull(kubernetesClient);
		ArgumentNullException.ThrowIfNull(options);

		_kubernetesClient = kubernetesClient;
		_options = options.Value;
	}

	/// <summary>
	/// The single home of the exhaustion rule for <c>Lease.spec.leaseTransitions</c>: the counter is a
	/// 32-bit integer, so at <see cref="int.MaxValue"/> the next leadership transition would overflow it
	/// and must fail closed rather than wrap into a non-monotonic token.
	/// </summary>
	/// <param name="transitions"> The transition count the store currently holds for the lease. </param>
	/// <returns> <see langword="true"/> when no further transition can be numbered. </returns>
	/// <remarks>
	/// The election advances the counter using the same Lease resource version as its ownership CAS.
	/// This reader never mints or advances a counter and does not implement IFencingTokenIssuer.
	/// </remarks>
	internal static bool IsTokenDomainExhausted(long transitions) => transitions >= int.MaxValue;

	/// <summary>
	/// The single home of the strictly-positive rule: a <c>Lease.spec.leaseTransitions</c> of zero, or
	/// absent, records no leadership transition and is therefore UNSET rather than a token of zero.
	/// </summary>
	/// <param name="transitions"> The transition count the store holds, or <see langword="null"/> when the Lease carries none. </param>
	/// <returns> <see langword="true"/> when the count can serve as a fencing token. </returns>
	/// <remarks>
	/// The bottom-of-domain sibling of <see cref="IsTokenDomainExhausted"/>: both answer "this counter
	/// cannot yield a strictly-positive token", one because the domain is spent and one because nothing
	/// has been recorded in it. Zero is the normal initial state of a Lease and the state of any Lease at
	/// this name created by another controller, so it is reachable rather than theoretical — and because
	/// the fence admits a token at or above the mark, a zero presented against a zero mark would be
	/// ADMITTED. Treating zero as a value is therefore fail-open at the resource, which is why neither
	/// this provider nor the election may report one.
	/// </remarks>
	internal static bool IsUsableFencingToken(long? transitions) => transitions > 0;

	/// <inheritdoc />
	public async ValueTask<long?> GetTokenAsync(string resourceId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);

		return await ReadLeaseTransitionsAsync(resourceId, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async ValueTask<bool> ValidateTokenAsync(string resourceId, long token, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);

		// Fail-closed high-water-mark check: with no mark nothing is valid -- and an absent Lease, an absent
		// leaseTransitions and a leaseTransitions of ZERO are all "no mark", because a token is strictly
		// positive. Otherwise accept only tokens at or above the current leaseTransitions, rejecting a
		// stale leader's lower token. A presented token at or below zero needs no separate guard: against a
		// positive mark the comparison already refuses it, and against no mark the HasValue term does.
		var current = await ReadLeaseTransitionsAsync(resourceId, cancellationToken).ConfigureAwait(false);
		return current.HasValue && token >= current.Value;
	}

	private async Task<long?> ReadLeaseTransitionsAsync(string resourceId, CancellationToken cancellationToken)
	{
		var leaseName = _options.LeaseName ?? $"{resourceId}-leader-election";
		var namespaceName = string.IsNullOrEmpty(_options.Namespace) ? "default" : _options.Namespace;

		try
		{
			var lease = await _kubernetesClient.CoordinationV1
				.ReadNamespacedLeaseAsync(leaseName, namespaceName, cancellationToken: cancellationToken)
				.ConfigureAwait(false);

			// A TRANSITION COUNT OF ZERO IS UNSET, NOT A MARK, and that distinction is the whole guarantee.
			// Fencing tokens are strictly positive, so a Lease that EXISTS carrying leaseTransitions 0 --
			// the normal initial state, and the state of any Lease at this name created by another
			// controller -- has recorded no transition and therefore carries no token. Reporting that as
			// the VALUE zero made the high-water comparison in ValidateTokenAsync read `token >= 0`, which
			// is TRUE FOR EVERY NON-NEGATIVE TOKEN including a fabricated one: the state that must refuse
			// everything instead admitted everything, and a delete-and-recreate of the Lease reset the
			// fence permanently. Absent and zero therefore answer identically -- null, the idiomatic
			// "no value" signal, which fails closed.
			var transitions = lease.Spec?.LeaseTransitions;
			return IsUsableFencingToken(transitions) ? transitions : null;
		}
		catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
		{
			return null;
		}
	}
}
