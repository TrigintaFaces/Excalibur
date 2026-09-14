// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Data.DynamoDb;

namespace Excalibur.Data.Tests.DynamoDb.Cdc;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
[Trait("Feature", "DynamoDb")]
public sealed class DynamoDbCdcPositionTokenFormatShould
{
	[Fact]
	public void EmitTheSameTokenBytesItAlwaysHas()
	{
		// CHARACTERIZATION. A resume token is persisted by consumers and read back after a restart, so its
		// wire format is a compatibility surface even though no consumer-facing type names it. Nothing else
		// in the suite pins it -- the sibling arms assert round-trip, which stays green through a format
		// change because the same code writes and reads it. This arm is what makes moving the serializer
		// off reflection a safe change rather than a hopeful one.
		//
		// Note the ABSENT "timestamp" key: these options omit nulls. The sibling Firestore position WRITES
		// timestamp:null, so the two formats are not interchangeable and their serializer configuration
		// must not be shared.
		var position = DynamoDbCdcPosition.FromShardPositions(
			"arn:aws:dynamodb:us-east-1:1:table/T/stream/2026-01-01T00:00:00.000",
			new Dictionary<string, string> { ["shard-1"] = "seq-100", ["shard-2"] = "seq-200" });

		Encoding.UTF8.GetString(Convert.FromBase64String(position.ToBase64()))
			.ShouldBe(@"{""streamArn"":""arn:aws:dynamodb:us-east-1:1:table/T/stream/2026-01-01T00:00:00.000"",""shardPositions"":{""shard-1"":""seq-100"",""shard-2"":""seq-200""}}");
	}

	[Fact]
	public void ReadBackATokenWrittenInThatFormat()
	{
		// Liveness. The arm above is satisfied by a writer that produces the right bytes and a reader that
		// cannot parse them; this one binds the other direction against a literal token rather than one
		// this process just produced.
		var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(@"{""streamArn"":""arn:aws:dynamodb:us-east-1:1:table/T/stream/2026-01-01T00:00:00.000"",""shardPositions"":{""shard-1"":""seq-100"",""shard-2"":""seq-200""}}"));

		var parsed = DynamoDbCdcPosition.FromBase64(token);

		parsed.StreamArn.ShouldBe("arn:aws:dynamodb:us-east-1:1:table/T/stream/2026-01-01T00:00:00.000");
		parsed.ShardPositions["shard-1"].ShouldBe("seq-100");
		parsed.ShardPositions["shard-2"].ShouldBe("seq-200");
	}
}
