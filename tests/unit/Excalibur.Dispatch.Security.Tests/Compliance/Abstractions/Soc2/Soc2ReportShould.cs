// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Soc2;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class Soc2ReportShould
{
    [Fact]
    public void CreateFullReport()
    {
        var system = new SystemDescription
        {
            Name = "Dispatch Platform",
            Description = "Message dispatching framework",
            Services = new[] { "Messaging", "EventSourcing" },
            Infrastructure = new[] { "Azure", "SQL Server" },
            DataTypes = new[] { "PII", "Financial" }
        };

        var report = new Soc2Report
        {
            ReportId = Guid.NewGuid(),
            ReportType = Soc2ReportType.TypeII,
            Title = "SOC 2 Type II Report",
            PeriodStart = DateTimeOffset.UtcNow.AddDays(-90),
            PeriodEnd = DateTimeOffset.UtcNow,
            CategoriesIncluded = new[] { TrustServicesCategory.Security, TrustServicesCategory.Availability },
            System = system,
            ControlSections = Array.Empty<ControlSection>(),
            Opinion = AuditorOpinion.Unqualified
        };

        report.ReportType.ShouldBe(Soc2ReportType.TypeII);
        report.Opinion.ShouldBe(AuditorOpinion.Unqualified);
        report.Exceptions.ShouldBeEmpty();
        report.TenantId.ShouldBeNull();
    }

    [Fact]
    public void CreateSystemDescriptionWithDefaults()
    {
        var system = new SystemDescription
        {
            Name = "Test",
            Description = "Test system",
            Services = new[] { "Svc1" },
            Infrastructure = new[] { "Infra1" },
            DataTypes = new[] { "Data1" }
        };

        system.ThirdParties.ShouldBeEmpty();
    }

    [Fact]
    public void CreateControlSection()
    {
        var section = new ControlSection
        {
            Criterion = TrustServicesCriterion.CC6_LogicalAccess,
            Description = "Encryption controls",
            Controls = new[]
            {
                new ControlDescription
                {
                    ControlId = "ENC-1",
                    Name = "Field Encryption",
                    Description = "Encrypts PII fields",
                    Implementation = "AES-256-GCM",
                    Type = ControlType.Preventive,
                    Frequency = ControlFrequency.PerTransaction
                }
            },
            IsMet = true
        };

        section.TestResults.ShouldBeNull();
        section.IsMet.ShouldBeTrue();
        section.Controls.Count.ShouldBe(1);
    }

    [Fact]
    public void CreateReportException()
    {
        var exception = new ReportException
        {
            ExceptionId = "EXC-1",
            Criterion = TrustServicesCriterion.CC6_LogicalAccess,
            ControlId = "ENC-1",
            Description = "Missing encryption on legacy field",
            ManagementResponse = "Will remediate in Q2",
            RemediationPlan = "Migrate legacy fields to encrypted store"
        };

        exception.ManagementResponse.ShouldNotBeNull();
        exception.RemediationPlan.ShouldNotBeNull();
    }

    [Fact]
    public void CreateTestResult()
    {
        var result = new TestResult
        {
            ControlId = "ENC-1",
            TestProcedure = "Selected 25 records and verified encryption",
            SampleSize = 25,
            ExceptionsFound = 0,
            Outcome = TestOutcome.NoExceptions,
            Notes = "All verified"
        };

        result.Outcome.ShouldBe(TestOutcome.NoExceptions);
        result.Notes.ShouldBe("All verified");
    }

    [Theory]
    [InlineData(AuditorOpinion.Unqualified)]
    [InlineData(AuditorOpinion.Qualified)]
    [InlineData(AuditorOpinion.Adverse)]
    [InlineData(AuditorOpinion.Disclaimer)]
    public void SupportAllAuditorOpinions(AuditorOpinion opinion)
    {
        var report = new Soc2Report
        {
            ReportId = Guid.NewGuid(),
            ReportType = Soc2ReportType.TypeI,
            Title = "Test",
            PeriodStart = DateTimeOffset.UtcNow,
            PeriodEnd = DateTimeOffset.UtcNow,
            CategoriesIncluded = new[] { TrustServicesCategory.Security },
            System = new SystemDescription
            {
                Name = "T",
                Description = "D",
                Services = Array.Empty<string>(),
                Infrastructure = Array.Empty<string>(),
                DataTypes = Array.Empty<string>()
            },
            ControlSections = Array.Empty<ControlSection>(),
            Opinion = opinion
        };

        report.Opinion.ShouldBe(opinion);
    }

    [Theory]
    [InlineData(ControlType.Preventive)]
    [InlineData(ControlType.Detective)]
    [InlineData(ControlType.Corrective)]
    public void SupportAllControlTypes(ControlType type)
    {
        var desc = new ControlDescription
        {
            ControlId = "c1",
            Name = "test",
            Description = "desc",
            Implementation = "impl",
            Type = type,
            Frequency = ControlFrequency.Continuous
        };

        desc.Type.ShouldBe(type);
    }
}
