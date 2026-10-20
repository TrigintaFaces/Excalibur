// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Options;

namespace Boundary.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
[Trait("Pattern", "Boundary")]
public sealed class OptionLivenessBaselineEvidenceShould
{
    private static readonly Lazy<OptionLivenessScanner.ScanResult> ProbeScan = new(() =>
        OptionLivenessScanner.Scan([typeof(OptionLivenessBaselineEvidenceShould).Assembly.Location]));

    [Fact]
    public void RetainAValidatedOptionWhenOnlyAnUnrelatedGenericGetterCouldBeItsRuntimeReader()
    {
        var scan = ProbeScan.Value;
        var option = scan.Population.Single(property =>
            property.DeclaringType == typeof(UncertainBaselineProbeOptions).FullName && property.PropertyName == "Mode");

        scan.UnreadableAssemblies.ShouldBeEmpty();
        scan.MalformedMethodBodies.ShouldBeEmpty();
        scan.Inert.ShouldNotContain(option,
            "The conservative new-inert detector must still credit an unresolved same-named getter.");
        scan.LiveOnlyViaUnresolvedReceiver.ShouldContain(option,
            "The generic fixture must really produce uncertain receiver evidence, and own validation must not establish a runtime reader.");

        PublicOptionLivenessGuardTests.FindBaselineEntriesWithRuntimeReaders(scan, [option.Id])
            .ShouldBeEmpty("An unresolved same-named getter cannot establish that this baselined option was wired.");
    }

    [Fact]
    public void RemoveAnOptionFromTheBaselineWhenItsOwnRuntimeReaderIsAttributed()
    {
        var scan = ProbeScan.Value;
        var option = scan.Population.Single(property =>
            property.DeclaringType == typeof(ConfirmedBaselineProbeOptions).FullName && property.PropertyName == "Mode");

        scan.Inert.ShouldNotContain(option);
        scan.LiveOnlyViaUnresolvedReceiver.ShouldNotContain(option,
            "A named runtime reader must establish liveness even when unrelated generic getters share its name.");

        PublicOptionLivenessGuardTests.FindBaselineEntriesWithRuntimeReaders(scan, [option.Id])
            .ShouldBe([option.Id]);
    }
}

/// <summary>A validator-only option whose runtime liveness must remain uncertain.</summary>
public sealed class UncertainBaselineProbeOptions
{
    public int Mode { get; set; }
}

public sealed class UncertainBaselineProbeOptionsValidator : IValidateOptions<UncertainBaselineProbeOptions>
{
    public ValidateOptionsResult Validate(string? name, UncertainBaselineProbeOptions options) =>
        options.Mode >= 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail("Mode must be nonnegative.");
}

/// <summary>A genuinely consumed option that must still require baseline removal.</summary>
public sealed class ConfirmedBaselineProbeOptions
{
    public int Mode { get; set; }
}

/// <summary>An unrelated generic receiver; its Mode does not read either options type.</summary>
public sealed class UnresolvedModeReceiver<T>
{
    public int Mode { get; set; }
}

public static class BaselineEvidenceProbeConsumer
{
    public static int ReadGenericMode(UnresolvedModeReceiver<int> receiver) => receiver.Mode;

    public static int ReadConfirmedMode(ConfirmedBaselineProbeOptions options) => options.Mode;
}
