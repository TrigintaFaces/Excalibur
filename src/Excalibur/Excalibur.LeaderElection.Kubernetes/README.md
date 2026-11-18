# Excalibur.LeaderElection.Kubernetes

Kubernetes implementation of distributed leader election using the Lease API.

## Installation

```bash
dotnet add package Excalibur.LeaderElection.Kubernetes
```

## Features

- Native Kubernetes Lease API for cloud-native leader election
- Automatic in-cluster configuration detection
- Local development support via kubeconfig
- Background hosted service for automatic leadership management
- Graceful leadership transitions
- Pod identity auto-detection
- Not Native AOT compatible - the Kubernetes client this provider builds on resolves types by runtime reflection

## Usage

Choose one registration method below and call it from your application's service setup.

```csharp
using Microsoft.Extensions.DependencyInjection;

public static class KubernetesLeaderElectionRegistration
{
    public static IServiceCollection AddElection(IServiceCollection services)
    {
        services.AddExcaliburKubernetesLeaderElection(builder => builder
            .Namespace("my-namespace")
            .LeaseName("my-app-leader")
            .LeaseDuration(15)
            .RenewDeadline(5000)
            .RetryPeriod(2000));
        return services;
    }

    // Alternative: let the hosted service manage the election lifetime.
    public static IServiceCollection AddHostedElection(IServiceCollection services)
    {
        services.AddExcaliburKubernetesLeaderElectionHostedService("order-processor", builder => builder
            .Namespace("my-namespace")
            .LeaseName("my-app-leader")
            .LeaseDuration(15)
            .RenewDeadline(5000)
            .RetryPeriod(2000));
        return services;
    }
}
```

## Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `Namespace` | auto-detect | Kubernetes namespace |
| `LeaseName` | `{resource}-leader-election` | Lease resource name |
| `InstanceId` | shared identity resolver | Candidate display identity; use a unique identity per instance |
| `LeaseDuration` | 15 seconds | Native lease duration; rounded upward to whole seconds |
| `RenewInterval` | 5 seconds | Successful ownership request-start cadence |
| `RetryInterval` | 2 seconds | Failed attempt request-start retry cadence |
| `GracePeriod` | 5 seconds | Additional local proof budget; also used by existing takeover comparison |
| `StepDownWhenUnhealthy` | true | Auto-release leadership when unhealthy |

## Local authority and native ownership

One owned actor performs read/create/replace operations. A confirmed successful native ownership write
starts a conservative monotonic local proof at the beginning of the entire request, including configured
retries. `IsLeader`, `CurrentLeadership` and local `CurrentLeaderId` derive from one immutable publication.
A separate timer retires it at `RenewInterval + GracePeriod` even while an SDK request is hung. A matching
holder read is discovery, not a renewal proof; late responses cannot revive a retired generation.

Normal cadence and failed-attempt retries are measured from actual request start, so reply latency is
not added a second time. There are no overlapping ownership writes or replay of missed ticks. Native
lease seconds use checked integer ceiling; unrepresentable native durations are rejected. Configuration
headroom is necessary but does not prove network, scheduler or cross-clock bounds.

`HolderIdentity` remains the display identity. A private per-tenure owner annotation, native Lease UID
and `resourceVersion` CAS distinguish independent tenures, including equal display identities. A second
same-display candidate cannot steal a live private tenure. Cleanup joins admitted native work, reads
fresh state and clears only its exact old UID/nonce using the current native version. Unknown or failed
cleanup remains an observable fault; it does not license a fresh run. The native transition counter
advances on takeover and fails closed on exhaustion or unusable values.

Health metadata uses independently owned read/CAS operations, so it does not wait ahead of every renewal
on one global semaphore. Health writes never refresh ownership proof. An unhealthy update reserves its
exact annotation intent before local retirement; that admitted intent can finish while retiring, and
cleanup joins it. Metadata is copied before asynchronous work. Health discovery is informational.

Kubernetes-local caller policy: `StartAsync` and `StopAsync` use one total monotonic wait budget
of `RenewInterval + GracePeriod`, including admission waits. Exceeding it throws `TimeoutException`;
it does not prove that an SDK operation ended or a native release succeeded. The logical ownership
attempt uses that total budget across reads, writes and retries; an existing tenure still expires at
its earlier proof deadline. This is deliberate caller-wait policy, not a network/safety theorem.
A timed-out start cannot publish a late tenure. A timed-out stop retains the same actual shutdown;
restart is refused until it joins successfully. Async and synchronous disposal still join actual SDK
operations and cancellation callbacks and can remain pending for an uncooperative client.
A phase admitted before the stop seal may physically begin or commit afterward; it remains owned.
No new ownership phase admission is allowed after the seal; late replies cannot revive retired authority.
Exact-owner cleanup is separately admitted retirement work after the actual operation settles.
Independent health operations and post-joined-Stop discovery retain their existing cancellation
semantics; no fixed global count of caller-admitted health work is promised.

The Kubernetes client is borrowed: the provider neither disposes it nor changes shared HTTP timeout
settings. The pinned SDK's ordinary default request timeout is 100 seconds and cooperative, which is
not this provider's local proof bound. `StopAsync` caller cancellation stops waiting without abandoning
owned shutdown. Repeated `DisposeAsync` shares the actual pending/faulted task; synchronous `Dispose`
joins that same resource cleanup and observes its failure. Await async disposal before disposing a manually supplied client.
Resource shutdown does not join consumer notification callbacks; `NotificationsCompleted` separately
joins the terminal synchronous callback delivery after permanent disposal. A blocked callback or logger
can delay notification completion without keeping local authority true.

This repair does not establish arbitrary wall-clock-skew tolerance. Existing challenger takeover still
compares a holder-authored `RenewTime` against another candidate's wall clock; the API server does not
stamp that field. Nor does a Lease transition counter constitute a globally monotonic external fence
domain across deletion/recreation of its UID. Protect business effects with the appropriate authority
and retain the Lease/fence domain. Package NativeAOT support is unchanged: `IsAotCompatible` remains
false for the SDK-backed package; local typed code and generated health JSON are not an end-to-end
NativeAOT compatibility claim.

## RBAC Requirements

Your pod's service account needs Lease permissions:

```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: leader-election-role
rules:
- apiGroups: ["coordination.k8s.io"]
  resources: ["leases"]
  verbs: ["get", "create", "update"]
```

## Related Packages

- `Excalibur.LeaderElection` - Core abstractions and InMemory implementation

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.

Lifecycle reentry from the election's own active SDK/cancellation context is rejected before shutdown
admission to prevent self-join. Active owner ancestors are checked; independent elections remain usable.
The guard expires only after the actual SDK call, cancellation callbacks and owned cancellation-source
cleanup join. This is distinct from normal ownership notification callbacks, which may stop/dispose.

This ancestry guard does not implement a global wait graph for independent cross-context cyclic
application callbacks. Per-request SDK scopes and cancellation sources are released after each actual
transaction; healthy long-lived tenures do not accumulate one retained scope per renewal.

Health APIs normally wait for a pending Stop before admitting a fresh intent. A call inherited from
this same election's active SDK/cancellation context is rejected before that wait, since Stop is joining
that very context. Ordinary notifications and independent election owners keep their normal behavior.
