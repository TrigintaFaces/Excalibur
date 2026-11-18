// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;

namespace Excalibur.Dispatch.Messaging;

/// <summary>Validates the selected timer backend for a provider-owned atomic saga transaction.</summary>
/// <remarks>
/// This optional capability must be obtained from the selected timeout store, never a raw
/// fallback. Its immutable binding identifies the selected logical database and exact timer
/// table, without credentials. It does not schedule, persist, open connections or commit.
/// Before any write the transaction owner must additionally establish that its ACTUAL timer
/// writer uses the same immutable backend/table binding included in the transition digest.
/// This interface's transaction/tenant validation alone cannot inspect an independently chosen
/// writer table. Built-in participants and writers must compare the same trusted canonical
/// binding; callers must not guess or parse opaque bindings from arbitrary implementations.
/// Ordinary state-only timeout APIs do not require this capability.
/// </remarks>
public interface ISagaAtomicTimeoutParticipant
{
    /// <summary>Copies the immutable backend and table binding included in the transition digest.</summary>
    /// <returns>A fresh caller-owned byte array containing a versioned unambiguous encoding.</returns>
    byte[] GetCanonicalBindingBytes();

    /// <summary>Refuses a different open transaction domain, timer table or authoritative tenant.</summary>
    /// <param name="transaction">The actual provider-owned open transaction used for all writes.</param>
    /// <param name="authoritativeTenantId">The exact tenant captured by the selected outer saga store.</param>
    /// <remarks>
    /// Must run before any saga, timer or outbox write. Revalidation uses the actual current
    /// transaction connection and the current authoritative tenant; it must not reuse ambient
    /// tenant/options values captured from an earlier flow. Refusal throws without persistence.
    /// This is logical binding validation, not attestation of physical database alias equivalence
    /// or protection against a malicious capability implementation.
    /// </remarks>
    void ValidateTransaction(IDbTransaction transaction, string authoritativeTenantId);
}
