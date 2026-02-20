// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Soc2;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ControlDefinitionShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        var def = new ControlDefinition
        {
            ControlId = "CUSTOM-1",
            Criterion = TrustServicesCriterion.CC6_LogicalAccess,
            Name = "Custom Control",
            Description = "A custom control",
            Implementation = "Manual review"
        };

        def.Type.ShouldBe(ControlType.Preventive);
        def.Frequency.ShouldBe(ControlFrequency.Continuous);
        def.ValidatorTypeName.ShouldBeNull();
    }

    [Fact]
    public void AllowCustomConfiguration()
    {
        var def = new ControlDefinition
        {
            ControlId = "CUSTOM-2",
            Criterion = TrustServicesCriterion.CC7_SystemOperations,
            Name = "Detective Control",
            Description = "Detects anomalies",
            Implementation = "Automated monitoring",
            Type = ControlType.Detective,
            Frequency = ControlFrequency.Daily,
            ValidatorTypeName = "MyProject.CustomValidator"
        };

        def.Type.ShouldBe(ControlType.Detective);
        def.Frequency.ShouldBe(ControlFrequency.Daily);
        def.ValidatorTypeName.ShouldBe("MyProject.CustomValidator");
    }

    [Theory]
    [InlineData(ControlFrequency.Continuous)]
    [InlineData(ControlFrequency.PerTransaction)]
    [InlineData(ControlFrequency.Daily)]
    [InlineData(ControlFrequency.Weekly)]
    [InlineData(ControlFrequency.Monthly)]
    [InlineData(ControlFrequency.Quarterly)]
    [InlineData(ControlFrequency.Annually)]
    [InlineData(ControlFrequency.OnDemand)]
    public void SupportAllFrequencyValues(ControlFrequency frequency)
    {
        Enum.IsDefined(typeof(ControlFrequency), frequency).ShouldBeTrue();
    }
}
