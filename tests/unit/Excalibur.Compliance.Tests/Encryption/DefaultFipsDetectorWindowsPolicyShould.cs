// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.InteropServices;

using Excalibur.Compliance.Encryption;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Compliance.Tests.Encryption;

/// <summary>
/// Binds the Windows FIPS policy read to the status the detector reports.
/// </summary>
/// <remarks>
/// <para>
/// <b>What these arms exist to catch.</b> The detector previously derived Windows FIPS status from
/// <c>CryptoConfig.AllowOnlyFipsAlgorithms</c>. On .NET Core and later that property is hardcoded to
/// <see langword="false" /> and is never populated from the host policy, so the detector reported every
/// Windows host as non-compliant — including a genuinely FIPS-enabled one. The result feeds a compliance
/// control validator, so the consequence was a regulated consumer receiving a permanent, false
/// "not FIPS compliant" in output they hand to an auditor.
/// </para>
/// <para>
/// <b>Why the existing arms could not catch it.</b> The sibling suite asserts that a result is returned,
/// that it is cached, that the platform is populated, and that a null logger throws. Every one of those
/// passes against a detector that always answers <see langword="false" />. None binds the policy to the
/// answer, which is the only property that was wrong.
/// </para>
/// <para>
/// <b>Both branches assert, on every platform.</b> The policy read is Windows-only by construction, so on
/// Windows these arms drive the substituted reader and assert the mapping. On any other platform the same
/// arms assert that the reader is <em>not</em> consulted — which is equally falsifiable, and keeps the
/// suite from passing vacuously on the Linux runners that carry most of this repository's CI.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class DefaultFipsDetectorWindowsPolicyShould
{
    private static bool OnWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    [Fact]
    public void ReportEnabled_WhenTheWindowsPolicyIsOn()
    {
        var consulted = false;
        var sut = new DefaultFipsDetector(
            NullLogger<DefaultFipsDetector>.Instance,
            () => { consulted = true; return true; });

        var result = sut.GetStatus();

        if (OnWindows)
        {
            // THE arm the old implementation could never satisfy: it read a property pinned to false, so a
            // FIPS-enabled host was reported non-compliant no matter what the policy said.
            result.IsFipsEnabled.ShouldBeTrue(
                "a Windows host whose FIPS policy is enabled must be reported as FIPS enabled; reporting "
                + "otherwise tells a regulated consumer their compliant deployment is not compliant.");
            consulted.ShouldBeTrue("the detector must consult the host policy rather than a pinned constant.");
        }
        else
        {
            consulted.ShouldBeFalse(
                "the Windows policy reader must not be consulted off Windows; this platform is answered by "
                + "its own detection path.");
            result.Platform.ShouldNotBeNullOrEmpty("every platform must still report which one it detected.");
        }
    }

    [Fact]
    public void ReportDisabled_WhenTheWindowsPolicyIsOff()
    {
        var sut = new DefaultFipsDetector(
            NullLogger<DefaultFipsDetector>.Instance,
            static () => false);

        var result = sut.GetStatus();

        if (OnWindows)
        {
            // The safety half. Without it, an implementation that answered "enabled" unconditionally would
            // satisfy the liveness arm above and be far worse than the defect being fixed.
            result.IsFipsEnabled.ShouldBeFalse(
                "a Windows host whose FIPS policy is off must not be reported as FIPS enabled.");
        }
        else
        {
            result.ShouldNotBeNull();
        }
    }

    [Fact]
    public void DistinguishAnUnreadablePolicyFromOneThatIsOff()
    {
        var sut = new DefaultFipsDetector(
            NullLogger<DefaultFipsDetector>.Instance,
            static () => null);

        var result = sut.GetStatus();

        if (OnWindows)
        {
            // Unreadable is not disabled. Both are not-compliant for the purpose of gating, but only one of
            // them is a statement the check actually established, and the details must say which — an
            // absent registry key is a different operational problem from a policy deliberately left off.
            result.IsFipsEnabled.ShouldBeFalse(
                "an unreadable policy must never be reported as compliant.");
            result.ValidationDetails.ShouldContain(
                "unconfirmed",
                Case.Insensitive,
                customMessage: "the details must record that compliance was UNCONFIRMED rather than "
                + "asserting the policy is disabled, which the detector did not establish.");
        }
        else
        {
            result.ValidationDetails.ShouldNotBeNullOrEmpty(
                "every detection path must explain how it reached its answer.");
        }
    }
}
