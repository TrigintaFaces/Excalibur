// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Cdc;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Unit tests for <see cref="CdcProcessingState"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
[Trait("Feature", "CDC")]
public sealed class CdcProcessingStateShould : UnitTestBase
{
	[Fact]
	public void HaveDefaultLastProcessedLsn_As10ByteArray()
	{
		// Arrange & Act
		var state = new CdcProcessingState();

		// Assert
		state.LastProcessedLsn.ShouldNotBeNull();
		state.LastProcessedLsn.Length.ShouldBe(10);
	}

	[Fact]
	public void HaveNullLastProcessedSequenceValue_ByDefault()
	{
		// Arrange & Act
		var state = new CdcProcessingState();

		// Assert
		state.LastProcessedSequenceValue.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultLastCommitTime()
	{
		// Arrange & Act
		var state = new CdcProcessingState();

		// Assert
		state.LastCommitTime.ShouldBe(default(DateTime));
	}

	[Fact]
	public void HaveDefaultProcessedAt()
	{
		// Arrange & Act
		var state = new CdcProcessingState();

		// Assert
		state.ProcessedAt.ShouldBe(default(DateTimeOffset));
	}

	[Fact]
	public void HaveEmptyDatabaseConnectionIdentifier_ByDefault()
	{
		// Arrange & Act
		var state = new CdcProcessingState();

		// Assert
		state.DatabaseConnectionIdentifier.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveEmptyDatabaseName_ByDefault()
	{
		// Arrange & Act
		var state = new CdcProcessingState();

		// Assert
		state.DatabaseName.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveEmptyTableName_ByDefault()
	{
		// Arrange & Act
		var state = new CdcProcessingState();

		// Assert
		state.TableName.ShouldBe(string.Empty);
	}

	[Fact]
	public void AllowSettingLastProcessedLsn()
	{
		// Arrange
		var lsn = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09 };

		// Act
		var state = new CdcProcessingState
		{
			LastProcessedLsn = lsn
		};

		// Assert
		state.LastProcessedLsn.ShouldBe(lsn);
	}

	[Fact]
	public void AllowSettingLastProcessedSequenceValue()
	{
		// Arrange
		var seqVal = new byte[] { 0x01, 0x02, 0x03 };

		// Act
		var state = new CdcProcessingState
		{
			LastProcessedSequenceValue = seqVal
		};

		// Assert
		state.LastProcessedSequenceValue.ShouldBe(seqVal);
	}

	[Fact]
	public void AllowSettingLastCommitTime()
	{
		// Arrange
		var commitTime = new DateTime(2025, 6, 15, 14, 30, 0, DateTimeKind.Utc);

		// Act
		var state = new CdcProcessingState
		{
			LastCommitTime = commitTime
		};

		// Assert
		state.LastCommitTime.ShouldBe(commitTime);
	}

	[Fact]
	public void AllowSettingProcessedAt()
	{
		// Arrange
		var processedAt = new DateTimeOffset(2025, 6, 15, 14, 30, 0, TimeSpan.Zero);

		// Act
		var state = new CdcProcessingState
		{
			ProcessedAt = processedAt
		};

		// Assert
		state.ProcessedAt.ShouldBe(processedAt);
	}

	[Fact]
	public void AllowSettingDatabaseConnectionIdentifier()
	{
		// Act
		var state = new CdcProcessingState
		{
			DatabaseConnectionIdentifier = "conn-001"
		};

		// Assert
		state.DatabaseConnectionIdentifier.ShouldBe("conn-001");
	}

	[Fact]
	public void AllowSettingDatabaseName()
	{
		// Act
		var state = new CdcProcessingState
		{
			DatabaseName = "TestDatabase"
		};

		// Assert
		state.DatabaseName.ShouldBe("TestDatabase");
	}

	[Fact]
	public void AllowSettingTableName()
	{
		// Act
		var state = new CdcProcessingState
		{
			TableName = "Users"
		};

		// Assert
		state.TableName.ShouldBe("Users");
	}

	[Fact]
	public void InitializeWithAllProperties()
	{
		// Arrange
		var lsn = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09 };
		var seqVal = new byte[] { 0x0A, 0x0B };
		var commitTime = new DateTime(2025, 6, 15, 14, 30, 0, DateTimeKind.Utc);
		var processedAt = new DateTimeOffset(2025, 6, 15, 14, 35, 0, TimeSpan.Zero);

		// Act
		var state = new CdcProcessingState
		{
			LastProcessedLsn = lsn,
			LastProcessedSequenceValue = seqVal,
			LastCommitTime = commitTime,
			ProcessedAt = processedAt,
			DatabaseConnectionIdentifier = "primary-conn",
			DatabaseName = "ProductionDb",
			TableName = "Orders"
		};

		// Assert
		state.LastProcessedLsn.ShouldBe(lsn);
		state.LastProcessedSequenceValue.ShouldBe(seqVal);
		state.LastCommitTime.ShouldBe(commitTime);
		state.ProcessedAt.ShouldBe(processedAt);
		state.DatabaseConnectionIdentifier.ShouldBe("primary-conn");
		state.DatabaseName.ShouldBe("ProductionDb");
		state.TableName.ShouldBe("Orders");
	}
}
