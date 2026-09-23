// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance;

namespace Excalibur.Compliance.Tests.Encryption;

/// <summary>
/// A property marked sensitive is selected for encryption at rest, not merely redacted in logs.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE DEFECT.</b> The attribute named trade secrets, API keys and credentials as its subjects, and the
/// only paths that read it were masking paths. Masking redacts a value in rendered output; it does nothing
/// to the bytes at rest. A consumer who annotated credentials on the strength of that description stored
/// them in plaintext, and could not discover it by testing the annotation — the value looked redacted
/// everywhere they would have looked.
/// </para>
/// <para>
/// <b>SELECTION IS A UNION, AND THAT IS WHAT MAKES THE PRECEDENCE RULE TOTAL.</b> The sensitive annotation
/// states no key purpose, so it can never disagree with the annotation beside it. The purpose comes from
/// whichever attribute states one, and otherwise from configuration. The one combination that genuinely
/// contradicts — an erasable per-subject value that is also pinned to a key chosen to survive erasure — is
/// refused rather than resolved, because silently honouring either one makes the other's guarantee false.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class SensitiveSelectsForEncryptionShould
{
    /// <summary>
    /// SAFETY, and the arm the defect is about. RED before the selection was widened.
    /// </summary>
    [Fact]
    public void SelectAPropertyMarkedOnlySensitive()
    {
        var selected = EncryptedFieldBinding.Select<CredentialsOnlySensitive>();

        selected.Length.ShouldBe(1,
            "a property marked sensitive names data that must not be stored in the clear. Selecting it "
            + "only for masking redacts it in logs while persisting the credential verbatim");
        selected[0].Name.ShouldBe(nameof(CredentialsOnlySensitive.ApiKey));
    }

    /// <summary>
    /// SAFETY. A sensitive property that cannot be encrypted must be REPORTED, never skipped — a silent
    /// skip would recreate the original defect at a new site.
    /// </summary>
    [Fact]
    public void ReportASensitivePropertyOfAnUnsupportedTypeRatherThanSkippingIt()
    {
        _ = EncryptedFieldBinding.Inspect(typeof(SensitiveOnAnUnsupportedType), out var unhonourable);

        unhonourable.Count.ShouldBe(1, "an unencryptable annotation must surface, not vanish");
        unhonourable[0].ShouldContain("[Sensitive]", Case.Sensitive,
            "the diagnostic must name the annotation the author actually wrote");
        unhonourable[0].ShouldContain(nameof(SensitiveOnAnUnsupportedType.Payload));
    }

    /// <summary>
    /// SAFETY. The one genuine contradiction is refused, naming both annotations.
    /// </summary>
    [Fact]
    public void RefuseAPropertyThatIsBothErasableAndPinnedToASurvivingKey()
    {
        _ = EncryptedFieldBinding.Inspect(typeof(ErasableAndPinned), out var unhonourable);

        unhonourable.Count.ShouldBe(1);
        unhonourable[0].ShouldContain("[PersonalData]");
        unhonourable[0].ShouldContain("[EncryptedField]");
    }

    /// <summary>
    /// LIVENESS, and the control that stops the refusal degenerating into "refuse any two annotations".
    /// Personal data plus sensitive is not a contradiction: sensitive states no purpose.
    /// </summary>
    [Fact]
    public void StillAcceptAPropertyCarryingBothPersonalDataAndSensitive()
    {
        var selected = EncryptedFieldBinding.Select<PersonalAndSensitive>();

        selected.Length.ShouldBe(1,
            "sensitive states no key purpose, so it cannot disagree with the per-subject key. Refusing "
            + "this would turn a precedence rule into a blanket refusal of any two annotations");
    }

    /// <summary>
    /// LIVENESS. The pre-existing annotation still selects exactly as before, and its stated purpose is
    /// untouched — the widening must not have changed the path that already worked.
    /// </summary>
    [Fact]
    public void StillSelectAnEncryptedFieldPropertyAndLeaveItsPurposeIntact()
    {
        var selected = EncryptedFieldBinding.Select<ExplicitlyEncrypted>();

        selected.Length.ShouldBe(1);
        selected[0].Name.ShouldBe(nameof(ExplicitlyEncrypted.Secret));

        var attribute = selected[0].GetCustomAttributes(typeof(EncryptedFieldAttribute), inherit: false);
        attribute.Length.ShouldBe(1);
        ((EncryptedFieldAttribute)attribute[0]).Purpose.ShouldBe("vault");
    }

    /// <summary>
    /// LIVENESS. An unannotated type still selects nothing — the widening must not select everything.
    /// </summary>
    [Fact]
    public void StillSelectNothingFromATypeCarryingNeitherAnnotation()
    {
        var selected = EncryptedFieldBinding.Select<NoAnnotations>();

        selected.Length.ShouldBe(0, "a widened predicate that selects an unannotated property would "
            + "encrypt fields nobody asked to protect and silently change what is stored");
    }

    private sealed class CredentialsOnlySensitive
    {
        [Sensitive]
        public string? ApiKey { get; set; }

        public string? Region { get; set; }
    }

    private sealed class SensitiveOnAnUnsupportedType
    {
        [Sensitive]
        public int Payload { get; set; }
    }

    private sealed class ErasableAndPinned
    {
        [PersonalData]
        [EncryptedField(Purpose = "vault")]
        public string? Contradiction { get; set; }
    }

    private sealed class PersonalAndSensitive
    {
        [PersonalData]
        [Sensitive]
        public string? Both { get; set; }
    }

    private sealed class ExplicitlyEncrypted
    {
        [EncryptedField(Purpose = "vault")]
        public string? Secret { get; set; }
    }

    private sealed class NoAnnotations
    {
        public string? Plain { get; set; }
    }
}
