// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Cryptography;
using System.Text;

using Excalibur.Security;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Masking;

/// <summary>
/// Author≠impl regression lock (bead osj47o) for the optional-pepper upgrade of
/// <see cref="MaskingTelemetrySanitizer"/>: a configured pepper switches tag fingerprinting from unkeyed
/// SHA-256 to keyed HMAC-SHA-256 (protecting low-entropy identifiers), while remaining fail-open.
/// </summary>
/// <remarks>
/// Non-vacuous by differential: the SAME low-entropy input produces a DIFFERENT fingerprint with vs
/// without a pepper — RED if the pepper were ignored (outputs equal), GREEN once it is read and used to
/// key the HMAC. Fail-open is proven directly: neither path throws, and the no-pepper path still emits the
/// documented unkeyed digest.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class MaskingTelemetrySanitizerPepperShould
{
    private static readonly byte[] Pepper =
        Encoding.UTF8.GetBytes("super-secret-pepper-value-32bytes!!");

    private const string TagName = "SourceIp";
    private const string LowEntropyValue = "10.0.0.1";

    private static MaskingTelemetrySanitizer WithPepper(byte[]? pepper) =>
        new(MsOptions.Create(new MaskingTelemetrySanitizerOptions { Pepper = pepper }));

    private static string Fingerprint(byte[] digest) =>
        string.Concat("sha256:", Convert.ToHexStringLower(digest.AsSpan(0, 12)));

    [Fact]
    public void DeriveHmacFingerprint_WhenPepperConfigured_DifferentFromUnkeyed()
    {
        var utf8 = Encoding.UTF8.GetBytes(LowEntropyValue);
        var expectedKeyed = Fingerprint(HMACSHA256.HashData(Pepper, utf8));
        var expectedUnkeyed = Fingerprint(SHA256.HashData(utf8));

        var keyed = WithPepper(Pepper).SanitizeTag(TagName, LowEntropyValue);
        var unkeyed = WithPepper(null).SanitizeTag(TagName, LowEntropyValue);

        keyed.ShouldBe(expectedKeyed);
        unkeyed.ShouldBe(expectedUnkeyed);
        keyed.ShouldNotBe(unkeyed); // the pepper is genuinely wired — same input, different fingerprint
    }

    [Fact]
    public void ReturnUnkeyedDigest_AndNotThrow_WhenNoPepper()
    {
        var expected = Fingerprint(SHA256.HashData(Encoding.UTF8.GetBytes(LowEntropyValue)));

        string? result = null;
        Should.NotThrow(() => result = WithPepper(null).SanitizeTag(TagName, LowEntropyValue));

        result.ShouldBe(expected);
    }

    [Fact]
    public void NotThrow_WhenPepperConfigured()
    {
        // Fail-open contract is unchanged by keying — fingerprinting never breaks the audit path.
        Should.NotThrow(() => WithPepper(Pepper).SanitizeTag(TagName, LowEntropyValue));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void PassThroughNullOrEmpty_Unchanged(string? value)
    {
        WithPepper(Pepper).SanitizeTag(TagName, value).ShouldBe(value);
    }
}
