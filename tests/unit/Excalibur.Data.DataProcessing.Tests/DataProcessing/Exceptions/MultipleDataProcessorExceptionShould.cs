// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing;
using Excalibur.Data.DataProcessing.Exceptions;

namespace Excalibur.Data.Tests.DataProcessing.Exceptions;

/// <summary>
/// Unit tests for <see cref="MultipleDataProcessorException"/>.
/// </summary>
[UnitTest]
public sealed class MultipleDataProcessorExceptionShould : UnitTestBase
{
	[Fact]
	public void CreateWithRecordType_SetsRecordType()
	{
		// Arrange & Act — use named parameter to avoid matching (string message) overload
		var ex = new MultipleDataProcessorException(recordType: "OrderRecord");

		// Assert
		ex.RecordType.ShouldBe("OrderRecord");
		ex.Message.ShouldContain("OrderRecord");
		ex.Message.ShouldContain(nameof(IDataProcessor));
	}

	[Fact]
	public void CreateWithCustomStatusCode()
	{
		// Arrange & Act
		var ex = new MultipleDataProcessorException(recordType: "OrderRecord", statusCode: 409);

		// Assert
		ex.RecordType.ShouldBe("OrderRecord");
	}

	[Fact]
	public void CreateWithCustomMessage()
	{
		// Arrange & Act
		var ex = new MultipleDataProcessorException(recordType: "OrderRecord", message: "Custom error");

		// Assert
		ex.Message.ShouldBe("Custom error");
		ex.RecordType.ShouldBe("OrderRecord");
	}

	[Fact]
	public void CreateWithInnerException()
	{
		// Arrange
		var inner = new InvalidOperationException("inner");

		// Act
		var ex = new MultipleDataProcessorException(recordType: "OrderRecord", innerException: inner);

		// Assert
		ex.InnerException.ShouldBeSameAs(inner);
	}

	[Fact]
	public void Throw_WhenRecordType_IsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => new MultipleDataProcessorException(recordType: null!, statusCode: 500));
	}

	[Fact]
	public void Throw_WhenRecordType_IsWhitespace()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => new MultipleDataProcessorException(recordType: "   ", statusCode: 500));
	}

	[Fact]
	public void MessageContainsGuidance_AboutSingleRegistration()
	{
		// Arrange & Act — use named parameter to avoid matching (string message) overload
		var ex = new MultipleDataProcessorException(recordType: "OrderRecord");

		// Assert
		ex.Message.ShouldContain("only one handler");
	}

	[Fact]
	public void CreateDefaultInstance()
	{
		// Arrange & Act
		var ex = new MultipleDataProcessorException();

		// Assert
		ex.ShouldNotBeNull();
	}

	[Fact]
	public void CreateWithStringMessage()
	{
		// Arrange & Act
		var ex = new MultipleDataProcessorException("test message");

		// Assert
		ex.Message.ShouldBe("test message");
	}

	[Fact]
	public void CreateWithStringMessageAndInnerException()
	{
		// Arrange
		var inner = new InvalidOperationException("inner");

		// Act
		var ex = new MultipleDataProcessorException("test message", inner);

		// Assert
		ex.Message.ShouldBe("test message");
		ex.InnerException.ShouldBeSameAs(inner);
	}
}
