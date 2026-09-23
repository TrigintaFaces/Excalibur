// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;

using Excalibur.Cdc.Firestore;

namespace Excalibur.Data.Tests.Firestore;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
[Trait("Feature", "Firestore")]
public sealed class FirestoreCdcPositionTokenFormatShould
{
	[Fact]
	public void EmitTheSameTokenBytesItAlwaysHas()
	{
		// CHARACTERIZATION -- see the DynamoDb sibling for why. Note the PRESENT "timestamp":null: these
		// options do not omit nulls, unlike the DynamoDb position's. Sharing one serializer configuration
		// between the two would silently rewrite every token one of them has ever issued.
		var position = FirestoreCdcPosition.FromUpdateTime(
			"projects/p/databases/(default)/documents/orders",
			DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
			"doc-1");

		Encoding.UTF8.GetString(Convert.FromBase64String(position.ToBase64()))
			.ShouldBe(@"{""collectionPath"":""projects/p/databases/(default)/documents/orders"",""updateTime"":""2026-01-01T00:00:00+00:00"",""lastDocumentId"":""doc-1"",""timestamp"":null}");
	}

	[Fact]
	public void ReadBackATokenWrittenInThatFormat()
	{
		// Liveness, against a literal token rather than one this process just produced.
		var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(@"{""collectionPath"":""projects/p/databases/(default)/documents/orders"",""updateTime"":""2026-01-01T00:00:00+00:00"",""lastDocumentId"":""doc-1"",""timestamp"":null}"));

		var parsed = FirestoreCdcPosition.FromBase64(token);

		parsed.CollectionPath.ShouldBe("projects/p/databases/(default)/documents/orders");
		parsed.LastDocumentId.ShouldBe("doc-1");
	}
}
