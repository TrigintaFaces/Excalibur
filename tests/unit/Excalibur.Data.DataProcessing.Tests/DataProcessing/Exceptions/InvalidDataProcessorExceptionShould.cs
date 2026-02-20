// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing;
using Excalibur.Data.DataProcessing.Exceptions;

namespace Excalibur.Data.Tests.DataProcessing.Exceptions;

/// <summary>
/// Unit tests for <see cref="InvalidDataProcessorException"/>.
/// </summary>
[UnitTest]
public sealed class InvalidDataProcessorExceptionShould : UnitTestBase
{
	[Fact]
	public void HaveDefaultStatusCode_Of500()
	{
		InvalidDataProcessorException.DefaultStatusCode.ShouldBe(500);
	}

	[Fact]
	public void HaveDefaultMessage_ContainingIDataProcessor()
	{
		InvalidDataProcessorException.DefaultMessage.ShouldContain(nameof(IDataProcessor));
	}

	[Fact]
	public void CreateWithProcessorType_IncludesTypeName()
	{
		// Arrange & Act
		var ex = new InvalidDataProcessorException(typeof(string));

		// Assert
		ex.Message.ShouldContain("System.String");
	}

	[Fact]
	public void CreateWithoutProcessorType_UsesDefaultMessage()
	{
		// Arrange & Act
		var ex = new InvalidDataProcessorException();

		// Assert
		ex.ShouldNotBeNull();
	}

	[Fact]
	public void CreateWithCustomMessage_UsesCustomMessage()
	{
		// Arrange & Act
		var ex = new InvalidDataProcessorException(processorType: null, message: "Custom error");

		// Assert
		ex.Message.ShouldBe("Custom error");
	}

	[Fact]
	public void CreateWithInnerException_SetsInnerException()
	{
		// Arrange
		var inner = new InvalidOperationException("inner error");

		// Act
		var ex = new InvalidDataProcessorException(processorType: null, message: null, innerException: inner);

		// Assert
		ex.InnerException.ShouldBeSameAs(inner);
	}

	[Fact]
	public void CreateWithCustomStatusCode()
	{
		// Arrange & Act
		var ex = new InvalidDataProcessorException(statusCode: 400, processorType: typeof(string));

		// Assert
		ex.Message.ShouldContain("System.String");
	}

	[Fact]
	public void CreateWithStringMessage()
	{
		// Arrange & Act
		var ex = new InvalidDataProcessorException("test message");

		// Assert
		ex.Message.ShouldBe("test message");
	}

	[Fact]
	public void CreateWithStringMessageAndInnerException()
	{
		// Arrange
		var inner = new InvalidOperationException("inner");

		// Act
		var ex = new InvalidDataProcessorException("test message", inner);

		// Assert
		ex.Message.ShouldBe("test message");
		ex.InnerException.ShouldBeSameAs(inner);
	}
}
