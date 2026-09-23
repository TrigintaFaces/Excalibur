// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Cdc.MongoDB;
using Excalibur.Dispatch;

using MongoDB.Bson;

namespace Excalibur.Data.Tests.MongoDB.Cdc;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class MongoDbCdcPositionShould
{
	[Fact]
	public void HaveInvalidStartPosition()
	{
		MongoDbCdcPosition.Start.IsValid.ShouldBeFalse();
		MongoDbCdcPosition.Start.ResumeToken.ShouldBeNull();
	}

	[Fact]
	public void CreateWithResumeToken()
	{
		var token = new BsonDocument("_data", "test-token");
		var position = new MongoDbCdcPosition(token);

		position.IsValid.ShouldBeTrue();
		position.ResumeToken.ShouldBeSameAs(token);
	}

	[Fact]
	public void ReturnNullTokenStringForStart()
	{
		MongoDbCdcPosition.Start.TokenString.ShouldBeNull();
	}

	[Fact]
	public void ReturnJsonTokenString()
	{
		var token = new BsonDocument("_data", "test-token");
		var position = new MongoDbCdcPosition(token);

		position.TokenString.ShouldNotBeNullOrWhiteSpace();
		position.TokenString.ShouldContain("_data");
	}

	[Fact]
	public void ParseFromValidJsonString()
	{
		var tokenJson = "{ \"_data\" : \"test-value\" }";
		var position = MongoDbCdcPosition.FromString(tokenJson);

		position.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void ReturnStartForNullString()
	{
		var position = MongoDbCdcPosition.FromString(null);

		position.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void ReturnStartForEmptyString()
	{
		var position = MongoDbCdcPosition.FromString("");

		position.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void ReturnStartForInvalidJson()
	{
		var position = MongoDbCdcPosition.FromString("not-json");

		position.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void TryParseValidJsonString()
	{
		var tokenJson = "{ \"_data\" : \"test-value\" }";

		MongoDbCdcPosition.TryParse(tokenJson, out var result).ShouldBeTrue();
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void TryParseNullString()
	{
		MongoDbCdcPosition.TryParse(null, out var result).ShouldBeTrue();
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void TryParseInvalidJson()
	{
		MongoDbCdcPosition.TryParse("not-json", out var result).ShouldBeFalse();
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void SupportEqualityForBothNull()
	{
		var a = new MongoDbCdcPosition(null);
		var b = new MongoDbCdcPosition(null);

		(a == b).ShouldBeTrue();
	}

	[Fact]
	public void SupportInequalityForNullAndNonNull()
	{
		var a = new MongoDbCdcPosition(null);
		var b = new MongoDbCdcPosition(new BsonDocument("_data", "test"));

		(a != b).ShouldBeTrue();
	}

	[Fact]
	public void SupportEqualityForSameDocument()
	{
		var token = new BsonDocument("_data", "test");
		var a = new MongoDbCdcPosition(token);
		var b = new MongoDbCdcPosition(token);

		a.Equals(b).ShouldBeTrue();
	}

	[Fact]
	public void HaveConsistentHashCodeForEqual()
	{
		var token = new BsonDocument("_data", "test");
		var a = new MongoDbCdcPosition(token);
		var b = new MongoDbCdcPosition(token);

		a.GetHashCode().ShouldBe(b.GetHashCode());
	}

	[Fact]
	public void ReturnAConsistentHashCodeForNullToken()
	{
		// This arm used to pin the literal value 0, which is an implementation detail no caller can rely
		// on — the hash now also folds in the resume mode. What a caller DOES rely on is the
		// Equals/GetHashCode contract, so that is what is asserted: equal positions hash equally, and a
		// position that differs only in mode is free to hash differently.
		var a = new MongoDbCdcPosition(null);
		var b = new MongoDbCdcPosition(null);

		a.Equals(b).ShouldBeTrue();
		a.GetHashCode().ShouldBe(b.GetHashCode());
		a.GetHashCode().ShouldBe(MongoDbCdcPosition.Start.GetHashCode());
	}

	[Fact]
	public void ConvertToChangePosition()
	{
		var token = new BsonDocument("_data", "test-value");
		var position = new MongoDbCdcPosition(token);

		var changePosition = position.ToChangePosition();

		changePosition.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void ConvertStartToEmptyChangePosition()
	{
		var changePosition = MongoDbCdcPosition.Start.ToChangePosition();

		changePosition.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void ConvertFromNullChangePosition()
	{
		var position = MongoDbCdcPosition.FromChangePosition(null);

		position.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void ReturnStartStringForStartPosition()
	{
		MongoDbCdcPosition.Start.ToString().ShouldBe("<start>");
	}

	[Fact]
	public void ReturnTokenStringForValidPosition()
	{
		var token = new BsonDocument("_data", "test-value");
		var position = new MongoDbCdcPosition(token);

		position.ToString().ShouldNotBe("<start>");
	}

	[Fact]
	public void NotEqualToObjectOfDifferentType()
	{
		var position = new MongoDbCdcPosition(null);

		position.Equals("not-a-position").ShouldBeFalse();
	}

	[Fact]
	public void DefaultToResumeAfterModeForAnOrdinaryCheckpoint()
	{
		// CONTROL. The single-argument constructor is what every ordinary checkpoint uses; it must keep
		// meaning "reopen with resumeAfter".
		new MongoDbCdcPosition(new BsonDocument("_data", "ordinary")).ResumeMode
			.ShouldBe(MongoDbChangeStreamResumeMode.ResumeAfter);
		MongoDbCdcPosition.Start.ResumeMode.ShouldBe(MongoDbChangeStreamResumeMode.ResumeAfter);
	}

	[Fact]
	public void CarryTheResumeModeThroughSerializationAndBack()
	{
		// A checkpoint taken at an invalidation boundary is only usable as a startAfter, and a restarted
		// process reads it back as a string. A mode held only in memory would be lost exactly there.
		var original = new MongoDbCdcPosition(
			new BsonDocument("_data", "at-the-invalidation"),
			MongoDbChangeStreamResumeMode.StartAfter);

		var reread = MongoDbCdcPosition.FromString(original.TokenString);

		reread.ResumeMode.ShouldBe(MongoDbChangeStreamResumeMode.StartAfter);
		reread.ResumeToken.ShouldNotBeNull();
		reread.ResumeToken!["_data"].AsString.ShouldBe("at-the-invalidation");
		reread.ShouldBe(original);
	}

	[Fact]
	public void ReadABareTokenAsAnOrdinaryResumeAfterCheckpoint()
	{
		// LIVENESS for the envelope: a token written before the mode existed, and every ordinary
		// checkpoint written since, must still parse — as resumeAfter, unwrapped.
		var bare = MongoDbCdcPosition.FromString(new BsonDocument("_data", "plain").ToJson());

		bare.ResumeMode.ShouldBe(MongoDbChangeStreamResumeMode.ResumeAfter);
		bare.ResumeToken!["_data"].AsString.ShouldBe("plain");
	}

	[Fact]
	public void TreatTheSameTokenInDifferentModesAsDifferentPositions()
	{
		// The two open different streams — one lands before an invalidation, the other after it — so
		// collapsing them would let a startAfter checkpoint be mistaken for an ordinary one.
		var token = new BsonDocument("_data", "same");
		var resumeAfter = new MongoDbCdcPosition(token);
		var startAfter = new MongoDbCdcPosition(token, MongoDbChangeStreamResumeMode.StartAfter);

		resumeAfter.Equals(startAfter).ShouldBeFalse();
		(resumeAfter == startAfter).ShouldBeFalse();
		(resumeAfter != startAfter).ShouldBeTrue();
	}
}
