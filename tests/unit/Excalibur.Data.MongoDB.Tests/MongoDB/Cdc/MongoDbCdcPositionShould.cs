// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MongoDB.Cdc;
using Excalibur.Dispatch.Abstractions;

using MongoDB.Bson;

namespace Excalibur.Data.Tests.MongoDB.Cdc;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
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
	public void ReturnZeroHashCodeForNullToken()
	{
		var position = new MongoDbCdcPosition(null);

		position.GetHashCode().ShouldBe(0);
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
}
