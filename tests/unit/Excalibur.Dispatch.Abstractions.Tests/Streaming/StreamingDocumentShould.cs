// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Streaming;

namespace Excalibur.Dispatch.Abstractions.Tests.Streaming;

/// <summary>
/// Unit tests for the <see cref="StreamingDocument"/> abstract record.
/// Validates inheritance, IDispatchDocument implementation, and property behavior.
/// </summary>
/// <remarks>
/// Sprint 445 S445.4: Unit tests for streaming helper types.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Streaming")]
public sealed class StreamingDocumentShould : UnitTestBase
{
	#region Test Fixtures

	/// <summary>
	/// Concrete implementation for testing the abstract StreamingDocument.
	/// </summary>
	private sealed record TestStreamingDocument(
		string StreamId,
		long SequenceNumber) : StreamingDocument(StreamId, SequenceNumber)
	{
		public string? TestPayload { get; init; }
	}

	#endregion

	#region Constructor and Required Properties

	[Fact]
	public void StoreStreamIdCorrectly()
	{
		// Arrange & Act
		var document = new TestStreamingDocument("stream-123", 0);

		// Assert
		document.StreamId.ShouldBe("stream-123");
	}

	[Fact]
	public void StoreSequenceNumberCorrectly()
	{
		// Arrange & Act
		var document = new TestStreamingDocument("stream-123", 42);

		// Assert
		document.SequenceNumber.ShouldBe(42);
	}

	[Fact]
	public void SupportLargeSequenceNumbers()
	{
		// Arrange & Act
		var document = new TestStreamingDocument("stream-123", long.MaxValue);

		// Assert
		document.SequenceNumber.ShouldBe(long.MaxValue);
	}

	#endregion

	#region Optional Properties

	[Fact]
	public void HaveNullCorrelationIdByDefault()
	{
		// Arrange & Act
		var document = new TestStreamingDocument("stream-123", 0);

		// Assert
		document.CorrelationId.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingCorrelationId()
	{
		// Arrange & Act
		var document = new TestStreamingDocument("stream-123", 0)
		{
			CorrelationId = "correlation-456"
		};

		// Assert
		document.CorrelationId.ShouldBe("correlation-456");
	}

	[Fact]
	public void HaveDefaultTimestamp()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var document = new TestStreamingDocument("stream-123", 0);

		// Assert
		var after = DateTimeOffset.UtcNow;
		document.Timestamp.ShouldBeGreaterThanOrEqualTo(before);
		document.Timestamp.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void AllowSettingCustomTimestamp()
	{
		// Arrange
		var customTimestamp = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

		// Act
		var document = new TestStreamingDocument("stream-123", 0)
		{
			Timestamp = customTimestamp
		};

		// Assert
		document.Timestamp.ShouldBe(customTimestamp);
	}

	[Fact]
	public void HaveFalseIsEndOfStreamByDefault()
	{
		// Arrange & Act
		var document = new TestStreamingDocument("stream-123", 0);

		// Assert
		document.IsEndOfStream.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingIsEndOfStreamToTrue()
	{
		// Arrange & Act
		var document = new TestStreamingDocument("stream-123", 99)
		{
			IsEndOfStream = true
		};

		// Assert
		document.IsEndOfStream.ShouldBeTrue();
	}

	#endregion

	#region IDispatchDocument Implementation

	[Fact]
	public void ImplementIDispatchDocument()
	{
		// Arrange
		var document = new TestStreamingDocument("stream-123", 0);

		// Assert
		_ = document.ShouldBeAssignableTo<IDispatchDocument>();
	}

	#endregion

	#region Inheritance

	[Fact]
	public void SupportDerivedTypeProperties()
	{
		// Arrange & Act
		var document = new TestStreamingDocument("stream-123", 5)
		{
			TestPayload = "custom-data"
		};

		// Assert
		document.TestPayload.ShouldBe("custom-data");
	}

	[Fact]
	public void SupportWithExpressionOnDerivedType()
	{
		// Arrange
		var original = new TestStreamingDocument("stream-123", 0)
		{
			CorrelationId = "corr-1",
			TestPayload = "payload-1"
		};

		// Act
		var modified = original with
		{
			SequenceNumber = 5,
			IsEndOfStream = true
		};

		// Assert
		modified.StreamId.ShouldBe("stream-123");
		modified.SequenceNumber.ShouldBe(5);
		modified.CorrelationId.ShouldBe("corr-1");
		modified.TestPayload.ShouldBe("payload-1");
		modified.IsEndOfStream.ShouldBeTrue();
	}

	#endregion

	#region Record Equality

	[Fact]
	public void BeEqual_WhenAllPropertiesMatch()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;
		var doc1 = new TestStreamingDocument("stream-123", 5)
		{
			CorrelationId = "corr-1",
			Timestamp = timestamp,
			IsEndOfStream = false,
			TestPayload = "data"
		};
		var doc2 = new TestStreamingDocument("stream-123", 5)
		{
			CorrelationId = "corr-1",
			Timestamp = timestamp,
			IsEndOfStream = false,
			TestPayload = "data"
		};

		// Assert
		doc1.ShouldBe(doc2);
	}

	[Fact]
	public void NotBeEqual_WhenStreamIdDiffers()
	{
		// Arrange
		var doc1 = new TestStreamingDocument("stream-1", 0);
		var doc2 = new TestStreamingDocument("stream-2", 0);

		// Assert
		doc1.ShouldNotBe(doc2);
	}

	[Fact]
	public void NotBeEqual_WhenSequenceNumberDiffers()
	{
		// Arrange
		var doc1 = new TestStreamingDocument("stream-123", 0);
		var doc2 = new TestStreamingDocument("stream-123", 1);

		// Assert
		doc1.ShouldNotBe(doc2);
	}

	#endregion
}
