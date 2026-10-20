// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;
using System.Text.Json;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

using Excalibur.Data.CloudNative;
using Excalibur.Outbox.DynamoDb;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.DynamoDb.Tests.Outbox;

/// <summary>Checks the closed cursor shape and the real outbox query caller without contacting AWS.</summary>
[Trait("Category", "Unit")]
[Trait("Component", "DynamoDb")]
[Trait("Pattern", "OUTBOX")]
public sealed class DynamoDbOutboxContinuationTokenShould
{
	[Fact]
	public void PreserveStringNumberAndTheEntireBinaryStreamWithoutChangingItsPosition()
	{
		using var binary = new MemoryStream([0, 1, 2, 254, 255]);
		binary.Position = 3;
		const string number = "12345678901234567890123456789012345678E-12";
		var key = new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
		{
			["pk"] = new() { S = "tenant-\"雪\\key" },
			["createdAt"] = new() { N = number },
			["sk"] = new() { B = binary },
		};
		using var parsed = JsonDocument.Parse(DynamoDbContinuationTokenWriter.Serialize(key)!);
		parsed.RootElement.EnumerateObject().Count().ShouldBe(3);
		parsed.RootElement.GetProperty("pk").GetProperty("S").GetString().ShouldBe(key["pk"].S);
		parsed.RootElement.GetProperty("createdAt").GetProperty("N").GetString().ShouldBe(number);
		parsed.RootElement.GetProperty("sk").GetProperty("B").GetBytesFromBase64().ShouldBe(binary.ToArray());
		parsed.RootElement.EnumerateObject().ShouldAllBe(p => p.Value.EnumerateObject().Count() == 1);
		binary.Position.ShouldBe(3);
	}

	[Fact]
	public void ReturnNoCursorForANullOrEmptyKey()
	{
		DynamoDbContinuationTokenWriter.Serialize(null).ShouldBeNull();
		DynamoDbContinuationTokenWriter.Serialize(new Dictionary<string, AttributeValue>()).ShouldBeNull();
	}

	[Fact]
	public void PreserveTheSameKeyValuesRegardlessOfDictionaryOrder()
	{
		var first = new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
		{
			["pk"] = new() { S = "partition" }, ["sk"] = new() { N = "1.2300E+4" },
		};
		var reversed = first.Reverse().ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
		using var firstDocument = JsonDocument.Parse(DynamoDbContinuationTokenWriter.Serialize(first)!);
		using var secondDocument = JsonDocument.Parse(DynamoDbContinuationTokenWriter.Serialize(reversed)!);
		foreach (var property in firstDocument.RootElement.EnumerateObject())
		{
			secondDocument.RootElement.GetProperty(property.Name).GetRawText().ShouldBe(property.Value.GetRawText());
		}
	}

	[Theory]
	[InlineData("missing")]
	[InlineData("null")]
	[InlineData("mixed")]
	[InlineData("bool-false")]
	[InlineData("null-false")]
	[InlineData("string-set")]
	[InlineData("number-set")]
	[InlineData("binary-set")]
	[InlineData("list")]
	[InlineData("map")]
	public void RefuseMissingAmbiguousOrUnsupportedKeyValues(string kind)
	{
		var value = kind switch
		{
			"missing" => new AttributeValue(),
			"null" => null,
			"mixed" => new AttributeValue { S = "string", N = "42" },
			"bool-false" => new AttributeValue { S = "string", BOOL = false },
			"null-false" => new AttributeValue { S = "string", NULL = false },
			"string-set" => new AttributeValue { S = "string", SS = [] },
			"number-set" => new AttributeValue { S = "string", NS = [] },
			"binary-set" => new AttributeValue { S = "string", BS = [] },
			"list" => new AttributeValue { S = "string", L = [] },
			"map" => new AttributeValue { S = "string", M = new Dictionary<string, AttributeValue>() },
			_ => throw new ArgumentException("Unknown test case.", nameof(kind)),
		};
		Should.Throw<InvalidOperationException>(() => DynamoDbContinuationTokenWriter.Serialize(
			new Dictionary<string, AttributeValue> { ["bad-key"] = value! })).Message.ShouldContain("bad-key");
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task ReturnTheSdkCursorEvenWhenTheFilteredPageHasNoItems(bool hasCursor)
	{
		var client = A.Fake<IAmazonDynamoDB>();
		var key = hasCursor ? new Dictionary<string, AttributeValue>
		{
			["pk"] = new() { S = "partition" }, ["createdAt"] = new() { N = "123.000" },
		} : null;
		A.CallTo(() => client.QueryAsync(A<QueryRequest>._, A<CancellationToken>._)).Returns(Task.FromResult(new QueryResponse
		{
			Items = [], LastEvaluatedKey = key, ConsumedCapacity = new ConsumedCapacity { ReadCapacityUnits = 2.5 },
		}));
		var options = new DynamoDbOutboxOptions { CreateTableIfNotExists = false };
		options.Connection.Region = "us-east-1";
		await using var store = new DynamoDbOutboxStore(Options.Create(options), NullLogger<DynamoDbOutboxStore>.Instance);
		SetField(store, "_client", client);
		SetField(store, "_initialized", true);
		var result = await store.GetPendingAsync(new PartitionKey("partition"), 5, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);
		result.Documents.ShouldBeEmpty();
		result.RequestCharge.ShouldBe(2.5);
		result.HasMoreResults.ShouldBe(hasCursor);
		if (hasCursor)
		{
			using var parsed = JsonDocument.Parse(result.ContinuationToken!);
			parsed.RootElement.GetProperty("pk").GetProperty("S").GetString().ShouldBe("partition");
			parsed.RootElement.GetProperty("createdAt").GetProperty("N").GetString().ShouldBe("123.000");
		}
		else { result.ContinuationToken.ShouldBeNull(); }
		A.CallTo(() => client.QueryAsync(A<QueryRequest>.That.Matches(request =>
			request.Limit == 5 && request.FilterExpression == "isPublished = :false"
			&& request.ExpressionAttributeValues[":pk"].S == "partition"), TestContext.Current.CancellationToken))
			.MustHaveHappenedOnceExactly();
	}

	private static void SetField(object store, string name, object value) =>
		(typeof(DynamoDbOutboxStore).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new MissingFieldException(typeof(DynamoDbOutboxStore).FullName, name)).SetValue(store, value);
}
