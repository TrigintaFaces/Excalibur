// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Delivery;

namespace Excalibur.Dispatch.Tests.Options.Delivery;

/// <summary>
/// bd-53hho5 (inbox-store half, ADR-336 cl.2) — independent engage-test (author≠impl) for the
/// EFFECTIVE-capability probe in the startup capability validators.
/// </summary>
/// <remarks>
/// A decorating inbox store (e.g. the transparent-encryption decorator) statically declares the segregated
/// capability interfaces so it can forward them — so a plain <c>is IClaimableInboxStore</c> check PASSES for
/// the decorator even when its wrapped inner store lacks the capability, and the decorator then throws
/// <see cref="NotSupportedException"/> at first call (pass-then-throw-at-runtime). The fix: a decorator reports
/// its <em>forwarded</em> capability via <see cref="IInboxStoreCapabilities"/>, and the validators probe that
/// effective report (falling back to the direct interface check for plain stores). This lock binds the probe:
/// a store that statically implements the capability interface but reports the effective capability as
/// <see langword="false"/> MUST fail fast at startup. Non-vacuous: RED on the pre-fix validator that only did
/// the direct <c>is</c> check (which passes for such a store, deferring the failure to a runtime throw).
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Feature", "Inbox")]
public sealed class CapabilityEffectiveProbeShould
{
    // --- IdempotencyClaimCapabilityValidator: effective IClaimableInboxStore probe ---

    [Fact]
    public void FailFast_WhenEffectiveClaimIsFalse_DespiteStaticClaimableInterface()
    {
        // A decorator-shaped store: statically IClaimableInboxStore (would pass a naive `is` check) BUT its
        // effective report says it cannot actually forward a claim.
        var store = A.Fake<IInboxStore>(o => o
            .Implements<IClaimableInboxStore>()
            .Implements<IInboxStoreCapabilities>());
        A.CallTo(() => ((IInboxStoreCapabilities)store).SupportsClaim).Returns(false);

        var result = new IdempotencyClaimCapabilityValidator(store).Validate(null, new InboxOptions());

        // RED on the pre-fix `is IClaimableInboxStore`-only validator (which passes here, then throws at runtime).
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldNotBeNull();
        result.FailureMessage.ShouldContain(nameof(IClaimableInboxStore));
    }

    [Fact]
    public void Succeed_WhenEffectiveClaimIsTrue()
    {
        var store = A.Fake<IInboxStore>(o => o
            .Implements<IClaimableInboxStore>()
            .Implements<IInboxStoreCapabilities>());
        A.CallTo(() => ((IInboxStoreCapabilities)store).SupportsClaim).Returns(true);

        var result = new IdempotencyClaimCapabilityValidator(store).Validate(null, new InboxOptions());

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Succeed_ForPlainClaimableStore_WithoutCapabilitiesReport()
    {
        // No IInboxStoreCapabilities -> the validator falls back to the direct interface check (no regression
        // for plain, non-decorating stores).
        var store = A.Fake<IInboxStore>(o => o.Implements<IClaimableInboxStore>());

        var result = new IdempotencyClaimCapabilityValidator(store).Validate(null, new InboxOptions());

        result.Succeeded.ShouldBeTrue();
    }

    // --- InboxProcessingCapabilityValidator: effective IProcessingTrackingInboxStore probe ---

    [Fact]
    public void FailFast_WhenEffectiveProcessingTrackingIsFalse_DespiteStaticInterface()
    {
        var store = A.Fake<IInboxStore>(o => o
            .Implements<IProcessingTrackingInboxStore>()
            .Implements<IInboxStoreCapabilities>());
        A.CallTo(() => ((IInboxStoreCapabilities)store).SupportsProcessingTracking).Returns(false);

        var result = new InboxProcessingCapabilityValidator(store).Validate(null, new InboxOptions());

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldNotBeNull();
        result.FailureMessage.ShouldContain(nameof(IProcessingTrackingInboxStore));
    }

    [Fact]
    public void Succeed_WhenEffectiveProcessingTrackingIsTrue()
    {
        var store = A.Fake<IInboxStore>(o => o
            .Implements<IProcessingTrackingInboxStore>()
            .Implements<IInboxStoreCapabilities>());
        A.CallTo(() => ((IInboxStoreCapabilities)store).SupportsProcessingTracking).Returns(true);

        var result = new InboxProcessingCapabilityValidator(store).Validate(null, new InboxOptions());

        result.Succeeded.ShouldBeTrue();
    }
}
