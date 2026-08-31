# Transport conformance: arms removed, and what each needs to come back

These facts existed in `TransportConformanceTestBase` and asserted nothing. Each opened with an
unconditional `Assert.Skip`, so the ~120 lines beneath it were unreachable, and each was reported as a
skipped test in every run — a comment with a test-runner tax, and a standing invitation to read the
suite as more thorough than it was.

They are recorded here rather than left in the tree. A skipped test does not preserve the requirement;
it hides it in a list nobody reads while suggesting it is covered.

## Removed, with live coverage elsewhere — do NOT re-add to this base

| Arm | Requirement | Where the coverage actually lives |
| --- | --- | --- |
| `Should_Support_CloudEvents_Structured_Format` | T10.34 | `HarnessCapabilityNonVacuityShould` |
| `Should_Support_CloudEvents_Binary_Format` | T10.34 | `HarnessCapabilityNonVacuityShould` |
| `Should_Preserve_CloudEvents_Attributes` | T10.34 | `HarnessCapabilityNonVacuityShould` |
| `Should_RoundTrip_CloudEvents_Without_Loss` | T10.34, R15.7 | `HarnessCapabilityNonVacuityShould` |

`HarnessCapabilityNonVacuityShould` round-trips a CloudEvent against a conforming double **and** proves
the assertion goes RED against a zero-CloudEvents double. That is strictly stronger than what these four
did, which was `await Task.CompletedTask` behind a skip.

They return to the per-transport suites only when a real transport can advertise the binding — which
needs `IChannelReceiver` to surface headers, not just test wiring. Tracked: bd-jj4hx4 (umbrella
Excalibur.Dispatch-urttf7).

## Removed as the wrong kind of test

| Arm | Requirement | Why |
| --- | --- | --- |
| `Should_Handle_High_Throughput` | R9.* | Throughput is a per-transport **SHOULD**, not behavioural conformance. Its own skip message said so. Belongs in the benchmark suite. |
| `Should_Maintain_Low_Latency` | R9.* | Same: p95/p99 is an SLO, and an SLO with no threshold is not an assertion. |

Tracked: bd-lpkwjr.

## Removed and still genuinely uncovered — the one that matters

| Arm | Requirement |
| --- | --- |
| `Should_Guarantee_At_Least_Once_Delivery` | R2.1, R4.3 |

**This is the most important guarantee in the file and nothing currently verifies it for a shipping
transport.** Deleting the shell does not weaken coverage — a single send/receive never proved
at-least-once, and a transport with completely broken redelivery passed it — but it does remove the
reminder, so the requirement is written down here instead.

It returns as a real test when all three of these are true. None is test wiring alone:

1. **`IChannelReceiver` grows an ack/nack surface.** Today it is one
   `Task<T?> ReceiveAsync<T>(CancellationToken)`. With no way to decline a message, no test can force the
   redelivery that the guarantee is about. This is a production API change and the real blocker.
2. **The base gates on `TransportCapability.AckNackRedelivery`.** It currently gates only on `Filtering`,
   so the capability is declared and never consumed.
3. **At least one transport overrides `AdvancedCapabilities`.** No suite does; every transport advertises
   `null`, so a capability-gated arm no-ops everywhere.

The assertion itself already exists and is proven RED-able:
`HarnessCapabilityNonVacuityShould.Redeliver_A_Nacked_Message_Against_A_Conforming_Transport`, with its
paired arm failing against a non-redelivering double. What is missing is a real transport to point it at.

Tracked: bd-5dox7c (umbrella Excalibur.Dispatch-urttf7).
