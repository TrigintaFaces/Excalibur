// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.InteropServices;
using System.Security;

namespace Excalibur.Security.Internal;

/// <summary>
/// Provides scoped, genuinely-scrubbed access to the plaintext of a <see cref="SecureString"/>.
/// </summary>
/// <remarks>
/// <para>
/// The plaintext is marshalled into a <strong>pinned</strong> <see cref="char"/> buffer
/// (<see cref="GCHandleType.Pinned"/>) so the garbage collector cannot relocate or silently copy
/// it, then the buffer is zeroed (<see cref="Array.Clear(Array)"/>) and unpinned in a
/// <see langword="finally"/> block once the caller's delegate returns. This replaces the prior
/// <c>Array.Fill(value.ToCharArray(), '\0')</c> anti-pattern, which zeroed a throwaway copy while
/// leaving the actual plaintext untouched.
/// </para>
/// <para>
/// <strong>Honest boundary note:</strong> a managed <see cref="string"/> is immutable, movable, and
/// cannot be zeroed, so plaintext is held here <em>only</em> in the pinned buffer — never in a
/// long-lived string owned by this layer. If a downstream SDK exposes only a <see cref="string"/>
/// based API (e.g. AWS Secrets Manager <c>SecretString</c>, Vault KV&#160;v2), the caller may
/// construct a <em>transient</em> string inside the scope to satisfy that call; that copy is the
/// SDK's surface and its lifetime is bounded by the SDK, not by this framework.
/// </para>
/// </remarks>
internal static class SecurePlaintextScope
{
    /// <summary>
    /// Marshals <paramref name="secret"/> into a pinned, zero-on-exit <see cref="char"/> buffer and
    /// invokes <paramref name="use"/> with it, returning the delegate's result.
    /// </summary>
    /// <typeparam name="TResult">The result type produced by <paramref name="use"/>.</typeparam>
    /// <param name="secret">The secret to expose as plaintext for the duration of the scope.</param>
    /// <param name="use">
    /// The asynchronous operation to run with the pinned plaintext buffer. The buffer is valid only
    /// until the returned task completes; it MUST NOT be captured or stored beyond the scope.
    /// </param>
    /// <param name="cancellationToken">A token to observe while awaiting <paramref name="use"/>.</param>
    /// <returns>The result produced by <paramref name="use"/>.</returns>
    public static async Task<TResult> UseAsync<TResult>(
        SecureString secret,
        Func<char[], CancellationToken, Task<TResult>> use,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(use);

        var length = secret.Length;
        var buffer = new char[length];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        var unmanaged = IntPtr.Zero;
        try
        {
            unmanaged = Marshal.SecureStringToGlobalAllocUnicode(secret);
            for (var i = 0; i < length; i++)
            {
                buffer[i] = (char)Marshal.ReadInt16(unmanaged, i * 2);
            }

            return await use(buffer, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (unmanaged != IntPtr.Zero)
            {
                Marshal.ZeroFreeGlobalAllocUnicode(unmanaged);
            }

            Array.Clear(buffer, 0, buffer.Length);
            handle.Free();
        }
    }
}
