// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Microsoft.Extensions.Logging;

using QuestPDF.Infrastructure;

using Excalibur.Dispatch.Compliance;
namespace Excalibur.Dispatch.Integration.Tests.Compliance.Soc2;

/// <summary>
/// Integration tests for SOC2 PDF export using QuestPDF.
/// Tests full PDF generation workflows including large reports and stress scenarios.
/// Per Sprint 396 T396.10.
/// </summary>
[Trait("Category", TestCategories.Integration)]
public sealed class Soc2PdfExportIntegrationShould
{
	private readonly Soc2ReportExporter _sut;
	private readonly ILogger<Soc2ReportExporter> _logger;

	static Soc2PdfExportIntegrationShould()
	{
		// Configure QuestPDF license before any PDF operations
		QuestPDF.Settings.License = LicenseType.Community;
	}

	public Soc2PdfExportIntegrationShould()
	{
		_logger = A.Fake<ILogger<Soc2ReportExporter>>();
		_sut = new Soc2ReportExporter(_logger);
	}

	#region Full PDF Generation Tests

	[Fact]
	public async Task ExportAsync_GenerateValidPdfWithAllSections()
	{
		// Arrange - Full report with all sections populated
		var report = CreateComprehensiveReport();

		// Act
		var result = await _sut.ExportAsync(report, ExportFormat.Pdf, null, CancellationToken.None);

		// Assert - PDF should be valid
		_ = result.ShouldNotBeNull();
		result.Data.Length.ShouldBeGreaterThan(1000); // PDF with content should be substantial

		// Verify PDF magic bytes
		var header = System.Text.Encoding.ASCII.GetString(result.Data, 0, 4);
		header.ShouldBe("%PDF");

		// PDF should end with %%EOF (allowing for whitespace)
		var trailer = System.Text.Encoding.ASCII.GetString(result.Data, result.Data.Length - 10, 10);
		trailer.ShouldContain("%%EOF");

		// Verify metadata
		result.ContentType.ShouldBe("application/pdf");
		result.FileName.ShouldEndWith(".pdf");
		result.Checksum.Length.ShouldBe(64); // SHA-256 hex
	}

	[Fact]
	public async Task ExportAsync_GeneratePdfForTypeIReport()
	{
		// Arrange
		var report = CreateTypeIReport();

		// Act
		var result = await _sut.ExportAsync(report, ExportFormat.Pdf, null, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Data.Length.ShouldBeGreaterThan(500);

		var header = System.Text.Encoding.ASCII.GetString(result.Data, 0, 4);
		header.ShouldBe("%PDF");
	}

	[Fact]
	public async Task ExportAsync_GeneratePdfForTypeIIReport()
	{
		// Arrange
		var report = CreateTypeIIReport();

		// Act
		var result = await _sut.ExportAsync(report, ExportFormat.Pdf, null, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Data.Length.ShouldBeGreaterThan(500);

		var header = System.Text.Encoding.ASCII.GetString(result.Data, 0, 4);
		header.ShouldBe("%PDF");
	}

	[Fact]
	public async Task ExportAsync_GeneratePdfWithCustomTitle()
	{
		// Arrange
		var report = CreateMinimalReport();
		var options = new Soc2ReportExportOptions
		{
			CustomTitle = "Custom Compliance Report - Acme Corporation"
		};

		// Act
		var result = await _sut.ExportAsync(report, ExportFormat.Pdf, options, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Data.Length.ShouldBeGreaterThan(500);
	}

	[Fact]
	public async Task ExportAsync_GeneratePdfWithExceptions()
	{
		// Arrange
		var report = CreateReportWithExceptions();

		// Act
		var result = await _sut.ExportAsync(report, ExportFormat.Pdf, null, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Data.Length.ShouldBeGreaterThan(500);

		var header = System.Text.Encoding.ASCII.GetString(result.Data, 0, 4);
		header.ShouldBe("%PDF");
	}

	[Fact]
	public async Task ExportAsync_GeneratePdfWithTestResults()
	{
		// Arrange
		var report = CreateReportWithTestResults();

		// Act
		var result = await _sut.ExportAsync(report, ExportFormat.Pdf, null, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Data.Length.ShouldBeGreaterThan(500);

		var header = System.Text.Encoding.ASCII.GetString(result.Data, 0, 4);
		header.ShouldBe("%PDF");
	}

	#endregion

	#region Large Report Stress Tests

	[Fact]
	public async Task ExportAsync_HandleLargeReportWithManyControls()
	{
		// Arrange - Create a report with many controls (stress test)
		var controls = new List<ControlDescription>();
		for (var i = 0; i < 100; i++)
		{
			controls.Add(new ControlDescription
			{
				ControlId = $"CC{i + 1:D3}",
				Name = $"Control {i + 1}",
				Description = $"Control {i + 1}: This is a test control with a detailed description that explains what the control does and why it is important for compliance.",
				Implementation = $"Implementation details for control {i + 1}",
				Type = (ControlType)(i % 3),
				Frequency = (ControlFrequency)(i % 8)
			});
		}

		var report = new Soc2Report
		{
			ReportId = Guid.NewGuid(),
			ReportType = Soc2ReportType.TypeII,
			Title = "Large SOC 2 Type II Report - Stress Test",
			PeriodStart = DateTimeOffset.UtcNow.AddMonths(-12),
			PeriodEnd = DateTimeOffset.UtcNow,
			Opinion = AuditorOpinion.Unqualified,
			GeneratedAt = DateTimeOffset.UtcNow,
			CategoriesIncluded = [
				TrustServicesCategory.Security,
				TrustServicesCategory.Availability,
				TrustServicesCategory.ProcessingIntegrity,
				TrustServicesCategory.Confidentiality,
				TrustServicesCategory.Privacy
			],
			System = CreateSystemDescription(),
			ControlSections = [
				new ControlSection
				{
					Criterion = TrustServicesCriterion.CC1_ControlEnvironment,
					Description = "Large control section with 100 controls",
					IsMet = true,
					Controls = controls
				}
			]
		};

		// Act
		var result = await _sut.ExportAsync(report, ExportFormat.Pdf, null, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Data.Length.ShouldBeGreaterThan(10000); // Large report should produce substantial PDF
		result.Data.Length.ShouldBeLessThan(50_000_000); // But not unreasonably large (50MB cap)

		var header = System.Text.Encoding.ASCII.GetString(result.Data, 0, 4);
		header.ShouldBe("%PDF");
	}

	[Fact]
	public async Task ExportAsync_HandleReportWithManyExceptions()
	{
		// Arrange
		var exceptions = new List<ReportException>();
		for (var i = 0; i < 25; i++)
		{
			exceptions.Add(new ReportException
			{
				ExceptionId = $"EXC-{i + 1:D3}",
				ControlId = $"CC{i + 1}",
				Criterion = (TrustServicesCriterion)(i % 9), // Cycle through CC1-CC9
				Description = $"Exception {i + 1}: Detailed description of the exception discovered during testing.",
				ManagementResponse = $"Management response for exception {i + 1}: We acknowledge this finding and are implementing corrective actions.",
				RemediationPlan = $"Remediation plan {i + 1}: Steps to address the exception."
			});
		}

		var report = new Soc2Report
		{
			ReportId = Guid.NewGuid(),
			ReportType = Soc2ReportType.TypeII,
			Title = "SOC 2 Report with Multiple Exceptions",
			PeriodStart = DateTimeOffset.UtcNow.AddMonths(-6),
			PeriodEnd = DateTimeOffset.UtcNow,
			Opinion = AuditorOpinion.Qualified,
			GeneratedAt = DateTimeOffset.UtcNow,
			CategoriesIncluded = [TrustServicesCategory.Security],
			System = CreateSystemDescription(),
			ControlSections = [
				new ControlSection
				{
					Criterion = TrustServicesCriterion.CC1_ControlEnvironment,
					Description = "Control section",
					IsMet = false,
					Controls = [
						new ControlDescription
						{
							ControlId = "CC1",
							Name = "Test Control",
							Description = "Test control description",
							Implementation = "Implementation",
							Type = ControlType.Preventive,
							Frequency = ControlFrequency.Continuous
						}
					]
				}
			],
			Exceptions = exceptions
		};

		// Act
		var result = await _sut.ExportAsync(report, ExportFormat.Pdf, null, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Data.Length.ShouldBeGreaterThan(5000);

		var header = System.Text.Encoding.ASCII.GetString(result.Data, 0, 4);
		header.ShouldBe("%PDF");
	}

	[Fact]
	public async Task ExportAsync_HandleReportWithLongDescriptions()
	{
		// Arrange - Test with very long text fields
		var longDescription = string.Join(" ", Enumerable.Repeat("This is a very long description that tests the PDF layout capabilities. ", 50));

		var report = new Soc2Report
		{
			ReportId = Guid.NewGuid(),
			ReportType = Soc2ReportType.TypeII,
			Title = "SOC 2 Report with Long Text Fields",
			PeriodStart = DateTimeOffset.UtcNow.AddMonths(-6),
			PeriodEnd = DateTimeOffset.UtcNow,
			Opinion = AuditorOpinion.Unqualified,
			GeneratedAt = DateTimeOffset.UtcNow,
			CategoriesIncluded = [TrustServicesCategory.Security],
			System = new SystemDescription
			{
				Name = "System with Very Long Name That Tests Layout Capabilities",
				Description = longDescription,
				Services = ["Authentication", "Authorization"],
				Infrastructure = ["AWS Cloud"],
				DataTypes = ["PII"]
			},
			ControlSections = [
				new ControlSection
				{
					Criterion = TrustServicesCriterion.CC1_ControlEnvironment,
					Description = longDescription,
					IsMet = true,
					Controls = [
						new ControlDescription
						{
							ControlId = "CC1",
							Name = "Control with Long Name That Tests Text Wrapping in PDF Tables",
							Description = longDescription,
							Implementation = longDescription,
							Type = ControlType.Preventive,
							Frequency = ControlFrequency.Continuous
						}
					]
				}
			]
		};

		// Act
		var result = await _sut.ExportAsync(report, ExportFormat.Pdf, null, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Data.Length.ShouldBeGreaterThan(1000);

		var header = System.Text.Encoding.ASCII.GetString(result.Data, 0, 4);
		header.ShouldBe("%PDF");
	}

	#endregion

	#region Multiple Export Tests

	[Fact]
	public async Task ExportAsync_SupportMultipleConcurrentExports()
	{
		// Arrange
		var reports = Enumerable.Range(0, 5)
			.Select(i => CreateMinimalReport(i.ToString()))
			.ToList();

		// Act - Export all concurrently
		var tasks = reports.Select(r => _sut.ExportAsync(r, ExportFormat.Pdf, null, CancellationToken.None));
		var results = await Task.WhenAll(tasks);

		// Assert - All should succeed
		results.Length.ShouldBe(5);
		foreach (var result in results)
		{
			_ = result.ShouldNotBeNull();
			var header = System.Text.Encoding.ASCII.GetString(result.Data, 0, 4);
			header.ShouldBe("%PDF");
		}

		// All should have unique checksums (different report IDs)
		var checksums = results.Select(r => r.Checksum).Distinct();
		checksums.Count().ShouldBe(5);
	}

	[Fact]
	public async Task ExportAsync_ProduceDifferentPdfsForDifferentReports()
	{
		// Arrange
		var report1 = CreateMinimalReport("report-1");
		var report2 = CreateMinimalReport("report-2");

		// Act
		var result1 = await _sut.ExportAsync(report1, ExportFormat.Pdf, null, CancellationToken.None);
		var result2 = await _sut.ExportAsync(report2, ExportFormat.Pdf, null, CancellationToken.None);

		// Assert - Different reports should produce different PDFs
		result1.Checksum.ShouldNotBe(result2.Checksum);
	}

	[Fact]
	public async Task ExportAsync_ProduceSamePdfForSameReport()
	{
		// Arrange
		var report = CreateMinimalReport("consistent-report");

		// Act
		var result1 = await _sut.ExportAsync(report, ExportFormat.Pdf, null, CancellationToken.None);
		var result2 = await _sut.ExportAsync(report, ExportFormat.Pdf, null, CancellationToken.None);

		// Assert - Same report should produce same checksum (deterministic)
		result1.Checksum.ShouldBe(result2.Checksum);
		result1.Data.Length.ShouldBe(result2.Data.Length);
	}

	#endregion

	#region All Categories Tests

	[Fact]
	public async Task ExportAsync_HandleAllTrustServiceCategories()
	{
		// Arrange - Create controls for all trust service categories
		var allCategories = Enum.GetValues<TrustServicesCategory>();
		var sections = allCategories.Select(cat => new ControlSection
		{
			Criterion = GetCriterionForCategory(cat),
			Description = $"Controls for {cat} category",
			IsMet = true,
			Controls = [
				new ControlDescription
				{
					ControlId = $"{cat}-001",
					Name = $"{cat} Control",
					Description = $"Control for {cat}",
					Implementation = "Implementation details",
					Type = ControlType.Preventive,
					Frequency = ControlFrequency.Continuous
				}
			]
		}).ToList();

		var report = new Soc2Report
		{
			ReportId = Guid.NewGuid(),
			ReportType = Soc2ReportType.TypeII,
			Title = "SOC 2 Report - All Categories",
			PeriodStart = DateTimeOffset.UtcNow.AddMonths(-12),
			PeriodEnd = DateTimeOffset.UtcNow,
			Opinion = AuditorOpinion.Unqualified,
			GeneratedAt = DateTimeOffset.UtcNow,
			CategoriesIncluded = [.. allCategories],
			System = CreateSystemDescription(),
			ControlSections = sections
		};

		// Act
		var result = await _sut.ExportAsync(report, ExportFormat.Pdf, null, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Data.Length.ShouldBeGreaterThan(1000);

		var header = System.Text.Encoding.ASCII.GetString(result.Data, 0, 4);
		header.ShouldBe("%PDF");
	}

	#endregion

	#region Helper Methods

	private static SystemDescription CreateSystemDescription()
	{
		return new SystemDescription
		{
			Name = "Test Cloud Platform",
			Description = "A comprehensive cloud platform for testing SOC 2 compliance",
			Services = ["Authentication", "Authorization", "Data Storage", "Analytics"],
			Infrastructure = ["AWS Cloud", "Postgres Database", "Redis Cache"],
			DataTypes = ["PII", "Financial Records", "Health Information"],
			ThirdParties = ["Stripe", "Twilio", "SendGrid"]
		};
	}

	private static Soc2Report CreateComprehensiveReport()
	{
		var controls = new List<ControlDescription>
		{
			new()
			{
				ControlId = "CC1.1",
				Name = "Control Environment",
				Description = "Control environment sets the tone for the organization",
				Implementation = "Executive leadership demonstrates commitment to ethics",
				Type = ControlType.Preventive,
				Frequency = ControlFrequency.Continuous
			},
			new()
			{
				ControlId = "CC6.1",
				Name = "Logical Access Security",
				Description = "Logical access security software and infrastructure",
				Implementation = "Multi-factor authentication required for all access",
				Type = ControlType.Preventive,
				Frequency = ControlFrequency.PerTransaction
			}
		};

		var testResults = new List<TestResult>
		{
			new()
			{
				ControlId = "CC1.1",
				TestProcedure = "Interviewed management about ethical standards",
				SampleSize = 25,
				ExceptionsFound = 0,
				Outcome = TestOutcome.NoExceptions,
				Notes = "No exceptions noted"
			}
		};

		return new Soc2Report
		{
			ReportId = Guid.NewGuid(),
			ReportType = Soc2ReportType.TypeII,
			Title = "Comprehensive SOC 2 Type II Examination Report",
			PeriodStart = DateTimeOffset.UtcNow.AddMonths(-12),
			PeriodEnd = DateTimeOffset.UtcNow,
			Opinion = AuditorOpinion.Unqualified,
			GeneratedAt = DateTimeOffset.UtcNow,
			CategoriesIncluded = [
				TrustServicesCategory.Security,
				TrustServicesCategory.Availability,
				TrustServicesCategory.Confidentiality,
				TrustServicesCategory.ProcessingIntegrity,
				TrustServicesCategory.Privacy
			],
			System = CreateSystemDescription(),
			ControlSections = [
				new ControlSection
				{
					Criterion = TrustServicesCriterion.CC1_ControlEnvironment,
					Description = "COSO Principle 1 - Demonstrates Commitment to Integrity",
					IsMet = true,
					Controls = controls,
					TestResults = testResults
				},
				new ControlSection
				{
					Criterion = TrustServicesCriterion.CC6_LogicalAccess,
					Description = "CC6 - Logical and Physical Access Controls",
					IsMet = true,
					Controls = [controls[1]]
				}
			],
			Exceptions = [
				new ReportException
				{
					ExceptionId = "EXC-001",
					ControlId = "CC6.1",
					Criterion = TrustServicesCriterion.CC6_LogicalAccess,
					Description = "Minor password complexity issue detected",
					ManagementResponse = "Enhanced password policy implemented"
				}
			]
		};
	}

	private static Soc2Report CreateTypeIReport()
	{
		return new Soc2Report
		{
			ReportId = Guid.NewGuid(),
			ReportType = Soc2ReportType.TypeI,
			Title = "SOC 2 Type I Examination Report",
			PeriodStart = DateTimeOffset.UtcNow,
			PeriodEnd = DateTimeOffset.UtcNow, // Type I is point-in-time
			Opinion = AuditorOpinion.Unqualified,
			GeneratedAt = DateTimeOffset.UtcNow,
			CategoriesIncluded = [TrustServicesCategory.Security],
			System = CreateSystemDescription(),
			ControlSections = [
				new ControlSection
				{
					Criterion = TrustServicesCriterion.CC1_ControlEnvironment,
					Description = "Control environment",
					IsMet = true,
					Controls = [
						new ControlDescription
						{
							ControlId = "CC1.1",
							Name = "Control Environment",
							Description = "Control environment policy",
							Implementation = "Policy implemented",
							Type = ControlType.Preventive,
							Frequency = ControlFrequency.Continuous
						}
					]
				}
			]
		};
	}

	private static Soc2Report CreateTypeIIReport()
	{
		return new Soc2Report
		{
			ReportId = Guid.NewGuid(),
			ReportType = Soc2ReportType.TypeII,
			Title = "SOC 2 Type II Examination Report",
			PeriodStart = DateTimeOffset.UtcNow.AddMonths(-6),
			PeriodEnd = DateTimeOffset.UtcNow,
			Opinion = AuditorOpinion.Unqualified,
			GeneratedAt = DateTimeOffset.UtcNow,
			CategoriesIncluded = [TrustServicesCategory.Security, TrustServicesCategory.Availability],
			System = CreateSystemDescription(),
			ControlSections = [
				new ControlSection
				{
					Criterion = TrustServicesCriterion.CC1_ControlEnvironment,
					Description = "Control environment",
					IsMet = true,
					Controls = [
						new ControlDescription
						{
							ControlId = "CC1.1",
							Name = "Control Environment",
							Description = "Control environment policy",
							Implementation = "Policy implemented",
							Type = ControlType.Preventive,
							Frequency = ControlFrequency.Continuous
						}
					],
					TestResults = [
						new TestResult
						{
							ControlId = "CC1.1",
							TestProcedure = "Review of policy documentation",
							SampleSize = 10,
							ExceptionsFound = 0,
							Outcome = TestOutcome.NoExceptions
						}
					]
				}
			]
		};
	}

	private static Soc2Report CreateReportWithExceptions()
	{
		return new Soc2Report
		{
			ReportId = Guid.NewGuid(),
			ReportType = Soc2ReportType.TypeII,
			Title = "SOC 2 Report with Exceptions",
			PeriodStart = DateTimeOffset.UtcNow.AddMonths(-6),
			PeriodEnd = DateTimeOffset.UtcNow,
			Opinion = AuditorOpinion.Qualified,
			GeneratedAt = DateTimeOffset.UtcNow,
			CategoriesIncluded = [TrustServicesCategory.Security],
			System = CreateSystemDescription(),
			ControlSections = [
				new ControlSection
				{
					Criterion = TrustServicesCriterion.CC6_LogicalAccess,
					Description = "Access control",
					IsMet = false,
					Controls = [
						new ControlDescription
						{
							ControlId = "CC6.1",
							Name = "Logical Access",
							Description = "Access control policy",
							Implementation = "MFA required",
							Type = ControlType.Preventive,
							Frequency = ControlFrequency.PerTransaction
						}
					]
				}
			],
			Exceptions = [
				new ReportException
				{
					ExceptionId = "EXC-001",
					ControlId = "CC6.1",
					Criterion = TrustServicesCriterion.CC6_LogicalAccess,
					Description = "Significant access control weakness",
					ManagementResponse = "Implementing new controls",
					RemediationPlan = "Deploy new access system"
				}
			]
		};
	}

	private static Soc2Report CreateReportWithTestResults()
	{
		return new Soc2Report
		{
			ReportId = Guid.NewGuid(),
			ReportType = Soc2ReportType.TypeII,
			Title = "SOC 2 Report with Test Results",
			PeriodStart = DateTimeOffset.UtcNow.AddMonths(-6),
			PeriodEnd = DateTimeOffset.UtcNow,
			Opinion = AuditorOpinion.Unqualified,
			GeneratedAt = DateTimeOffset.UtcNow,
			CategoriesIncluded = [TrustServicesCategory.Security],
			System = CreateSystemDescription(),
			ControlSections = [
				new ControlSection
				{
					Criterion = TrustServicesCriterion.CC1_ControlEnvironment,
					Description = "Control environment",
					IsMet = true,
					Controls = [
						new ControlDescription
						{
							ControlId = "CC1.1",
							Name = "Control Environment",
							Description = "Control environment policy",
							Implementation = "Policy implemented",
							Type = ControlType.Preventive,
							Frequency = ControlFrequency.Quarterly
						}
					],
					TestResults = [
						new TestResult
						{
							ControlId = "CC1.1",
							TestProcedure = "Inquiry - Verified access control policy is documented",
							SampleSize = 10,
							ExceptionsFound = 0,
							Outcome = TestOutcome.NoExceptions
						},
						new TestResult
						{
							ControlId = "CC1.1",
							TestProcedure = "Observation - Observed policy enforcement",
							SampleSize = 5,
							ExceptionsFound = 0,
							Outcome = TestOutcome.NoExceptions
						},
						new TestResult
						{
							ControlId = "CC1.1",
							TestProcedure = "Inspection - Reviewed sample of access logs",
							SampleSize = 25,
							ExceptionsFound = 1,
							Outcome = TestOutcome.MinorExceptions,
							Notes = "One minor exception noted - resolved"
						}
					]
				}
			]
		};
	}

	private static Soc2Report CreateMinimalReport(string? suffix = null)
	{
		// Generate deterministic GUID based on suffix for reproducible tests
		// Use SHA256 and take first 16 bytes to create consistent GUID values
		Guid reportId;
		if (suffix != null)
		{
			var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(suffix));
			var guidBytes = new byte[16];
			Array.Copy(hash, guidBytes, 16);
			reportId = new Guid(guidBytes);
		}
		else
		{
			reportId = Guid.NewGuid();
		}

		return new Soc2Report
		{
			ReportId = reportId,
			ReportType = Soc2ReportType.TypeII,
			Title = $"Minimal SOC 2 Report {suffix}",
			PeriodStart = DateTimeOffset.UtcNow.AddMonths(-6),
			PeriodEnd = DateTimeOffset.UtcNow,
			Opinion = AuditorOpinion.Unqualified,
			GeneratedAt = DateTimeOffset.UtcNow,
			CategoriesIncluded = [TrustServicesCategory.Security],
			System = CreateSystemDescription(),
			ControlSections = [
				new ControlSection
				{
					Criterion = TrustServicesCriterion.CC1_ControlEnvironment,
					Description = "Control environment",
					IsMet = true,
					Controls = [
						new ControlDescription
						{
							ControlId = "CC1.1",
							Name = "Control Environment",
							Description = "Control environment policy",
							Implementation = "Policy implemented",
							Type = ControlType.Preventive,
							Frequency = ControlFrequency.Continuous
						}
					]
				}
			]
		};
	}

	private static TrustServicesCriterion GetCriterionForCategory(TrustServicesCategory category) => category switch
	{
		TrustServicesCategory.Security => TrustServicesCriterion.CC1_ControlEnvironment,
		TrustServicesCategory.Availability => TrustServicesCriterion.A1_InfrastructureManagement,
		TrustServicesCategory.ProcessingIntegrity => TrustServicesCriterion.PI1_InputValidation,
		TrustServicesCategory.Confidentiality => TrustServicesCriterion.C1_DataClassification,
		TrustServicesCategory.Privacy => TrustServicesCriterion.P1_Notice,
		_ => TrustServicesCriterion.CC1_ControlEnvironment
	};

	#endregion
}
