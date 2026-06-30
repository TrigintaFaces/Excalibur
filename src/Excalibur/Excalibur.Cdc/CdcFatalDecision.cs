// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc;

/// <summary>
/// The decision a CDC processor's consume loop must act on after each iteration: whether it may
/// advance its durable checkpoint and whether it must stop.
/// </summary>
/// <remarks>
/// <para>
/// Produced exclusively by <see cref="CdcFatalGuard.Decide"/>. The safety invariant — <em>a fatal (or
/// transient) fault never advances the durable checkpoint past the unprocessed change</em> (FR-B2 /
///, /) — is enforced by the idiom native to each provider class:
/// </para>
/// <list type="bullet">
/// <item><description><b>Poll-batch providers (Cosmos, DynamoDb)</b> gate the durable advance literally
/// on <see cref="AdvanceCheckpoint"/> (<c>if (decision.AdvanceCheckpoint) { …Confirm… }</c>), reached on
/// both the success and the captured-fault path, so the <c>if(true)</c> mutant advances past a failed
/// change and turns the field-gate lock RED — structurally non-vacuous.</description></item>
/// <item><description><b>Streaming providers (Postgres, Mongo)</b> enforce the same invariant by
/// confirm-site <i>placement</i> at the transaction-commit / invalidation boundary: a mid-batch fault
/// unwinds out of the consume method before the confirm site is reachable. A field-gate there would be
/// vacuous (the confirm point never sees a <see cref="CdcFatalGuard.Decide"/>-evaluated fault), so the
/// placement is regression-locked by the non-skipped real-infra <i>restart-redelivery</i> test instead.</description></item>
/// </list>
/// <para>
/// A "reconnect-on-transient" signal is intentionally NOT a field here: it is the implicit else of a
/// non-fatal fault (<see cref="Stop"/> is <see langword="false"/>); a future reconnect-policy knob would
/// be a tracked feature bead, not a silent unread field.
/// </para>
/// </remarks>
/// <param name="AdvanceCheckpoint">
/// <see langword="true"/> only on the clean-success path; <see langword="false"/> on ANY fault
/// (fatal or transient) so the durable checkpoint is never advanced past an unprocessed change.
/// </param>
/// <param name="Stop">
/// <see langword="true"/> when the fault is fatal (non-retryable) — the processor stops loudly
/// rather than silently reconnecting forever. When <see langword="false"/> on a fault, the loop
/// reconnects and retries from the (un-advanced) checkpoint.
/// </param>
public readonly record struct CdcFatalDecision(bool AdvanceCheckpoint, bool Stop);
