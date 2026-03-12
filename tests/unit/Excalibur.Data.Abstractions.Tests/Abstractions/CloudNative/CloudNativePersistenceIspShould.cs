// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Data.Abstractions.CloudNative;

namespace Excalibur.Data.Tests.Abstractions.CloudNative;

/// <summary>
/// ISP gate compliance and behavioral tests for the Sprint 612 cloud-native
/// persistence provider interface splits (A.1) and cloud-native event store
/// interface splits (A.2).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data")]
[Trait("Feature", "CloudNative")]
public sealed class CloudNativePersistenceIspShould : UnitTestBase
{
	#region A.1 -- ICloudNativePersistenceProvider ISP Gates

	[Fact]
	public void ICloudNativePersistenceProvider_HaveAtMostFiveMethods()
	{
		// ISP gate: <= 5 methods (excluding inherited)
		var methods = typeof(ICloudNativePersistenceProvider)
			.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
			.Where(m => !m.IsSpecialName) // exclude property getters/setters
			.ToArray();

		methods.Length.ShouldBeLessThanOrEqualTo(5,
			$"ICloudNativePersistenceProvider has {methods.Length} methods: {string.Join(", ", methods.Select(m => m.Name))}");
	}

	[Fact]
	public void ICloudNativeProviderInfo_HaveExactlyOneProperty()
	{
		var props = typeof(ICloudNativeProviderInfo).GetProperties(BindingFlags.Public | BindingFlags.Instance);

		props.Length.ShouldBe(1);
		props[0].Name.ShouldBe("CloudProvider");
		props[0].PropertyType.ShouldBe(typeof(CloudProviderType));
	}

	[Fact]
	public void ICloudNativePersistenceQueryOperations_HaveExactlyOneMethod()
	{
		var methods = typeof(ICloudNativePersistenceQueryOperations)
			.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
			.Where(m => !m.IsSpecialName)
			.ToArray();

		methods.Length.ShouldBe(1);
		methods[0].Name.ShouldBe("QueryAsync");
	}

	[Fact]
	public void ICloudNativePersistenceBatchOperations_HaveExactlyOneMethod()
	{
		var methods = typeof(ICloudNativePersistenceBatchOperations)
			.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
			.Where(m => !m.IsSpecialName)
			.ToArray();

		methods.Length.ShouldBe(1);
		methods[0].Name.ShouldBe("ExecuteBatchAsync");
	}

	[Fact]
	public void ICloudNativePersistenceChangeFeed_HaveAtMostFiveMembers()
	{
		var methods = typeof(ICloudNativePersistenceChangeFeed)
			.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
			.Where(m => !m.IsSpecialName)
			.ToArray();

		var props = typeof(ICloudNativePersistenceChangeFeed)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

		methods.Length.ShouldBe(1); // CreateChangeFeedSubscriptionAsync
		props.Length.ShouldBe(2);   // SupportsChangeFeed, SupportsMultiRegionWrites

		(methods.Length + props.Length).ShouldBeLessThanOrEqualTo(5);
	}

	#endregion

	#region A.2 -- ICloudNativeEventStore ISP Gates

	[Fact]
	public void ICloudNativeEventStore_HaveAtMostFiveMethods()
	{
		// 3 core methods + GetService default interface method = 4
		var methods = typeof(ICloudNativeEventStore)
			.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
			.Where(m => !m.IsSpecialName)
			.ToArray();

		methods.Length.ShouldBeLessThanOrEqualTo(5,
			$"ICloudNativeEventStore has {methods.Length} methods: {string.Join(", ", methods.Select(m => m.Name))}");
	}

	[Fact]
	public void ICloudNativeEventStoreChangeFeed_HaveExactlyOneMethod()
	{
		var methods = typeof(ICloudNativeEventStoreChangeFeed)
			.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
			.Where(m => !m.IsSpecialName)
			.ToArray();

		methods.Length.ShouldBe(1);
		methods[0].Name.ShouldBe("SubscribeToChangesAsync");
	}

	[Fact]
	public void ICloudNativeEventStoreInfo_HaveExactlyOneMethod()
	{
		var methods = typeof(ICloudNativeEventStoreInfo)
			.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
			.Where(m => !m.IsSpecialName)
			.ToArray();

		methods.Length.ShouldBe(1);
		methods[0].Name.ShouldBe("GetCurrentVersionAsync");
	}

	[Fact]
	public void ICloudNativeEventStore_ExposeGetServiceMethod()
	{
		var getService = typeof(ICloudNativeEventStore).GetMethod("GetService");

		getService.ShouldNotBeNull();
		getService!.GetParameters().Length.ShouldBe(1);
		getService.GetParameters()[0].ParameterType.ShouldBe(typeof(Type));
		getService.ReturnType.ShouldBe(typeof(object));
	}

	#endregion

	#region CloudProviderType Enum

	[Fact]
	public void CloudProviderType_HaveThreeProviders()
	{
		var values = Enum.GetValues<CloudProviderType>();
		values.Length.ShouldBe(3);
	}

	[Theory]
	[InlineData(CloudProviderType.CosmosDb, 0)]
	[InlineData(CloudProviderType.DynamoDb, 1)]
	[InlineData(CloudProviderType.Firestore, 2)]
	public void CloudProviderType_HaveCorrectUnderlyingValues(CloudProviderType provider, int expected)
	{
		((int)provider).ShouldBe(expected);
	}

	#endregion

	#region CloudBatchOperationType Enum

	[Fact]
	public void CloudBatchOperationType_HaveSixOperations()
	{
		var values = Enum.GetValues<CloudBatchOperationType>();
		values.Length.ShouldBe(6);
	}

	[Theory]
	[InlineData(CloudBatchOperationType.Create, 0)]
	[InlineData(CloudBatchOperationType.Replace, 1)]
	[InlineData(CloudBatchOperationType.Upsert, 2)]
	[InlineData(CloudBatchOperationType.Delete, 3)]
	[InlineData(CloudBatchOperationType.Patch, 4)]
	[InlineData(CloudBatchOperationType.Read, 5)]
	public void CloudBatchOperationType_HaveCorrectUnderlyingValues(CloudBatchOperationType op, int expected)
	{
		((int)op).ShouldBe(expected);
	}

	#endregion

	#region CloudOperationResult DTOs

	[Fact]
	public void CloudOperationResult_StoreAllProperties()
	{
		var result = new CloudOperationResult(
			success: true,
			statusCode: 200,
			requestCharge: 3.5,
			etag: "etag-1",
			sessionToken: "session-1",
			errorMessage: null);

		result.Success.ShouldBeTrue();
		result.StatusCode.ShouldBe(200);
		result.RequestCharge.ShouldBe(3.5);
		result.ETag.ShouldBe("etag-1");
		result.SessionToken.ShouldBe("session-1");
		result.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void CloudOperationResult_DetectConcurrencyConflict()
	{
		var result = new CloudOperationResult(false, 412, 1.0, errorMessage: "Conflict");

		result.IsConcurrencyConflict.ShouldBeTrue();
		result.IsNotFound.ShouldBeFalse();
	}

	[Fact]
	public void CloudOperationResult_DetectNotFound()
	{
		var result = new CloudOperationResult(false, 404, 1.0, errorMessage: "Not found");

		result.IsNotFound.ShouldBeTrue();
		result.IsConcurrencyConflict.ShouldBeFalse();
	}

	[Fact]
	public void CloudOperationResultGeneric_StoreDocument()
	{
		var result = new CloudOperationResult<string>(
			success: true,
			statusCode: 200,
			requestCharge: 2.0,
			document: "test-doc",
			etag: "etag-2");

		result.Document.ShouldBe("test-doc");
		result.Success.ShouldBeTrue();
		result.ETag.ShouldBe("etag-2");
	}

	[Fact]
	public void CloudOperationResultGeneric_AllowNullDocument()
	{
		var result = new CloudOperationResult<string>(
			success: true,
			statusCode: 204,
			requestCharge: 1.0);

		result.Document.ShouldBeNull();
	}

	#endregion

	#region CloudQueryResult

	[Fact]
	public void CloudQueryResult_StoreDocumentsAndMetadata()
	{
		var docs = new List<string> { "doc-1", "doc-2" };
		var result = new CloudQueryResult<string>(docs, 5.0, "cont-token", "session-1");

		result.Documents.Count.ShouldBe(2);
		result.RequestCharge.ShouldBe(5.0);
		result.ContinuationToken.ShouldBe("cont-token");
		result.SessionToken.ShouldBe("session-1");
		result.HasMoreResults.ShouldBeTrue();
	}

	[Fact]
	public void CloudQueryResult_HasMoreResults_FalseWhenNoContinuationToken()
	{
		var result = new CloudQueryResult<string>([], 1.0);

		result.HasMoreResults.ShouldBeFalse();
		result.ContinuationToken.ShouldBeNull();
	}

	#endregion

	#region CloudBatchResult

	[Fact]
	public void CloudBatchResult_StoreOperationResults()
	{
		var opResults = new List<CloudOperationResult>
		{
			new(true, 200, 1.0),
			new(true, 201, 2.0)
		};
		var result = new CloudBatchResult(true, 3.0, opResults, "session-1");

		result.Success.ShouldBeTrue();
		result.RequestCharge.ShouldBe(3.0);
		result.OperationResults.Count.ShouldBe(2);
		result.SessionToken.ShouldBe("session-1");
		result.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void CloudBatchResult_StoreFailureMessage()
	{
		var result = new CloudBatchResult(false, 1.0, [], errorMessage: "Batch failed");

		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldBe("Batch failed");
	}

	#endregion

	#region CloudStoredEvent Record

	[Fact]
	public void CloudStoredEvent_StoreAllRequiredAndOptionalFields()
	{
		var timestamp = DateTimeOffset.UtcNow;
		var evt = new CloudStoredEvent
		{
			EventId = "evt-1",
			AggregateId = "agg-1",
			AggregateType = "Order",
			EventType = "OrderCreated",
			EventData = [1, 2, 3],
			Metadata = [4, 5],
			Version = 5,
			Timestamp = timestamp,
			PartitionKeyValue = "Order:agg-1",
			ETag = "etag-1",
			DocumentId = "doc-1",
			IsDispatched = true
		};

		evt.EventId.ShouldBe("evt-1");
		evt.AggregateId.ShouldBe("agg-1");
		evt.AggregateType.ShouldBe("Order");
		evt.EventType.ShouldBe("OrderCreated");
		evt.EventData.ShouldBe(new byte[] { 1, 2, 3 });
		evt.Metadata.ShouldBe(new byte[] { 4, 5 });
		evt.Version.ShouldBe(5);
		evt.Timestamp.ShouldBe(timestamp);
		evt.PartitionKeyValue.ShouldBe("Order:agg-1");
		evt.ETag.ShouldBe("etag-1");
		evt.DocumentId.ShouldBe("doc-1");
		evt.IsDispatched.ShouldBeTrue();
	}

	[Fact]
	public void CloudStoredEvent_OptionalFieldsDefaultToNull()
	{
		var evt = new CloudStoredEvent
		{
			EventId = "evt-1",
			AggregateId = "agg-1",
			AggregateType = "Order",
			EventType = "OrderCreated",
			EventData = [1],
			Version = 1,
			Timestamp = DateTimeOffset.UtcNow,
			PartitionKeyValue = "Order:agg-1"
		};

		evt.Metadata.ShouldBeNull();
		evt.ETag.ShouldBeNull();
		evt.DocumentId.ShouldBeNull();
		evt.IsDispatched.ShouldBeFalse();
	}

	#endregion

	#region CloudEventLoadResult

	[Fact]
	public void CloudEventLoadResult_CurrentVersion_ReturnLastEventVersion()
	{
		var events = new List<CloudStoredEvent>
		{
			new()
			{
				EventId = "e1", AggregateId = "a1", AggregateType = "Order",
				EventType = "Created", EventData = [1], Version = 1,
				Timestamp = DateTimeOffset.UtcNow, PartitionKeyValue = "Order:a1"
			},
			new()
			{
				EventId = "e2", AggregateId = "a1", AggregateType = "Order",
				EventType = "Updated", EventData = [2], Version = 3,
				Timestamp = DateTimeOffset.UtcNow, PartitionKeyValue = "Order:a1"
			}
		};

		var result = new CloudEventLoadResult(events, 4.5, "session-1");

		result.Events.Count.ShouldBe(2);
		result.CurrentVersion.ShouldBe(3);
		result.RequestCharge.ShouldBe(4.5);
		result.SessionToken.ShouldBe("session-1");
	}

	[Fact]
	public void CloudEventLoadResult_CurrentVersion_ReturnNegativeOneWhenEmpty()
	{
		var result = new CloudEventLoadResult([], 0.5);

		result.CurrentVersion.ShouldBe(-1);
		result.Events.Count.ShouldBe(0);
	}

	#endregion

	#region CloudAppendResult Factory Methods

	[Fact]
	public void CloudAppendResult_CreateSuccess_ReturnSuccessfulResult()
	{
		var result = CloudAppendResult.CreateSuccess(10, 3.5, "session-1");

		result.Success.ShouldBeTrue();
		result.NextExpectedVersion.ShouldBe(10);
		result.RequestCharge.ShouldBe(3.5);
		result.SessionToken.ShouldBe("session-1");
		result.ErrorMessage.ShouldBeNull();
		result.IsConcurrencyConflict.ShouldBeFalse();
	}

	[Fact]
	public void CloudAppendResult_CreateConcurrencyConflict_ReturnConflictResult()
	{
		var result = CloudAppendResult.CreateConcurrencyConflict(
			expectedVersion: 5,
			actualVersion: 7,
			requestCharge: 1.0);

		result.Success.ShouldBeFalse();
		result.IsConcurrencyConflict.ShouldBeTrue();
		result.NextExpectedVersion.ShouldBe(7);
		result.RequestCharge.ShouldBe(1.0);
		result.ErrorMessage.ShouldContain("expected version 5");
		result.ErrorMessage.ShouldContain("current version is 7");
	}

	[Fact]
	public void CloudAppendResult_CreateFailure_ReturnFailedResult()
	{
		var result = CloudAppendResult.CreateFailure("Something went wrong", 0.5);

		result.Success.ShouldBeFalse();
		result.IsConcurrencyConflict.ShouldBeFalse();
		result.NextExpectedVersion.ShouldBe(-1);
		result.RequestCharge.ShouldBe(0.5);
		result.ErrorMessage.ShouldBe("Something went wrong");
	}

	#endregion
}
