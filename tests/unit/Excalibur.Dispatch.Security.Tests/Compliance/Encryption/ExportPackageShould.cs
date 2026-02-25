// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Encryption;

/// <summary>
/// Unit tests for <see cref="ExportPackage"/> and related export types.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ExportPackageShould
{
	#region ExportPackage Tests

	[Fact]
	public void ExportPackage_InitializeWithRequiredProperties()
	{
		// Arrange
		var data = new byte[] { 1, 2, 3, 4, 5 };
		var exportedAt = DateTimeOffset.UtcNow;

		// Act
		var package = new ExportPackage
		{
			Version = 1,
			Format = EncryptedDataExportFormat.Json,
			Data = data,
			ItemCount = 10,
			ExportedAt = exportedAt,
			Checksum = "sha256:abc123"
		};

		// Assert
		package.Version.ShouldBe(1);
		package.Format.ShouldBe(EncryptedDataExportFormat.Json);
		package.Data.ShouldBe(data);
		package.ItemCount.ShouldBe(10);
		package.ExportedAt.ShouldBe(exportedAt);
		package.Checksum.ShouldBe("sha256:abc123");
		package.Metadata.ShouldBeNull();
	}

	[Fact]
	public void ExportPackage_SupportOptionalMetadata()
	{
		// Arrange
		var metadata = new Dictionary<string, string>
		{
			["exportedBy"] = "TestService",
			["environment"] = "Production"
		};

		// Act
		var package = new ExportPackage
		{
			Version = 2,
			Format = EncryptedDataExportFormat.Binary,
			Data = new byte[] { 1, 2, 3 },
			ItemCount = 5,
			ExportedAt = DateTimeOffset.UtcNow,
			Checksum = "md5:xyz789",
			Metadata = metadata
		};

		// Assert
		_ = package.Metadata.ShouldNotBeNull();
		package.Metadata["exportedBy"].ShouldBe("TestService");
		package.Metadata["environment"].ShouldBe("Production");
	}

	[Theory]
	[InlineData(EncryptedDataExportFormat.Json)]
	[InlineData(EncryptedDataExportFormat.Binary)]
	[InlineData(EncryptedDataExportFormat.Pkcs7)]
	[InlineData(EncryptedDataExportFormat.Jwe)]
	public void ExportPackage_SupportAllExportFormats(EncryptedDataExportFormat format)
	{
		// Act
		var package = new ExportPackage
		{
			Version = 1,
			Format = format,
			Data = new byte[] { 0 },
			ItemCount = 1,
			ExportedAt = DateTimeOffset.UtcNow,
			Checksum = "test"
		};

		// Assert
		package.Format.ShouldBe(format);
	}

	#endregion ExportPackage Tests

	#region EncryptedDataExportOptions Tests

	[Fact]
	public void ExportOptions_HaveCorrectDefaults()
	{
		// Act
		var options = EncryptedDataExportOptions.Default;

		// Assert
		options.IncludeKeyMetadata.ShouldBeFalse();
		options.Compress.ShouldBeFalse();
		options.MaxItemsPerPackage.ShouldBeNull();
		options.AdditionalMetadata.ShouldBeNull();
	}

	[Fact]
	public void ExportOptions_SupportCustomConfiguration()
	{
		// Arrange
		var additionalMetadata = new Dictionary<string, string>
		{
			["version"] = "1.0.0"
		};

		// Act
		var options = new EncryptedDataExportOptions
		{
			IncludeKeyMetadata = true,
			Compress = true,
			MaxItemsPerPackage = 1000,
			AdditionalMetadata = additionalMetadata
		};

		// Assert
		options.IncludeKeyMetadata.ShouldBeTrue();
		options.Compress.ShouldBeTrue();
		options.MaxItemsPerPackage.ShouldBe(1000);
		_ = options.AdditionalMetadata.ShouldNotBeNull();
		options.AdditionalMetadata["version"].ShouldBe("1.0.0");
	}

	#endregion EncryptedDataExportOptions Tests

	#region ComplianceReport Tests

	[Fact]
	public void ComplianceReport_CalculateCompliancePercentage()
	{
		// Act
		var report = new ComplianceReport
		{
			ReportId = "report-001",
			GeneratedAt = DateTimeOffset.UtcNow,
			TotalItems = 100,
			CompliantItems = 85,
			NonCompliantItems = 15
		};

		// Assert
		report.CompliancePercentage.ShouldBe(85.0);
	}

	[Fact]
	public void ComplianceReport_HandleZeroItems()
	{
		// Act
		var report = new ComplianceReport
		{
			ReportId = "report-002",
			GeneratedAt = DateTimeOffset.UtcNow,
			TotalItems = 0,
			CompliantItems = 0,
			NonCompliantItems = 0
		};

		// Assert
		report.CompliancePercentage.ShouldBe(100.0); // 100% if no items
	}

	[Fact]
	public void ComplianceReport_IncludeIssuesAndStatistics()
	{
		// Arrange
		var issues = new List<ComplianceIssue>
		{
			new()
			{
				Severity = ComplianceIssueSeverity.Warning,
				Code = "ALGO001",
				Description = "Algorithm is deprecated"
			}
		};

		var statistics = new Dictionary<string, object>
		{
			["deprecatedKeys"] = 5,
			["oldestEncryption"] = DateTimeOffset.UtcNow.AddDays(-365)
		};

		// Act
		var report = new ComplianceReport
		{
			ReportId = "report-003",
			GeneratedAt = DateTimeOffset.UtcNow,
			TotalItems = 50,
			CompliantItems = 45,
			NonCompliantItems = 5,
			Issues = issues,
			Statistics = statistics
		};

		// Assert
		_ = report.Issues.ShouldNotBeNull();
		report.Issues.Count.ShouldBe(1);
		report.Issues[0].Severity.ShouldBe(ComplianceIssueSeverity.Warning);
		_ = report.Statistics.ShouldNotBeNull();
		report.Statistics["deprecatedKeys"].ShouldBe(5);
	}

	#endregion ComplianceReport Tests

	#region ComplianceIssue Tests

	[Theory]
	[InlineData(ComplianceIssueSeverity.Info)]
	[InlineData(ComplianceIssueSeverity.Warning)]
	[InlineData(ComplianceIssueSeverity.Error)]
	[InlineData(ComplianceIssueSeverity.Critical)]
	public void ComplianceIssue_SupportAllSeverityLevels(ComplianceIssueSeverity severity)
	{
		// Act
		var issue = new ComplianceIssue
		{
			Severity = severity,
			Code = "TEST001",
			Description = "Test issue"
		};

		// Assert
		issue.Severity.ShouldBe(severity);
	}

	[Fact]
	public void ComplianceIssue_IncludeAffectedItemsAndRemediation()
	{
		// Arrange
		var affectedItems = new List<string> { "item-1", "item-2", "item-3" };

		// Act
		var issue = new ComplianceIssue
		{
			Severity = ComplianceIssueSeverity.Error,
			Code = "KEY001",
			Description = "Key rotation required",
			AffectedItems = affectedItems,
			Remediation = "Rotate keys using the key management API"
		};

		// Assert
		_ = issue.AffectedItems.ShouldNotBeNull();
		issue.AffectedItems.Count.ShouldBe(3);
		issue.Remediation.ShouldBe("Rotate keys using the key management API");
	}

	#endregion ComplianceIssue Tests

	#region ComplianceReportOptions Tests

	[Fact]
	public void ReportOptions_HaveCorrectDefaults()
	{
		// Act
		var options = new ComplianceReportOptions();

		// Assert
		options.Requirements.ShouldBeNull();
		options.IncludeDetails.ShouldBeTrue();
		options.IncludeRemediation.ShouldBeTrue();
		options.MaxIssuesPerCategory.ShouldBeNull();
	}

	[Fact]
	public void ReportOptions_SupportCustomConfiguration()
	{
		// Arrange
		var requirements = ComplianceRequirements.Fips;

		// Act
		var options = new ComplianceReportOptions
		{
			Requirements = requirements,
			IncludeDetails = false,
			IncludeRemediation = false,
			MaxIssuesPerCategory = 10
		};

		// Assert
		options.Requirements.ShouldBe(requirements);
		options.IncludeDetails.ShouldBeFalse();
		options.IncludeRemediation.ShouldBeFalse();
		options.MaxIssuesPerCategory.ShouldBe(10);
	}

	#endregion ComplianceReportOptions Tests

	#region KeyMetadataExport Tests

	[Fact]
	public void KeyMetadataExport_InitializeWithRequiredProperties()
	{
		// Arrange
		var keys = new List<KeyMetadata>
		{
			new()
			{
				KeyId = "key-001",
				Version = 1,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				CreatedAt = DateTimeOffset.UtcNow.AddMonths(-6),
				Status = KeyStatus.Active
			}
		};

		// Act
		var export = new KeyMetadataExport
		{
			ExportId = "export-001",
			ExportedAt = DateTimeOffset.UtcNow,
			Keys = keys,
			Checksum = "sha256:keyexport123"
		};

		// Assert
		export.ExportId.ShouldBe("export-001");
		export.Keys.Count.ShouldBe(1);
		export.Keys[0].KeyId.ShouldBe("key-001");
		export.Keys[0].Version.ShouldBe(1);
		export.Checksum.ShouldBe("sha256:keyexport123");
	}

	#endregion KeyMetadataExport Tests
}
