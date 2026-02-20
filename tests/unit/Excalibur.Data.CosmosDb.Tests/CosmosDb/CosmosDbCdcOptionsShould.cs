// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CosmosDb;
namespace Excalibur.Data.Tests.CosmosDb.Cdc;

/// <summary>
/// Unit tests for <see cref="CosmosDbCdcOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
[Trait("Feature", "CosmosDb")]
public sealed class CosmosDbCdcOptionsShould : UnitTestBase
{
	[Fact]
	public void HaveDefaultValues()
	{
		// Act
		var options = new CosmosDbCdcOptions();

		// Assert
		options.ConnectionString.ShouldBe(string.Empty);
		options.DatabaseId.ShouldBe(string.Empty);
		options.ContainerId.ShouldBe(string.Empty);
		options.ProcessorName.ShouldBe("cdc-processor");
		options.Mode.ShouldBe(CosmosDbCdcMode.LatestVersion);
		options.StartPosition.ShouldBeNull();
		options.MaxBatchSize.ShouldBe(100);
		options.PollInterval.ShouldBe(TimeSpan.FromSeconds(5));
		options.MaxWaitTime.ShouldBe(TimeSpan.FromSeconds(30));
		options.PartitionKeyPath.ShouldBeNull();
		options.PartitionKeyValues.ShouldBeNull();
		options.IncludeTimestamp.ShouldBeTrue();
		options.IncludeLsn.ShouldBeTrue();
	}

	[Fact]
	public void ValidateWithValidConfiguration()
	{
		// Arrange
		var options = new CosmosDbCdcOptions
		{
			ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=test==",
			DatabaseId = "TestDb",
			ContainerId = "TestContainer"
		};

		// Act & Assert - should not throw
		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void ThrowWhenConnectionStringMissing()
	{
		// Arrange
		var options = new CosmosDbCdcOptions
		{
			DatabaseId = "TestDb",
			ContainerId = "TestContainer"
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("ConnectionString");
	}

	[Fact]
	public void ThrowWhenDatabaseIdMissing()
	{
		// Arrange
		var options = new CosmosDbCdcOptions
		{
			ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=test==",
			ContainerId = "TestContainer"
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("DatabaseId");
	}

	[Fact]
	public void ThrowWhenContainerIdMissing()
	{
		// Arrange
		var options = new CosmosDbCdcOptions
		{
			ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=test==",
			DatabaseId = "TestDb"
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("ContainerId");
	}

	[Fact]
	public void ThrowWhenProcessorNameEmpty()
	{
		// Arrange
		var options = new CosmosDbCdcOptions
		{
			ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=test==",
			DatabaseId = "TestDb",
			ContainerId = "TestContainer",
			ProcessorName = ""
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("ProcessorName");
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(-100)]
	public void ThrowWhenMaxBatchSizeInvalid(int maxBatchSize)
	{
		// Arrange
		var options = new CosmosDbCdcOptions
		{
			ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=test==",
			DatabaseId = "TestDb",
			ContainerId = "TestContainer",
			MaxBatchSize = maxBatchSize
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("MaxBatchSize");
	}

	[Fact]
	public void ThrowWhenPollIntervalInvalid()
	{
		// Arrange
		var options = new CosmosDbCdcOptions
		{
			ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=test==",
			DatabaseId = "TestDb",
			ContainerId = "TestContainer",
			PollInterval = TimeSpan.Zero
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("PollInterval");
	}

	[Fact]
	public void ThrowWhenMaxWaitTimeInvalid()
	{
		// Arrange
		var options = new CosmosDbCdcOptions
		{
			ConnectionString = "AccountEndpoint=https://test.documents.azure.com:443/;AccountKey=test==",
			DatabaseId = "TestDb",
			ContainerId = "TestContainer",
			MaxWaitTime = TimeSpan.FromSeconds(-1)
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("MaxWaitTime");
	}

	[Fact]
	public void AllowCustomConfiguration()
	{
		// Arrange
		var startPosition = CosmosDbCdcPosition.Now();

		// Act
		var options = new CosmosDbCdcOptions
		{
			ConnectionString = "CustomConnection",
			DatabaseId = "CustomDb",
			ContainerId = "CustomContainer",
			ProcessorName = "custom-processor",
			Mode = CosmosDbCdcMode.AllVersionsAndDeletes,
			StartPosition = startPosition,
			MaxBatchSize = 500,
			PollInterval = TimeSpan.FromSeconds(10),
			MaxWaitTime = TimeSpan.FromMinutes(1),
			PartitionKeyPath = "/tenantId",
			PartitionKeyValues = ["tenant1", "tenant2"],
			IncludeTimestamp = false,
			IncludeLsn = false
		};

		// Assert
		options.ConnectionString.ShouldBe("CustomConnection");
		options.DatabaseId.ShouldBe("CustomDb");
		options.ContainerId.ShouldBe("CustomContainer");
		options.ProcessorName.ShouldBe("custom-processor");
		options.Mode.ShouldBe(CosmosDbCdcMode.AllVersionsAndDeletes);
		options.StartPosition.ShouldBe(startPosition);
		options.MaxBatchSize.ShouldBe(500);
		options.PollInterval.ShouldBe(TimeSpan.FromSeconds(10));
		options.MaxWaitTime.ShouldBe(TimeSpan.FromMinutes(1));
		options.PartitionKeyPath.ShouldBe("/tenantId");
		options.PartitionKeyValues.ShouldContain("tenant1");
		options.PartitionKeyValues.ShouldContain("tenant2");
		options.IncludeTimestamp.ShouldBeFalse();
		options.IncludeLsn.ShouldBeFalse();
	}
}
