// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch;

/// <summary>
/// Enforces a configurable maximum inbound-payload length at a message-ingress trust boundary,
/// before the body is materialized/deserialized. The framework analogue of Kestrel's
/// <c>MaxRequestBodySize</c>.
/// </summary>
/// <remarks>
/// A transport-agnostic, cross-cutting messaging primitive (a byte-length limit on an inbound body —
/// no transport specifics), so it lives at the shared <c>Dispatch.Abstractions</c> foundation reachable
/// by both core inbox/outbox and the transport packages. The serializer/codec stays limit-agnostic
/// (size is a boundary policy, not a codec concern). Ingress fails <strong>closed</strong>: an
/// over-limit payload throws <see cref="PayloadTooLargeException"/> and is rejected — never truncated,
/// never silently passed.
/// </remarks>
internal static class PayloadSizeGuard
{
    /// <summary>
    /// The default maximum inbound-payload length (4 MiB) applied when a consumer does not configure
    /// one. A bounded default is deliberate: an unbounded default would leave the guard inert for
    /// every consumer who never sets a limit (advertised-but-unwired). Comfortably above typical
    /// broker message sizes, well below allocation-DoS territory; raise it or opt out (<c>null</c>)
    /// for larger legitimate payloads.
    /// </summary>
    public const int DefaultMaxPayloadBytes = 4 * 1024 * 1024;

    /// <summary>
    /// Throws <see cref="PayloadTooLargeException"/> when <paramref name="length"/> exceeds
    /// <paramref name="maxBytes"/>; otherwise returns.
    /// </summary>
    /// <param name="length"> The inbound payload length, in bytes (measured before materialization). </param>
    /// <param name="maxBytes"> The configured maximum payload length, in bytes. </param>
    /// <exception cref="PayloadTooLargeException"> Thrown when <paramref name="length"/> &gt; <paramref name="maxBytes"/>. </exception>
    public static void EnsureWithinLimit(int length, int maxBytes)
    {
        if (length > maxBytes)
        {
            throw new PayloadTooLargeException(length, maxBytes);
        }
    }

    /// <summary>
    /// Convenience overload honoring the <c>int?</c> options contract: a <see langword="null"/>
    /// <paramref name="maxBytes"/> is an explicit opt-out (unbounded) and performs no check.
    /// </summary>
    /// <param name="length"> The inbound payload length, in bytes. </param>
    /// <param name="maxBytes"> The configured maximum, or <see langword="null"/> to opt out. </param>
    public static void EnsureWithinLimit(int length, int? maxBytes)
    {
        if (maxBytes is int max)
        {
            EnsureWithinLimit(length, max);
        }
    }

    /// <summary>
    /// Throws <see cref="PayloadTooLargeException"/> when the <em>decoded</em> byte length of a
    /// Base64-encoded payload exceeds <paramref name="maxBytes"/>; otherwise returns. The decoded
    /// length is computed arithmetically from the Base64 character length and its padding, so no
    /// decoded buffer is allocated for the check — an over-limit payload is rejected before it is
    /// materialized (a <see cref="Convert.FromBase64String(string)"/> would inflate the wire string
    /// by ~33%, so measuring the raw character length would enforce the wrong limit).
    /// </summary>
    /// <param name="base64"> The Base64-encoded inbound payload (the wire representation). </param>
    /// <param name="maxBytes"> The configured maximum decoded length, or <see langword="null"/> to opt out. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="base64"/> is <see langword="null"/>. </exception>
    /// <exception cref="PayloadTooLargeException"> Thrown when the decoded length &gt; <paramref name="maxBytes"/>. </exception>
    public static void EnsureBase64WithinLimit(string base64, int? maxBytes)
    {
        ArgumentNullException.ThrowIfNull(base64);
        if (maxBytes is int max)
        {
            EnsureWithinLimit(GetBase64DecodedLength(base64), max);
        }
    }

    /// <summary>
    /// Computes the decoded byte length of a padded Base64 string without allocating the decoded
    /// buffer. Derived from the character length and trailing padding (<c>=</c>) count.
    /// </summary>
    /// <param name="base64"> The padded Base64 string. </param>
    /// <returns> The number of bytes the string decodes to. </returns>
    public static int GetBase64DecodedLength(string base64)
    {
        ArgumentNullException.ThrowIfNull(base64);
        var length = base64.Length;
        if (length == 0)
        {
            return 0;
        }

        var padding = 0;
        if (base64[length - 1] == '=')
        {
            padding++;
            if (length >= 2 && base64[length - 2] == '=')
            {
                padding++;
            }
        }

        return (length / 4 * 3) - padding;
    }
}
