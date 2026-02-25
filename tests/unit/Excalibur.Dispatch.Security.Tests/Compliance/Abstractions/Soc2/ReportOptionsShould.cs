// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Soc2;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ReportOptionsShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        var options = new ReportOptions();

        options.Categories.ShouldBeNull();
        options.IncludeDetailedEvidence.ShouldBeTrue();
        options.IncludeTestResults.ShouldBeTrue();
        options.TenantId.ShouldBeNull();
        options.CustomTitle.ShouldBeNull();
        options.IncludeManagementAssertion.ShouldBeTrue();
        options.IncludeSystemDescription.ShouldBeTrue();
        options.MaxEvidenceItemsPerCriterion.ShouldBeNull();
    }

    [Fact]
    public void AllowCustomization()
    {
        var options = new ReportOptions
        {
            Categories = new[] { TrustServicesCategory.Security, TrustServicesCategory.Availability },
            IncludeDetailedEvidence = false,
            IncludeTestResults = false,
            TenantId = "t1",
            CustomTitle = "Custom Report",
            IncludeManagementAssertion = false,
            IncludeSystemDescription = false,
            MaxEvidenceItemsPerCriterion = 10
        };

        options.Categories.Length.ShouldBe(2);
        options.IncludeDetailedEvidence.ShouldBeFalse();
        options.MaxEvidenceItemsPerCriterion.ShouldBe(10);
    }

    [Theory]
    [InlineData(ExportFormat.Pdf)]
    [InlineData(ExportFormat.Excel)]
    [InlineData(ExportFormat.Csv)]
    [InlineData(ExportFormat.Json)]
    [InlineData(ExportFormat.Xml)]
    public void SupportAllExportFormats(ExportFormat format)
    {
        // Verify all enum values exist
        Enum.IsDefined(typeof(ExportFormat), format).ShouldBeTrue();
    }
}
