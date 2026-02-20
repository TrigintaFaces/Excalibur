// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Data.DynamoDb.Outbox;
using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for the DynamoDbOutboxDocument class.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Tests verify outbox document constants and key creation.
/// Note: DynamoDbOutboxDocument is internal, so we use reflection to test it.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait("Feature", "Outbox")]
public sealed class DynamoDbOutboxDocumentShould
{
	private readonly Type _documentType;

	public DynamoDbOutboxDocumentShould()
	{
		// Get the internal type via reflection
		var assembly = typeof(DynamoDbOutboxOptions).Assembly;
		_documentType = assembly.GetType("Excalibur.Data.DynamoDb.Outbox.DynamoDbOutboxDocument")!;
	}

	#region Constant Value Tests

	[Fact]
	public void PK_Constant_Equals_PK()
	{
		// Arrange
		var field = _documentType.GetField("PK", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)field!.GetValue(null)!;

		// Assert
		value.ShouldBe("PK");
	}

	[Fact]
	public void SK_Constant_Equals_SK()
	{
		// Arrange
		var field = _documentType.GetField("SK", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)field!.GetValue(null)!;

		// Assert
		value.ShouldBe("SK");
	}

	[Fact]
	public void OutboxPrefix_Constant_Equals_OUTBOX_Hash()
	{
		// Arrange
		var field = _documentType.GetField("OutboxPrefix", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)field!.GetValue(null)!;

		// Assert
		value.ShouldBe("OUTBOX#");
	}

	[Fact]
	public void MessagePrefix_Constant_Equals_MSG_Hash()
	{
		// Arrange
		var field = _documentType.GetField("MessagePrefix", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)field!.GetValue(null)!;

		// Assert
		value.ShouldBe("MSG#");
	}

	[Fact]
	public void ScheduledPrefix_Constant_Equals_SCHEDULED()
	{
		// Arrange
		var field = _documentType.GetField("ScheduledPrefix", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)field!.GetValue(null)!;

		// Assert
		value.ShouldBe("SCHEDULED");
	}

	[Fact]
	public void GSI1PK_Constant_Equals_GSI1PK()
	{
		// Arrange
		var field = _documentType.GetField("GSI1PK", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)field!.GetValue(null)!;

		// Assert
		value.ShouldBe("GSI1PK");
	}

	[Fact]
	public void GSI2PK_Constant_Equals_GSI2PK()
	{
		// Arrange
		var field = _documentType.GetField("GSI2PK", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)field!.GetValue(null)!;

		// Assert
		value.ShouldBe("GSI2PK");
	}

	[Fact]
	public void MessageId_Constant_Equals_messageId()
	{
		// Arrange
		var field = _documentType.GetField("MessageId", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)field!.GetValue(null)!;

		// Assert
		value.ShouldBe("messageId");
	}

	[Fact]
	public void Status_Constant_Equals_status()
	{
		// Arrange
		var field = _documentType.GetField("Status", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)field!.GetValue(null)!;

		// Assert
		value.ShouldBe("status");
	}

	[Fact]
	public void Priority_Constant_Equals_priority()
	{
		// Arrange
		var field = _documentType.GetField("Priority", BindingFlags.Public | BindingFlags.Static);

		// Act
		var value = (string)field!.GetValue(null)!;

		// Assert
		value.ShouldBe("priority");
	}

	#endregion

	#region CreatePK Tests

	[Fact]
	public void CreatePK_ReturnsCorrectPartitionKey_ForStagedStatus()
	{
		// Arrange
		var method = _documentType.GetMethod("CreatePK", BindingFlags.Public | BindingFlags.Static);

		// Act
		var result = (string)method!.Invoke(null, new object[] { OutboxStatus.Staged })!;

		// Assert
		result.ShouldBe("OUTBOX#Staged");
	}

	[Fact]
	public void CreatePK_ReturnsCorrectPartitionKey_ForSendingStatus()
	{
		// Arrange
		var method = _documentType.GetMethod("CreatePK", BindingFlags.Public | BindingFlags.Static);

		// Act
		var result = (string)method!.Invoke(null, new object[] { OutboxStatus.Sending })!;

		// Assert
		result.ShouldBe("OUTBOX#Sending");
	}

	[Fact]
	public void CreatePK_ReturnsCorrectPartitionKey_ForSentStatus()
	{
		// Arrange
		var method = _documentType.GetMethod("CreatePK", BindingFlags.Public | BindingFlags.Static);

		// Act
		var result = (string)method!.Invoke(null, new object[] { OutboxStatus.Sent })!;

		// Assert
		result.ShouldBe("OUTBOX#Sent");
	}

	[Fact]
	public void CreatePK_ReturnsCorrectPartitionKey_ForFailedStatus()
	{
		// Arrange
		var method = _documentType.GetMethod("CreatePK", BindingFlags.Public | BindingFlags.Static);

		// Act
		var result = (string)method!.Invoke(null, new object[] { OutboxStatus.Failed })!;

		// Assert
		result.ShouldBe("OUTBOX#Failed");
	}

	#endregion

	#region CreateGSI1PK Tests

	[Fact]
	public void CreateGSI1PK_ReturnsCorrectKey()
	{
		// Arrange
		var method = _documentType.GetMethod("CreateGSI1PK", BindingFlags.Public | BindingFlags.Static);

		// Act
		var result = (string)method!.Invoke(null, new object[] { "msg-123" })!;

		// Assert
		result.ShouldBe("MSG#msg-123");
	}

	#endregion

	#region CreateSK Tests

	[Fact]
	public void CreateSK_ReturnsCorrectSortKey()
	{
		// Arrange
		var method = _documentType.GetMethod("CreateSK", BindingFlags.Public | BindingFlags.Static);
		var createdAt = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

		// Act
		var result = (string)method!.Invoke(null, new object[] { 5, createdAt, "msg-123" })!;

		// Assert
		result.ShouldContain("00005#"); // Priority with 5 digits
		result.ShouldContain("#msg-123"); // MessageId at end
	}

	[Fact]
	public void CreateSK_PadsPriority_ToFiveDigits()
	{
		// Arrange
		var method = _documentType.GetMethod("CreateSK", BindingFlags.Public | BindingFlags.Static);
		var createdAt = DateTimeOffset.UtcNow;

		// Act
		var result = (string)method!.Invoke(null, new object[] { 1, createdAt, "msg-123" })!;

		// Assert
		result.ShouldStartWith("00001#");
	}

	#endregion

	#region CreateGSI2SK Tests

	[Fact]
	public void CreateGSI2SK_ReturnsCorrectKey()
	{
		// Arrange
		var method = _documentType.GetMethod("CreateGSI2SK", BindingFlags.Public | BindingFlags.Static);
		var scheduledAt = new DateTimeOffset(2024, 6, 15, 14, 0, 0, TimeSpan.Zero);

		// Act
		var result = (string)method!.Invoke(null, new object[] { scheduledAt, "msg-456" })!;

		// Assert
		result.ShouldContain("#msg-456");
	}

	#endregion

	#region Type Tests

	[Fact]
	public void IsStatic()
	{
		// Assert
		_documentType.IsAbstract.ShouldBeTrue();
		_documentType.IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsInternal()
	{
		// Assert
		_documentType.IsNotPublic.ShouldBeTrue();
	}

	#endregion
}
