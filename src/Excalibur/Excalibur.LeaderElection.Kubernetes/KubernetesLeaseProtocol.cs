// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Net;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Polly;

namespace Excalibur.LeaderElection.Kubernetes;

// No mutable native response is shared with the actor/health lane. Every mutation is resourceVersion CAS.
internal sealed class KubernetesLeaseProtocol(IKubernetes client, KubernetesLeaderElectionOptions options,
    string name, string ns, string candidate, string resource, TimeProvider clock, ResiliencePipeline retry)
{
    internal const string OwnerAnnotation = "leader-election.excalibur.io/owner";
    private int NativeSeconds => ToNativeSeconds(options.LeaseDuration);
    internal static int ToNativeSeconds(TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);
        var seconds = duration.Ticks / TimeSpan.TicksPerSecond;
        if (duration.Ticks % TimeSpan.TicksPerSecond != 0) { seconds++; }
        if (seconds > int.MaxValue) { throw new ArgumentOutOfRangeException(nameof(duration), "Lease duration exceeds native Int32 seconds."); }
        return (int)seconds;
    }
    internal sealed record Owner(string Uid, string Nonce, int Token, string ResourceVersion);
    internal sealed class Acquisition(string nonce)
    {
        internal string Nonce { get; } = nonce;
        internal Owner? Prepared { get; set; }
    }
    internal enum FenceFailure { None, Exhausted, Unset }
    internal sealed record Result(bool Confirmed, bool Lost, Owner? Proof, string? OtherHolder, FenceFailure Failure = FenceFailure.None);

    internal async Task<Result> AcquireAsync(Acquisition attempt, CancellationToken token)
    {
        var lease = Clone(await ReadOrCreateAsync(token).ConfigureAwait(false));
        if (!UsableIdentity(lease)) { throw new InvalidOperationException("Lease has no usable UID/resourceVersion."); }
        var own = Matches(lease, new Owner(lease.Metadata.Uid, attempt.Nonce, lease.Spec.LeaseTransitions ?? 0, lease.Metadata.ResourceVersion));
        if (!string.IsNullOrEmpty(lease.Spec.HolderIdentity) && !own && !Expired(lease))
        { return new(false, false, null, lease.Spec.HolderIdentity); }
        var transitions = lease.Spec.LeaseTransitions ?? 0;
        if (!own)
        {
            if (KubernetesFencingTokenProvider.IsTokenDomainExhausted(transitions))
            { return new(false, false, null, lease.Spec.HolderIdentity, FenceFailure.Exhausted); }
            transitions = checked(transitions + 1);
        }
        if (!KubernetesFencingTokenProvider.IsUsableFencingToken(transitions)) { return new(false, false, null, lease.Spec.HolderIdentity, FenceFailure.Unset); }
        var now = clock.GetUtcNow().UtcDateTime;
        lease.Spec.HolderIdentity = candidate;
        lease.Spec.LeaseTransitions = transitions;
        lease.Spec.LeaseDurationSeconds = NativeSeconds;
        lease.Spec.RenewTime = now;
        if (!own || lease.Spec.AcquireTime is null) { lease.Spec.AcquireTime = now; }
        lease.Metadata.Annotations[OwnerAnnotation] = attempt.Nonce;
        foreach (var item in options.CandidateMetadata)
        { lease.Metadata.Annotations["leader-election.excalibur.io/metadata-" + item.Key] = item.Value; }
        attempt.Prepared = new Owner(lease.Metadata.Uid, attempt.Nonce, transitions, lease.Metadata.ResourceVersion);
        var written = await ReplaceAsync(lease, token).ConfigureAwait(false);
        return ValidateWrite(written, attempt.Prepared);
    }

    internal async Task<Result> RenewAsync(Owner owner, CancellationToken token)
    {
        V1Lease lease;
        try { lease = Clone(await ReadAsync(token).ConfigureAwait(false)); }
        catch (HttpOperationException error) when (error.Response?.StatusCode == HttpStatusCode.NotFound)
        { return new(false, true, null, null); }
        if (!Matches(lease, owner) || lease.Spec.LeaseTransitions != owner.Token)
        { return new(false, true, null, lease.Spec?.HolderIdentity); }
        if (!UsableIdentity(lease)) { throw new InvalidOperationException("Lease proof lacks resourceVersion."); }
        lease.Spec.RenewTime = clock.GetUtcNow().UtcDateTime;
        lease.Spec.LeaseDurationSeconds = NativeSeconds;
        try { return ValidateWrite(await ReplaceAsync(lease, token).ConfigureAwait(false), owner); }
        catch (HttpOperationException error) when (error.Response?.StatusCode == HttpStatusCode.NotFound)
        { return new(false, true, null, null); }
        // Conflict is ambiguous: an independent same-owner annotation CAS can win this version.
    }

    internal async Task<bool> WriteHealthAsync(Owner owner, string annotation, CancellationToken token)
    {
        for (var attempt = 0; ; attempt++)
        {
            V1Lease lease;
            try { lease = Clone(await ReadAsync(token).ConfigureAwait(false)); }
            catch (HttpOperationException error) when (error.Response?.StatusCode == HttpStatusCode.NotFound) { return false; }
            if (!Matches(lease, owner) || lease.Spec.LeaseTransitions != owner.Token) { return false; }
            if (!UsableIdentity(lease)) { throw new InvalidOperationException("Health CAS requires a complete native identity."); }
            lease.Metadata.Annotations["leader-election.excalibur.io/health-" + candidate] = annotation;
            try { var written = await ReplaceAsync(lease, token).ConfigureAwait(false); return Matches(written, owner) && written.Spec.LeaseTransitions == owner.Token; }
            catch (HttpOperationException error) when (error.Response?.StatusCode == HttpStatusCode.Conflict && attempt < options.MaxRetries) { }
        }
    }

    internal async Task ReleaseAsync(Owner owner, CancellationToken token)
    {
        for (var attempt = 0; ; attempt++)
        {
            V1Lease lease;
            try { lease = Clone(await ReadAsync(token).ConfigureAwait(false)); }
            catch (HttpOperationException error) when (error.Response?.StatusCode == HttpStatusCode.NotFound) { return; }
            if (!UsableIdentity(lease)) { throw new InvalidOperationException("Release read did not establish a usable native identity."); }
            if (!Matches(lease, owner)) { return; } // A same-display successor is not this private tenure.
            if (!UsableIdentity(lease)) { throw new InvalidOperationException("Cannot release without native CAS identity."); }
            lease.Spec.HolderIdentity = null; lease.Spec.AcquireTime = null; lease.Spec.RenewTime = null;
            lease.Metadata.Annotations.Remove(OwnerAnnotation);
            try
            {
                var response = await ReplaceAsync(lease, token).ConfigureAwait(false);
                if (!UsableIdentity(response) || response.Metadata.Uid != owner.Uid || response.Spec.HolderIdentity is not null)
                { throw new InvalidOperationException("Release response did not confirm the exact old lease was cleared."); }
                return;
            }
            catch (HttpOperationException error) when (error.Response?.StatusCode == HttpStatusCode.NotFound) { return; }
            catch (HttpOperationException error) when (error.Response?.StatusCode == HttpStatusCode.Conflict && attempt < options.MaxRetries) { }
        }
    }

    internal Task<V1Lease> ReadAsync(CancellationToken token) => ExecuteAsync(ct =>
        client.CoordinationV1.ReadNamespacedLeaseAsync(name, ns, cancellationToken: ct), token);

    private Task<V1Lease> ReplaceAsync(V1Lease lease, CancellationToken token) => ExecuteAsync(ct =>
        client.CoordinationV1.ReplaceNamespacedLeaseAsync(lease, name, ns, cancellationToken: ct), token);

    private async Task<V1Lease> ExecuteAsync(Func<CancellationToken, Task<V1Lease>> operation, CancellationToken token)
    {
        try
        {
            return await retry.ExecuteAsync(async ct =>
            {
                ct.ThrowIfCancellationRequested();
                KubernetesNativeOperation.AdmitPhase();
                return await operation(ct).ConfigureAwait(false);
            }, token).ConfigureAwait(false);
        }
        catch (TaskCanceledException error) when (token.IsCancellationRequested)
        {
            // The pinned HTTP SDK uses a linked request-timeout token. Retain the actual cause,
            // normalize its completed Task cancellation only when OUR supplied token was requested.
            // Unrelated OperationCanceledException remains an observable ambiguous/fault outcome.
            throw new OperationCanceledException("Owned Kubernetes request was cancelled.", error, token);
        }
    }

    private async Task<V1Lease> ReadOrCreateAsync(CancellationToken token)
    {
        try { return await ReadAsync(token).ConfigureAwait(false); }
        catch (HttpOperationException error) when (error.Response?.StatusCode == HttpStatusCode.NotFound)
        {
            var empty = new V1Lease
            {
                Metadata = new V1ObjectMeta { Name = name, NamespaceProperty = ns,
                    Labels = new Dictionary<string, string>(StringComparer.Ordinal)
                    { ["app.kubernetes.io/managed-by"] = "excalibur-leader-election", ["excalibur.io/resource"] = resource } },
                Spec = new V1LeaseSpec { LeaseDurationSeconds = NativeSeconds, LeaseTransitions = 0 }
            };
            try { return await ExecuteAsync(ct => client.CoordinationV1.CreateNamespacedLeaseAsync(empty, ns, cancellationToken: ct), token).ConfigureAwait(false); }
            catch (HttpOperationException conflict) when (conflict.Response?.StatusCode == HttpStatusCode.Conflict)
            { return await ReadAsync(token).ConfigureAwait(false); }
        }
    }

    private Result ValidateWrite(V1Lease written, Owner expected)
    {
        if (!UsableIdentity(written)) { throw new InvalidOperationException("Successful write response lacks native identity."); }
        if (!KubernetesFencingTokenProvider.IsUsableFencingToken(written.Spec.LeaseTransitions)) { return new(false, true, null, written.Spec.HolderIdentity, FenceFailure.Unset); }
        if (!Matches(written, expected) || written.Spec.LeaseTransitions != expected.Token)
        { return new(false, true, null, written.Spec?.HolderIdentity); }
        return new(true, false, expected with { ResourceVersion = written.Metadata.ResourceVersion }, null);
    }

    private bool Matches(V1Lease lease, Owner owner) => lease.Metadata?.Name == name && lease.Metadata.NamespaceProperty == ns && lease.Metadata.Uid == owner.Uid &&
        lease.Spec?.HolderIdentity == candidate && lease.Metadata.Annotations is not null &&
        lease.Metadata.Annotations.TryGetValue(OwnerAnnotation, out var nonce) && nonce == owner.Nonce;
    private bool UsableIdentity(V1Lease lease) => lease.Metadata?.Name == name && lease.Metadata.NamespaceProperty == ns && !string.IsNullOrEmpty(lease.Metadata.Uid) &&
        !string.IsNullOrEmpty(lease.Metadata.ResourceVersion) && lease.Spec is not null;

    private bool Expired(V1Lease lease)
    {
        if (lease.Spec.RenewTime is not { } renewed) { return false; }
        var duration = lease.Spec.LeaseDurationSeconds ?? NativeSeconds;
        if (duration <= 0) { return false; }
        // Existing takeover policy uses HOLDER-authored wall time. This is not a cross-clock safety proof.
        var age = clock.GetUtcNow() - new DateTimeOffset(DateTime.SpecifyKind(renewed, DateTimeKind.Utc));
        return age > TimeSpan.FromSeconds(duration) + options.GracePeriod;
    }

    internal static V1Lease Clone(V1Lease value)
    {
        var metadata = value.Metadata ?? new V1ObjectMeta();
        var spec = value.Spec ?? new V1LeaseSpec();
        return new V1Lease()
    {
        ApiVersion = value.ApiVersion, Kind = value.Kind,
        Metadata = new V1ObjectMeta
        {
            Name = metadata.Name, NamespaceProperty = metadata.NamespaceProperty,
            Uid = metadata.Uid, ResourceVersion = metadata.ResourceVersion,
            GenerateName = metadata.GenerateName, Generation = metadata.Generation, SelfLink = metadata.SelfLink,
            CreationTimestamp = metadata.CreationTimestamp, DeletionTimestamp = metadata.DeletionTimestamp,
            DeletionGracePeriodSeconds = metadata.DeletionGracePeriodSeconds,
            Annotations = metadata.Annotations is null ? new(StringComparer.Ordinal) : new Dictionary<string, string>(metadata.Annotations, StringComparer.Ordinal),
            Labels = metadata.Labels is null ? null : new Dictionary<string, string>(metadata.Labels, StringComparer.Ordinal),
            Finalizers = metadata.Finalizers?.ToArray(), OwnerReferences = metadata.OwnerReferences?.ToArray(),
            ManagedFields = metadata.ManagedFields?.ToArray()
        },
        Spec = new V1LeaseSpec
        {
            HolderIdentity = spec.HolderIdentity, LeaseTransitions = spec.LeaseTransitions,
            LeaseDurationSeconds = spec.LeaseDurationSeconds, AcquireTime = spec.AcquireTime,
            RenewTime = spec.RenewTime, PreferredHolder = spec.PreferredHolder, Strategy = spec.Strategy
        }
    };
    }
}
