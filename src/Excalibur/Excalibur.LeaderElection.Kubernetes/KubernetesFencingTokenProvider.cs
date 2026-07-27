// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
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
/// lower token.
/// </para>
/// <para>
/// <b>Exhaustion:</b> <c>leaseTransitions</c> is a 32-bit integer. When it has reached
/// <see cref="int.MaxValue"/> the next transition would overflow, so the provider fails closed with
/// <see cref="FencingTokenExhaustedException"/> (the election also relinquishes rather than wrapping).
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

	/// <inheritdoc />
	public async ValueTask<long> IssueTokenAsync(string resourceId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);

		var transitions = await ReadLeaseTransitionsAsync(resourceId, cancellationToken).ConfigureAwait(false)
			?? throw new InvalidOperationException(
				string.Format(
					CultureInfo.InvariantCulture,
					"Cannot read the Kubernetes fencing token for resource '{0}': the Lease does not exist yet.",
					resourceId));

		if (transitions >= int.MaxValue)
		{
			throw new FencingTokenExhaustedException(
				string.Format(
					CultureInfo.InvariantCulture,
					"Kubernetes leaseTransitions (int32 fencing token) is exhausted for resource '{0}'.",
					resourceId))
			{
				ResourceId = resourceId,
			};
		}

		return transitions;
	}

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

		// Fail-closed high-water-mark check: with no lease/token nothing is valid; otherwise accept only
		// tokens at or above the current leaseTransitions, rejecting a stale leader's lower token.
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

			// No transitions recorded yet -> no active fencing token (idiomatic "no value" signal).
			return lease.Spec?.LeaseTransitions;
		}
		catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
		{
			return null;
		}
	}
}
