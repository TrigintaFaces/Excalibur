// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Options;

namespace Boundary.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
[Trait("Pattern", "Regression")]
public sealed class OptionLivenessBridgeShould
{
    [Fact]
    public void RecognizeMatchedForwardingAccessorsWhoseCanonicalValueReachesAConsumer()
    {
        var options = new ForwardingLegacyProbeOptions { BridgeProbeText = "configured" };
        ForwardingProbeConsumer.Read(options).ShouldBe("configured");

        Inert(typeof(ForwardingLegacyProbeOptions), nameof(ForwardingLegacyProbeOptions.BridgeProbeText)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(typeof(GetterOnlyForwardingProbeOptions))]
    [InlineData(typeof(SetterOnlyForwardingProbeOptions))]
    [InlineData(typeof(SplitForwardingProbeOptions))]
    public void RefuseUnmatchedStorageEvenWhenTheBasePropertyIsConsumed(Type type)
    {
        Inert(type, nameof(ForwardingProbeBaseOptions.BridgeProbeText)).ShouldBeTrue();
    }

    [Fact]
    public void KeepBothViewsInertWhenOnlyTheirValidationOrOwnMembersReadThem()
    {
        foreach (var type in new[] { typeof(ForwardingProbeBaseOptions), typeof(ForwardingLegacyProbeOptions) })
        {
            Inert(type, nameof(ForwardingProbeBaseOptions.BridgeValidatorOnly)).ShouldBeTrue();
            Inert(type, nameof(ForwardingProbeBaseOptions.BridgeSelfOnly)).ShouldBeTrue();
        }
    }

    [Fact]
    public void PreserveTheGenuineUnrelatedDirectReader()
    {
        Inert(typeof(UnrelatedForwardingProbeOptions), nameof(UnrelatedForwardingProbeOptions.BridgeProbeText)).ShouldBeFalse();
    }

    [Fact]
    public void RefuseAForwardingAliasWhoseCanonicalAssemblyWasNotScanned()
    {
        var legacy = typeof(Excalibur.Hosting.Web.Diagnostics.ProblemDetailsOptions);
        var scan = OptionLivenessScanner.Scan([legacy.Assembly.Location]);

        scan.UnreadableAssemblies.ShouldBeEmpty();
        scan.Population.ShouldContain(p => p.DeclaringType == legacy.FullName && p.PropertyName == "StatusTypeBaseUrl");
        scan.Inert.ShouldContain(p => p.DeclaringType == legacy.FullName && p.PropertyName == "StatusTypeBaseUrl");
    }

    [Fact]
    public void PreserveBodylessInterfaceOptionsAndTheirGenuineReader()
    {
        Inert(typeof(IForwardingInterfaceProbeOptions), nameof(IForwardingInterfaceProbeOptions.BridgeProbeText)).ShouldBeFalse();
    }

    [Fact]
    public void RefuseDuplicateAssemblyIdentityInsteadOfInflatingTheCensus()
    {
        var path = typeof(OptionLivenessBridgeShould).Assembly.Location;
        var scan = OptionLivenessScanner.Scan([path, path]);

        scan.AssembliesScanned.ShouldBe(1);
        scan.UnreadableAssemblies.ShouldContain(message => message.Contains("duplicate assembly simple name", StringComparison.Ordinal));
    }

    private static bool Inert(Type type, string property)
    {
        var scan = OptionLivenessScanner.Scan([typeof(OptionLivenessBridgeShould).Assembly.Location]);
        var id = type.FullName + "::" + property;
        scan.UnreadableAssemblies.ShouldBeEmpty();
        scan.MalformedMethodBodies.ShouldBeEmpty();
        scan.Population.ShouldContain(p => p.Id == id);
        return scan.Inert.Any(p => p.Id == id);
    }
}

/// <summary>Canonical storage for the accessor identity controls.</summary>
public class ForwardingProbeBaseOptions
{
    public string BridgeProbeText { get; set; } = "initial";

    public string OtherProbeText { get; set; } = "other";

    public string BridgeValidatorOnly { get; set; } = "validation";

    public string BridgeSelfOnly { get; set; } = "self";
}

/// <summary>Two views of exactly the same canonical storage.</summary>
public sealed class ForwardingLegacyProbeOptions : ForwardingProbeBaseOptions
{
    public new string BridgeProbeText { get => base.BridgeProbeText; set => base.BridgeProbeText = value; }

    public new string BridgeValidatorOnly { get => base.BridgeValidatorOnly; set => base.BridgeValidatorOnly = value; }

    public new string BridgeSelfOnly { get => base.BridgeSelfOnly; set => base.BridgeSelfOnly = value; }

    public string ReadSelf() => base.BridgeSelfOnly;
}

/// <summary>A getter bridge whose consumer writes different storage.</summary>
public sealed class GetterOnlyForwardingProbeOptions : ForwardingProbeBaseOptions
{
    private string _local = string.Empty;

    public new string BridgeProbeText { get => base.BridgeProbeText; set => _local = value; }

    public string LocalText => _local;
}

/// <summary>A setter bridge whose getter observes different storage.</summary>
public sealed class SetterOnlyForwardingProbeOptions : ForwardingProbeBaseOptions
{
    public new string BridgeProbeText { get => "independent"; set => base.BridgeProbeText = value; }
}

/// <summary>Accessors forwarding to two distinct base properties.</summary>
public sealed class SplitForwardingProbeOptions : ForwardingProbeBaseOptions
{
    public new string BridgeProbeText { get => base.BridgeProbeText; set => base.OtherProbeText = value; }
}

/// <summary>A same-named property on an unrelated type.</summary>
public sealed class UnrelatedForwardingProbeOptions
{
    public string BridgeProbeText { get; set; } = "unrelated";
}

/// <summary>Real reader of canonical and unrelated storage, never the shadow accessors.</summary>
public static class ForwardingProbeConsumer
{
    public static string ReadInterface(IForwardingInterfaceProbeOptions options) => options.BridgeProbeText;

    public static string Read(ForwardingProbeBaseOptions options) => options.BridgeProbeText;

    public static string ReadUnrelated(UnrelatedForwardingProbeOptions options) => options.BridgeProbeText;
}

/// <summary>Public bodyless accessors remain ordinary options, never forwarding bridges.</summary>
public interface IForwardingInterfaceProbeOptions
{
    string BridgeProbeText { get; set; }
}

/// <summary>Validation alone must not establish canonical or bridged liveness.</summary>
public sealed class ForwardingCanonicalProbeValidator : IValidateOptions<ForwardingProbeBaseOptions>
{
    public ValidateOptionsResult Validate(string? name, ForwardingProbeBaseOptions options) =>
        options.BridgeValidatorOnly.Length > 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail("probe");
}

/// <summary>Validation of the shadow view is still only validation.</summary>
public sealed class ForwardingLegacyProbeValidator : IValidateOptions<ForwardingLegacyProbeOptions>
{
    public ValidateOptionsResult Validate(string? name, ForwardingLegacyProbeOptions options) =>
        options.BridgeValidatorOnly.Length > 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail("probe");
}
