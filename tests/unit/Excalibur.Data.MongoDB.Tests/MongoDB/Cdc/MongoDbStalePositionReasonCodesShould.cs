// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MongoDB.Cdc;

namespace Excalibur.Data.Tests.MongoDB.Cdc;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MongoDbStalePositionReasonCodesShould
{
	[Fact]
	public void HaveCorrectConstants()
	{
		MongoDbStalePositionReasonCodes.ResumeTokenNotFound.ShouldBe("MONGODB_RESUME_TOKEN_NOT_FOUND");
		MongoDbStalePositionReasonCodes.InvalidResumeToken.ShouldBe("MONGODB_INVALID_RESUME_TOKEN");
		MongoDbStalePositionReasonCodes.CollectionDropped.ShouldBe("MONGODB_COLLECTION_DROPPED");
		MongoDbStalePositionReasonCodes.CollectionRenamed.ShouldBe("MONGODB_COLLECTION_RENAMED");
		MongoDbStalePositionReasonCodes.ShardMigration.ShouldBe("MONGODB_SHARD_MIGRATION");
		MongoDbStalePositionReasonCodes.StreamInvalidated.ShouldBe("MONGODB_STREAM_INVALIDATED");
		MongoDbStalePositionReasonCodes.Unknown.ShouldBe("MONGODB_UNKNOWN");
	}

	[Theory]
	[InlineData(136, "MONGODB_RESUME_TOKEN_NOT_FOUND")]
	[InlineData(286, "MONGODB_INVALID_RESUME_TOKEN")]
	[InlineData(26, "MONGODB_COLLECTION_DROPPED")]
	[InlineData(73, "MONGODB_COLLECTION_RENAMED")]
	[InlineData(133, "MONGODB_SHARD_MIGRATION")]
	[InlineData(999, "MONGODB_UNKNOWN")]
	public void MapErrorCodeToReasonCode(int errorCode, string expectedReason)
	{
		MongoDbStalePositionReasonCodes.FromErrorCode(errorCode).ShouldBe(expectedReason);
	}

	[Theory]
	[InlineData("Resume token not found in oplog", "MONGODB_RESUME_TOKEN_NOT_FOUND")]
	[InlineData("RESUMETOKEN is expired", "MONGODB_RESUME_TOKEN_NOT_FOUND")]
	[InlineData("Resume after failed", "MONGODB_RESUME_TOKEN_NOT_FOUND")]
	[InlineData("Oplog rollover detected", "MONGODB_RESUME_TOKEN_NOT_FOUND")]
	[InlineData("Change stream history lost", "MONGODB_RESUME_TOKEN_NOT_FOUND")]
	// "Invalid resume token format" matches "RESUME TOKEN" first, so maps to ResumeTokenNotFound
	[InlineData("Invalid resume token format", "MONGODB_RESUME_TOKEN_NOT_FOUND")]
	[InlineData("Invalid token checksum", "MONGODB_INVALID_RESUME_TOKEN")]
	[InlineData("Collection dropped by admin", "MONGODB_COLLECTION_DROPPED")]
	[InlineData("Collection does not exist", "MONGODB_COLLECTION_DROPPED")]
	[InlineData("Collection not found in namespace", "MONGODB_COLLECTION_DROPPED")]
	[InlineData("Collection renamed to new_name", "MONGODB_COLLECTION_RENAMED")]
	[InlineData("Namespace changed during migration", "MONGODB_COLLECTION_RENAMED")]
	[InlineData("Shard migration in progress", "MONGODB_SHARD_MIGRATION")]
	[InlineData("Shard stale version", "MONGODB_SHARD_MIGRATION")]
	[InlineData("Shard version mismatch", "MONGODB_SHARD_MIGRATION")]
	[InlineData("Invalidate event received", "MONGODB_STREAM_INVALIDATED")]
	[InlineData("Change stream is invalid", "MONGODB_STREAM_INVALIDATED")]
	[InlineData("Some random error", "MONGODB_UNKNOWN")]
	[InlineData(null, "MONGODB_UNKNOWN")]
	[InlineData("", "MONGODB_UNKNOWN")]
	[InlineData("   ", "MONGODB_UNKNOWN")]
	public void MapErrorMessageToReasonCode(string? errorMessage, string expectedReason)
	{
		MongoDbStalePositionReasonCodes.FromErrorMessage(errorMessage).ShouldBe(expectedReason);
	}
}
