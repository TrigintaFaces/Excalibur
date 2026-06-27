// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc;

/// <summary>
/// The decision a CDC processor's consume loop must act on after each iteration: whether it may
/// advance its durable checkpoint, whether it must stop, and whether it should reconnect.
/// </summary>
/// <remarks>
/// Produced exclusively by <see cref="CdcFatalGuard.Decide"/>. The loop MUST gate every
/// checkpoint-advance on <see cref="AdvanceCheckpoint"/>, so advancing past a fatal (or transient)
/// fault is structurally inexpressible — the safety invariant of FR-B2 / ADR-338 (bd-pxhqri).
/// </remarks>
/// <param name="AdvanceCheckpoint">
/// <see langword="true"/> only on the clean-success path; <see langword="false"/> on ANY fault
/// (fatal or transient) so the durable checkpoint is never advanced past an unprocessed change.
/// </param>
/// <param name="Stop">
/// <see langword="true"/> when the fault is fatal (non-retryable) — the processor stops loudly
/// rather than silently reconnecting forever.
/// </param>
/// <param name="Reconnect">
/// <see langword="true"/> when the fault is transient — the processor reconnects and retries from
/// the (un-advanced) checkpoint.
/// </param>
public readonly record struct CdcFatalDecision(bool AdvanceCheckpoint, bool Stop, bool Reconnect);
