// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Cryptography;
using System.Text;

using Excalibur.Dispatch.Observability.Sanitization;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Observability.Tests.Sanitization;

/// <summary>
/// Author≠impl regression lock (bead a28aio, osj47o sibling) for the optional-pepper upgrade of
/// <see cref="HashingTelemetrySanitizer"/>: a configured pepper on the shared
/// <see cref="TelemetrySanitizerOptions"/> switches tag fingerprinting from unkeyed SHA-256 to keyed
/// HMAC-SHA-256, making the masking sanitizer's doc-referral to "the keyed sanitizer" genuinely true.
/// </summary>
/// <remarks>
/// Non-vacuous by differential: the SAME sensitive value hashes to a DIFFERENT fingerprint with vs without
/// a pepper — RED if the pepper were ignored, GREEN once it keys the HMAC. Fail-open: neither path throws.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class HashingTelemetrySanitizerPepperShould
{
    private static readonly byte[] Pepper =
        Encoding.UTF8.GetBytes("super-secret-pepper-value-32bytes!!");

    private const string TagName = "user.id";
    private const string LowEntropyValue = "42";

    private static HashingTelemetrySanitizer Create(byte[]? pepper) =>
        new(MsOptions.Create(new TelemetrySanitizerOptions
        {
            SensitiveTagNames = [TagName],
            Pepper = pepper,
        }));

    private static string Fingerprint(byte[] digest) =>
        string.Concat("sha256:", Convert.ToHexStringLower(digest));

    [Fact]
    public void DeriveHmacFingerprint_WhenPepperConfigured_DifferentFromUnkeyed()
    {
        var utf8 = Encoding.UTF8.GetBytes(LowEntropyValue);
        var expectedKeyed = Fingerprint(HMACSHA256.HashData(Pepper, utf8));
        var expectedUnkeyed = Fingerprint(SHA256.HashData(utf8));

        var keyed = Create(Pepper).SanitizeTag(TagName, LowEntropyValue);
        var unkeyed = Create(null).SanitizeTag(TagName, LowEntropyValue);

        keyed.ShouldBe(expectedKeyed);
        unkeyed.ShouldBe(expectedUnkeyed);
        keyed.ShouldNotBe(unkeyed); // pepper genuinely wired
    }

    [Fact]
    public void ReturnUnkeyedDigest_AndNotThrow_WhenNoPepper()
    {
        var expected = Fingerprint(SHA256.HashData(Encoding.UTF8.GetBytes(LowEntropyValue)));

        string? result = null;
        Should.NotThrow(() => result = Create(null).SanitizeTag(TagName, LowEntropyValue));

        result.ShouldBe(expected);
    }

    [Fact]
    public void NotThrow_WhenPepperConfigured()
    {
        Should.NotThrow(() => Create(Pepper).SanitizeTag(TagName, LowEntropyValue));
    }
}
