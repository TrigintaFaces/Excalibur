// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Patterns.ClaimCheck;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

/// <summary>
/// Unit tests for <see cref="ClaimCheckEnvelope"/> record.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Patterns")]
[Trait("Feature", "ClaimCheck")]
public sealed class ClaimCheckEnvelopeShould
{
	#region Property Default Value Tests

	[Fact]
	public void HaveNullReference_ByDefault()
	{
		// Arrange & Act
		var envelope = new ClaimCheckEnvelope();

		// Assert
		envelope.Reference.ShouldBeNull();
	}

	[Fact]
	public void HaveEmptyMessageType_ByDefault()
	{
		// Arrange & Act
		var envelope = new ClaimCheckEnvelope();

		// Assert
		envelope.MessageType.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveEmptySerializerName_ByDefault()
	{
		// Arrange & Act
		var envelope = new ClaimCheckEnvelope();

		// Assert
		envelope.SerializerName.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveZeroOriginalSize_ByDefault()
	{
		// Arrange & Act
		var envelope = new ClaimCheckEnvelope();

		// Assert
		envelope.OriginalSize.ShouldBe(0);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void AllowSettingReference()
	{
		// Arrange
		var reference = new ClaimCheckReference
		{
			Id = "claim-123",
			Location = "blob://container/claim-123",
		};
		var envelope = new ClaimCheckEnvelope();

		// Act
		envelope.Reference = reference;

		// Assert
		envelope.Reference.ShouldBe(reference);
		envelope.Reference.Id.ShouldBe("claim-123");
	}

	[Fact]
	public void AllowSettingMessageType()
	{
		// Arrange
		var envelope = new ClaimCheckEnvelope();

		// Act
		envelope.MessageType = "OrderCreatedEvent";

		// Assert
		envelope.MessageType.ShouldBe("OrderCreatedEvent");
	}

	[Fact]
	public void AllowSettingSerializerName()
	{
		// Arrange
		var envelope = new ClaimCheckEnvelope();

		// Act
		envelope.SerializerName = "Json-STJ";

		// Assert
		envelope.SerializerName.ShouldBe("Json-STJ");
	}

	[Fact]
	public void AllowSettingOriginalSize()
	{
		// Arrange
		var envelope = new ClaimCheckEnvelope();

		// Act
		envelope.OriginalSize = 1024 * 1024;

		// Assert
		envelope.OriginalSize.ShouldBe(1024 * 1024);
	}

	#endregion

	#region Initialization Tests

	[Fact]
	public void AllowCreatingWithAllProperties()
	{
		// Arrange
		var reference = new ClaimCheckReference
		{
			Id = "claim-456",
			Location = "s3://bucket/claim-456",
		};

		// Act
		var envelope = new ClaimCheckEnvelope
		{
			Reference = reference,
			MessageType = "PaymentProcessedEvent",
			SerializerName = "MessagePack",
			OriginalSize = 2048,
		};

		// Assert
		envelope.Reference.ShouldBe(reference);
		envelope.MessageType.ShouldBe("PaymentProcessedEvent");
		envelope.SerializerName.ShouldBe("MessagePack");
		envelope.OriginalSize.ShouldBe(2048);
	}

	#endregion

	#region Record Equality Tests

	[Fact]
	public void BeEqualToAnotherEnvelope_WhenAllPropertiesMatch()
	{
		// Arrange
		var reference = new ClaimCheckReference
		{
			Id = "claim-789",
			Location = "gcs://bucket/claim-789",
		};

		var envelope1 = new ClaimCheckEnvelope
		{
			Reference = reference,
			MessageType = "TestEvent",
			SerializerName = "Json",
			OriginalSize = 512,
		};

		var envelope2 = new ClaimCheckEnvelope
		{
			Reference = reference,
			MessageType = "TestEvent",
			SerializerName = "Json",
			OriginalSize = 512,
		};

		// Act & Assert
		envelope1.ShouldBe(envelope2);
	}

	[Fact]
	public void NotBeEqualToAnotherEnvelope_WhenMessageTypeDiffers()
	{
		// Arrange
		var envelope1 = new ClaimCheckEnvelope { MessageType = "TypeA" };
		var envelope2 = new ClaimCheckEnvelope { MessageType = "TypeB" };

		// Act & Assert
		envelope1.ShouldNotBe(envelope2);
	}

	[Fact]
	public void NotBeEqualToAnotherEnvelope_WhenSerializerNameDiffers()
	{
		// Arrange
		var envelope1 = new ClaimCheckEnvelope { SerializerName = "Json" };
		var envelope2 = new ClaimCheckEnvelope { SerializerName = "MessagePack" };

		// Act & Assert
		envelope1.ShouldNotBe(envelope2);
	}

	[Fact]
	public void NotBeEqualToAnotherEnvelope_WhenOriginalSizeDiffers()
	{
		// Arrange
		var envelope1 = new ClaimCheckEnvelope { OriginalSize = 100 };
		var envelope2 = new ClaimCheckEnvelope { OriginalSize = 200 };

		// Act & Assert
		envelope1.ShouldNotBe(envelope2);
	}

	#endregion

	#region Record With-Expression Tests

	[Fact]
	public void SupportWithExpression_ForModifyingReference()
	{
		// Arrange
		var originalRef = new ClaimCheckReference { Id = "original" };
		var newRef = new ClaimCheckReference { Id = "new" };
		var original = new ClaimCheckEnvelope { Reference = originalRef };

		// Act
		var modified = original with { Reference = newRef };

		// Assert
		modified.Reference.ShouldBe(newRef);
		original.Reference.ShouldBe(originalRef); // Original unchanged
	}

	[Fact]
	public void SupportWithExpression_ForModifyingOriginalSize()
	{
		// Arrange
		var original = new ClaimCheckEnvelope { OriginalSize = 100 };

		// Act
		var modified = original with { OriginalSize = 500 };

		// Assert
		modified.OriginalSize.ShouldBe(500);
		original.OriginalSize.ShouldBe(100); // Original unchanged
	}

	#endregion
}
