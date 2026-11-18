# Excalibur.LeaderElection.Redis

Redis implementation of distributed leader election using SET NX with Lua scripts.

## Installation

```bash
dotnet add package Excalibur.LeaderElection.Redis
```

## Features

- Uses Redis `SET NX` with TTL for distributed locking
- Lua scripts for atomic lock operations
- StackExchange.Redis integration
- Automatic lock renewal via background timer
- Measure acquisition and renewal latency against the configured Redis deployment.
- NativeAOT compatibility depends on configured providers, payload metadata and dependencies; validate by publishing and executing the application’s native binary.

## Usage

```csharp
// Register Redis leader election via the Excalibur builder
services.AddExcalibur(excalibur =>
    excalibur.AddLeaderElection(le =>
        le.UseRedis(redis => redis
            .ConnectionString("localhost:6379")
            .LockKey("myapp:leader")
            .Database(0))));
```

## How It Works

Redis provides fast, distributed locking:
- `SET key value NX PX timeout` - Acquire lock atomically
- Lua script for safe lock release (only if still owner)
- Background renewal prevents premature expiry

## Related Packages

- `Excalibur.LeaderElection` - Core abstractions and InMemory implementation

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.


### Tenure lifetime and callbacks

A successful acquisition owns a private random Redis value for that tenure; the public candidate ID is display identity. Renewals and releases compare the complete private value, so a delayed old request cannot alter a later tenure with the same candidate ID. Leader discovery decodes known display payloads and treats unknown values as display-only.

Local leadership expires after RenewInterval + GracePeriod measured from the conservative start of the last successful ownership request. A hung request cannot postpone local demotion. Indeterminate failures retry at RetryInterval without extending that deadline; affirmative ownership loss or a permanent failure demotes immediately. Redis TTL and downstream fencing remain separate safeguards.

Normal renewal and retry schedules start from the preceding request's start, so response latency does not add another whole interval before the next request. Requests remain serialized. A response arriving after the active deadline cannot revive that tenure. Positive intervals below one millisecond use a one-millisecond scheduling floor; larger delays are sliced to the timer's supported range. The host's timer granularity and scheduling latency can still make a very small configured budget expire before a retry.

When a fencing provider is configured, the acquiring `LeaderChanged` event carries that transition's exact token. Capture it from the event rather than re-reading `CurrentLeadership` after another tenure may have begun. Relinquish and remote-observation events do not carry an acquired token.

Stop waits for owned Redis operations, remote cleanup and timer disposal. A canceled Stop wait reports cancellation while unfinished work remains owned, and restart is refused until shutdown succeeds. Consumer notifications are immutable transition history delivered outside state locks; Stop does not join consumer callbacks. This allows a callback to stop or dispose its candidate. Calling Start from an active callback is refused because it would wait for its own delivery. An ordinary external restart waits prior notifications to drain before allocating another run. A blocked callback can therefore delay restart but cannot delay local demotion or owned cleanup. Remote holder and acquisition-failure observations use bounded coalesced delivery.

## Fencing authority consistency

Fencing authority reads demand the primary; they do not fall back to a lagging replica. A primary-read failure propagates rather than returning an absent/stale mark. Keep fencing counter keys free of expiry/eviction and retain their history through the chosen persistence policy. Replica promotion or old restore can still regress lost history; restart/restore durability is UNVERIFIED here.

Validation I/O/consistency faults and cancellation propagate without invoking downstream middleware. Discard a pinned token on leadership loss; in-flight writes still need atomic resource-side fencing. These bounds are not real-provider Native or full-release certification.
