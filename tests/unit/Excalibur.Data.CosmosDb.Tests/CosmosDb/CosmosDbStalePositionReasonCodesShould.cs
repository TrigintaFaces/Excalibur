// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CosmosDb.Cdc;

namespace Excalibur.Data.Tests.CosmosDb;

/// <summary>
/// Unit tests for the <see cref="CosmosDbStalePositionReasonCodes"/> class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.3): CosmosDB unit tests.
/// Tests verify reason code constants and parsing methods.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "CosmosDb")]
[Trait("Feature", "CDC")]
public sealed class CosmosDbStalePositionReasonCodesShould
{
	#region Constant Value Tests

	[Fact]
	public void ContinuationTokenExpired_HasCorrectValue()
	{
		// Assert
		CosmosDbStalePositionReasonCodes.ContinuationTokenExpired.ShouldBe("COSMOSDB_CONTINUATION_TOKEN_EXPIRED");
	}

	[Fact]
	public void PartitionNotFound_HasCorrectValue()
	{
		// Assert
		CosmosDbStalePositionReasonCodes.PartitionNotFound.ShouldBe("COSMOSDB_PARTITION_NOT_FOUND");
	}

	[Fact]
	public void ContainerDeleted_HasCorrectValue()
	{
		// Assert
		CosmosDbStalePositionReasonCodes.ContainerDeleted.ShouldBe("COSMOSDB_CONTAINER_DELETED");
	}

	[Fact]
	public void ETagMismatch_HasCorrectValue()
	{
		// Assert
		CosmosDbStalePositionReasonCodes.ETagMismatch.ShouldBe("COSMOSDB_ETAG_MISMATCH");
	}

	[Fact]
	public void PartitionSplit_HasCorrectValue()
	{
		// Assert
		CosmosDbStalePositionReasonCodes.PartitionSplit.ShouldBe("COSMOSDB_PARTITION_SPLIT");
	}

	[Fact]
	public void ThroughputChange_HasCorrectValue()
	{
		// Assert
		CosmosDbStalePositionReasonCodes.ThroughputChange.ShouldBe("COSMOSDB_THROUGHPUT_CHANGE");
	}

	[Fact]
	public void Unknown_HasCorrectValue()
	{
		// Assert
		CosmosDbStalePositionReasonCodes.Unknown.ShouldBe("COSMOSDB_UNKNOWN");
	}

	#endregion

	#region FromStatusCode Tests

	[Fact]
	public void FromStatusCode_Returns_ContinuationTokenExpired_For410()
	{
		// Act
		var result = CosmosDbStalePositionReasonCodes.FromStatusCode(410);

		// Assert
		result.ShouldBe(CosmosDbStalePositionReasonCodes.ContinuationTokenExpired);
	}

	[Fact]
	public void FromStatusCode_Returns_PartitionNotFound_For404()
	{
		// Act
		var result = CosmosDbStalePositionReasonCodes.FromStatusCode(404);

		// Assert
		result.ShouldBe(CosmosDbStalePositionReasonCodes.PartitionNotFound);
	}

	[Fact]
	public void FromStatusCode_Returns_ETagMismatch_For412()
	{
		// Act
		var result = CosmosDbStalePositionReasonCodes.FromStatusCode(412);

		// Assert
		result.ShouldBe(CosmosDbStalePositionReasonCodes.ETagMismatch);
	}

	[Fact]
	public void FromStatusCode_Returns_Unknown_ForUnknownCode()
	{
		// Act
		var result = CosmosDbStalePositionReasonCodes.FromStatusCode(500);

		// Assert
		result.ShouldBe(CosmosDbStalePositionReasonCodes.Unknown);
	}

	#endregion

	#region FromErrorMessage Tests

	[Fact]
	public void FromErrorMessage_Returns_Unknown_ForNull()
	{
		// Act
		var result = CosmosDbStalePositionReasonCodes.FromErrorMessage(null);

		// Assert
		result.ShouldBe(CosmosDbStalePositionReasonCodes.Unknown);
	}

	[Fact]
	public void FromErrorMessage_Returns_Unknown_ForEmpty()
	{
		// Act
		var result = CosmosDbStalePositionReasonCodes.FromErrorMessage("");

		// Assert
		result.ShouldBe(CosmosDbStalePositionReasonCodes.Unknown);
	}

	[Fact]
	public void FromErrorMessage_Returns_Unknown_ForWhitespace()
	{
		// Act
		var result = CosmosDbStalePositionReasonCodes.FromErrorMessage("   ");

		// Assert
		result.ShouldBe(CosmosDbStalePositionReasonCodes.Unknown);
	}

	[Fact]
	public void FromErrorMessage_Detects_ContinuationTokenExpired()
	{
		// Act
		var result = CosmosDbStalePositionReasonCodes.FromErrorMessage("The continuation token has expired");

		// Assert
		result.ShouldBe(CosmosDbStalePositionReasonCodes.ContinuationTokenExpired);
	}

	[Fact]
	public void FromErrorMessage_Detects_ContinuationTokenInvalid()
	{
		// Act
		var result = CosmosDbStalePositionReasonCodes.FromErrorMessage("Continuation token is invalid");

		// Assert
		result.ShouldBe(CosmosDbStalePositionReasonCodes.ContinuationTokenExpired);
	}

	[Fact]
	public void FromErrorMessage_Detects_ContinuationTokenGone()
	{
		// Act
		var result = CosmosDbStalePositionReasonCodes.FromErrorMessage("Continuation is gone");

		// Assert
		result.ShouldBe(CosmosDbStalePositionReasonCodes.ContinuationTokenExpired);
	}

	[Fact]
	public void FromErrorMessage_Detects_PartitionNotFound()
	{
		// Act
		var result = CosmosDbStalePositionReasonCodes.FromErrorMessage("Partition not found");

		// Assert
		result.ShouldBe(CosmosDbStalePositionReasonCodes.PartitionNotFound);
	}

	[Fact]
	public void FromErrorMessage_Detects_PartitionSplit()
	{
		// Act
		var result = CosmosDbStalePositionReasonCodes.FromErrorMessage("Partition was split");

		// Assert
		result.ShouldBe(CosmosDbStalePositionReasonCodes.PartitionSplit);
	}

	[Fact]
	public void FromErrorMessage_Detects_ContainerDeleted()
	{
		// Act
		var result = CosmosDbStalePositionReasonCodes.FromErrorMessage("Container does not exist");

		// Assert
		result.ShouldBe(CosmosDbStalePositionReasonCodes.ContainerDeleted);
	}

	[Fact]
	public void FromErrorMessage_Detects_ContainerNotFound()
	{
		// Act
		var result = CosmosDbStalePositionReasonCodes.FromErrorMessage("Container not found");

		// Assert
		result.ShouldBe(CosmosDbStalePositionReasonCodes.ContainerDeleted);
	}

	[Fact]
	public void FromErrorMessage_Detects_ETagMismatch()
	{
		// Act
		var result = CosmosDbStalePositionReasonCodes.FromErrorMessage("ETag mismatch detected");

		// Assert
		result.ShouldBe(CosmosDbStalePositionReasonCodes.ETagMismatch);
	}

	[Fact]
	public void FromErrorMessage_Detects_PreconditionFailed()
	{
		// Act
		var result = CosmosDbStalePositionReasonCodes.FromErrorMessage("Precondition failed");

		// Assert
		result.ShouldBe(CosmosDbStalePositionReasonCodes.ETagMismatch);
	}

	[Fact]
	public void FromErrorMessage_Detects_ThroughputChange()
	{
		// Act
		var result = CosmosDbStalePositionReasonCodes.FromErrorMessage("Throughput was changed");

		// Assert
		result.ShouldBe(CosmosDbStalePositionReasonCodes.ThroughputChange);
	}

	[Fact]
	public void FromErrorMessage_Detects_RuChange()
	{
		// Act
		var result = CosmosDbStalePositionReasonCodes.FromErrorMessage("RU change detected");

		// Assert
		result.ShouldBe(CosmosDbStalePositionReasonCodes.ThroughputChange);
	}

	[Fact]
	public void FromErrorMessage_IsCaseInsensitive()
	{
		// Act
		var result = CosmosDbStalePositionReasonCodes.FromErrorMessage("CONTINUATION TOKEN EXPIRED");

		// Assert
		result.ShouldBe(CosmosDbStalePositionReasonCodes.ContinuationTokenExpired);
	}

	#endregion
}
