# Excalibur.Outbox.InMemory

In-memory implementation of the outbox pattern for Excalibur, suitable for development and testing.

## Claim ownership

Each lease acquisition returns a detached envelope with a new `DispatcherId`. Keep that identity
with the acquisition. Use `IClaimScopedOutboxStore` for ungated success/failure/backoff and
`IClaimScopedDeadLetterableOutboxStore` for ungated dead-letter completion. Fenced workers pass
`OutboxWriteAuthority` to success/failure and dead-letter authority overloads.
A late report from an older acquisition returns `ClaimLost`, including when the current lease was
already released or expired even if no successor has claimed it yet. Guards, outcomes, mutation and
lease/floor cleanup share the same claim lock. Tokens must be positive and claim identities nonblank. A newer tenure is still
reported as `FenceRefused` before row-level outcomes.

A lease is valid until, but not including, its expiration instant. At the expiration instant,
completion with its old identity is refused and a new acquisition may receive a different identity.
Retry schedules and failure floors still apply independently.

Staging and reads copy the envelope, payload bytes, top-level headers and transport delivery records.
Header values are not deep-cloned; callers must treat nested consumer header objects as immutable.
Lease timeout, retry floors and sent-message retention are unchanged.

The older completion overloads carrying only a message ID or tenure token do not establish
same-tenure acquisition ownership. They remain weaker direct administrative APIs; framework workers
use the strong capabilities. These changes do not strengthen the token-only success or dead-letter
contracts or make an in-memory high-water mark durable across application restart.

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
