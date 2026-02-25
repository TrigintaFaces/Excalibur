// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Encryption;

/// <summary>
/// Unit tests for <see cref="BulkDecryptionExportOptions"/> class.
/// </summary>
/// <remarks>
/// Per AD-255-3, these tests verify the export options configuration for bulk decryption.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Compliance")]
public sealed class ExportOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void HaveDefaultFormatOfJson()
	{
		// Arrange & Act
		using var stream = new MemoryStream();
		var options = new BulkDecryptionExportOptions { Destination = stream };

		// Assert
		options.Format.ShouldBe(DecryptionExportFormat.Json);
	}

	[Fact]
	public void HaveIncludeMetadataTrueByDefault()
	{
		// Arrange & Act
		using var stream = new MemoryStream();
		var options = new BulkDecryptionExportOptions { Destination = stream };

		// Assert
		options.IncludeMetadata.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultBatchSizeOf100()
	{
		// Arrange & Act
		using var stream = new MemoryStream();
		var options = new BulkDecryptionExportOptions { Destination = stream };

		// Assert
		options.BatchSize.ShouldBe(100);
	}

	[Fact]
	public void HaveContinueOnErrorFalseByDefault()
	{
		// Arrange & Act
		using var stream = new MemoryStream();
		var options = new BulkDecryptionExportOptions { Destination = stream };

		// Assert
		options.ContinueOnError.ShouldBeFalse();
	}

	[Fact]
	public void HaveNullContextByDefault()
	{
		// Arrange & Act
		using var stream = new MemoryStream();
		var options = new BulkDecryptionExportOptions { Destination = stream };

		// Assert
		options.Context.ShouldBeNull();
	}

	#endregion Default Value Tests

	#region Required Property Tests

	[Fact]
	public void RequireDestinationStream()
	{
		// Arrange
		using var stream = new MemoryStream();

		// Act
		var options = new BulkDecryptionExportOptions { Destination = stream };

		// Assert
		_ = options.Destination.ShouldNotBeNull();
		options.Destination.ShouldBe(stream);
	}

	[Fact]
	public void AcceptFileStream()
	{
		// Arrange
		var tempPath = Path.GetTempFileName();
		try
		{
			using var stream = new FileStream(tempPath, FileMode.Create);

			// Act
			var options = new BulkDecryptionExportOptions { Destination = stream };

			// Assert
			options.Destination.ShouldBe(stream);
			options.Destination.CanWrite.ShouldBeTrue();
		}
		finally
		{
			File.Delete(tempPath);
		}
	}

	#endregion Required Property Tests

	#region Format Configuration Tests

	[Theory]
	[InlineData(DecryptionExportFormat.Json)]
	[InlineData(DecryptionExportFormat.Csv)]
	[InlineData(DecryptionExportFormat.Plaintext)]
	public void AllowFormatConfiguration(DecryptionExportFormat format)
	{
		// Arrange
		using var stream = new MemoryStream();

		// Act
		var options = new BulkDecryptionExportOptions
		{
			Destination = stream,
			Format = format
		};

		// Assert
		options.Format.ShouldBe(format);
	}

	#endregion Format Configuration Tests

	#region Property Assignment Tests

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void AllowIncludeMetadataConfiguration(bool include)
	{
		// Arrange
		using var stream = new MemoryStream();

		// Act
		var options = new BulkDecryptionExportOptions
		{
			Destination = stream,
			IncludeMetadata = include
		};

		// Assert
		options.IncludeMetadata.ShouldBe(include);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(50)]
	[InlineData(100)]
	[InlineData(500)]
	[InlineData(1000)]
	public void AllowBatchSizeConfiguration(int batchSize)
	{
		// Arrange
		using var stream = new MemoryStream();

		// Act
		var options = new BulkDecryptionExportOptions
		{
			Destination = stream,
			BatchSize = batchSize
		};

		// Assert
		options.BatchSize.ShouldBe(batchSize);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void AllowContinueOnErrorConfiguration(bool continueOnError)
	{
		// Arrange
		using var stream = new MemoryStream();

		// Act
		var options = new BulkDecryptionExportOptions
		{
			Destination = stream,
			ContinueOnError = continueOnError
		};

		// Assert
		options.ContinueOnError.ShouldBe(continueOnError);
	}

	[Fact]
	public void AllowContextConfiguration()
	{
		// Arrange
		using var stream = new MemoryStream();
		var context = new EncryptionContext { TenantId = "tenant-1", Purpose = "export" };

		// Act
		var options = new BulkDecryptionExportOptions
		{
			Destination = stream,
			Context = context
		};

		// Assert
		_ = options.Context.ShouldNotBeNull();
		options.Context.TenantId.ShouldBe("tenant-1");
		options.Context.Purpose.ShouldBe("export");
	}

	#endregion Property Assignment Tests

	#region Semantic Tests

	[Fact]
	public void BeFullyConfigurable()
	{
		// Arrange
		using var stream = new MemoryStream();
		var context = new EncryptionContext { TenantId = "gdpr-export", Purpose = "compliance" };

		// Act
		var options = new BulkDecryptionExportOptions
		{
			Destination = stream,
			Format = DecryptionExportFormat.Csv,
			IncludeMetadata = false,
			BatchSize = 250,
			ContinueOnError = true,
			Context = context
		};

		// Assert
		options.Destination.ShouldBe(stream);
		options.Format.ShouldBe(DecryptionExportFormat.Csv);
		options.IncludeMetadata.ShouldBeFalse();
		options.BatchSize.ShouldBe(250);
		options.ContinueOnError.ShouldBeTrue();
		options.Context.ShouldBe(context);
	}

	[Fact]
	public void SupportGdprExportUseCase()
	{
		// Per AD-255-3: GDPR data portability requires structured export
		using var stream = new MemoryStream();

		var options = new BulkDecryptionExportOptions
		{
			Destination = stream,
			Format = DecryptionExportFormat.Json,
			IncludeMetadata = true
		};

		options.Format.ShouldBe(DecryptionExportFormat.Json);
		options.IncludeMetadata.ShouldBeTrue();
	}

	[Fact]
	public void SupportAuditExportUseCase()
	{
		// Per AD-255-3: Audit exports may need CSV for spreadsheet tools
		using var stream = new MemoryStream();

		var options = new BulkDecryptionExportOptions
		{
			Destination = stream,
			Format = DecryptionExportFormat.Csv,
			IncludeMetadata = true
		};

		options.Format.ShouldBe(DecryptionExportFormat.Csv);
	}

	#endregion Semantic Tests
}
