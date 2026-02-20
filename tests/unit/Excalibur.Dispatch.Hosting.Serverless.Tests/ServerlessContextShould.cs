// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

/// <summary>
/// Unit tests for <see cref="ServerlessContext"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ServerlessContextShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void Constructor_WithNullEnvelope_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => new ServerlessContext(null!, NullLogger.Instance));
	}

	[Fact]
	public void Constructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange
		var envelope = CreateMinimalEnvelope();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => new ServerlessContext(envelope, null!));
	}

	[Fact]
	public void Constructor_WithValidEnvelope_CreatesInstance()
	{
		// Arrange
		var envelope = CreateMinimalEnvelope();

		// Act
		using var context = new ServerlessContext(envelope, NullLogger.Instance);

		// Assert
		context.ShouldNotBeNull();
	}

	#endregion

	#region Property Mapping Tests

	[Fact]
	public void RequestId_UsesEnvelopeRequestId_WhenProvided()
	{
		// Arrange
		var envelope = CreateMinimalEnvelope();
		envelope.RequestId = "test-request-123";

		// Act
		using var context = new ServerlessContext(envelope, NullLogger.Instance);

		// Assert
		context.RequestId.ShouldBe("test-request-123");
	}

	[Fact]
	public void RequestId_GeneratesNewGuid_WhenEnvelopeRequestIdIsNull()
	{
		// Arrange
		var envelope = CreateMinimalEnvelope();
		envelope.RequestId = null;

		// Act
		using var context = new ServerlessContext(envelope, NullLogger.Instance);

		// Assert
		context.RequestId.ShouldNotBeNullOrEmpty();
		Guid.TryParse(context.RequestId, out _).ShouldBeTrue();
	}

	[Fact]
	public void FunctionName_UsesEnvelopeFunctionName_WhenProvided()
	{
		// Arrange
		var envelope = CreateMinimalEnvelope();
		envelope.FunctionName = "my-function";

		// Act
		using var context = new ServerlessContext(envelope, NullLogger.Instance);

		// Assert
		context.FunctionName.ShouldBe("my-function");
	}

	[Fact]
	public void FunctionName_DefaultsToUnknown_WhenNull()
	{
		// Arrange
		var envelope = CreateMinimalEnvelope();
		envelope.FunctionName = null;

		// Act
		using var context = new ServerlessContext(envelope, NullLogger.Instance);

		// Assert
		context.FunctionName.ShouldBe("unknown");
	}

	[Fact]
	public void FunctionVersion_UsesEnvelopeFunctionVersion_WhenProvided()
	{
		// Arrange
		var envelope = CreateMinimalEnvelope();
		envelope.FunctionVersion = "2.0";

		// Act
		using var context = new ServerlessContext(envelope, NullLogger.Instance);

		// Assert
		context.FunctionVersion.ShouldBe("2.0");
	}

	[Fact]
	public void FunctionVersion_DefaultsTo1Point0_WhenNull()
	{
		// Arrange
		var envelope = CreateMinimalEnvelope();
		envelope.FunctionVersion = null;

		// Act
		using var context = new ServerlessContext(envelope, NullLogger.Instance);

		// Assert
		context.FunctionVersion.ShouldBe("1.0");
	}

	[Fact]
	public void InvokedFunctionArn_UsesEnvelopeSource_WhenProvided()
	{
		// Arrange
		var envelope = CreateMinimalEnvelope();
		envelope.Source = "arn:aws:lambda:us-east-1:123456789:function:my-function";

		// Act
		using var context = new ServerlessContext(envelope, NullLogger.Instance);

		// Assert
		context.InvokedFunctionArn.ShouldBe("arn:aws:lambda:us-east-1:123456789:function:my-function");
	}

	[Fact]
	public void InvokedFunctionArn_DefaultsToEmpty_WhenSourceIsNull()
	{
		// Arrange
		var envelope = CreateMinimalEnvelope();
		envelope.Source = null;

		// Act
		using var context = new ServerlessContext(envelope, NullLogger.Instance);

		// Assert
		context.InvokedFunctionArn.ShouldBe(string.Empty);
	}

	[Fact]
	public void CloudProvider_UsesEnvelopeCloudProvider_WhenProvided()
	{
		// Arrange
		var envelope = CreateMinimalEnvelope();
		envelope.CloudProvider = "AWS";

		// Act
		using var context = new ServerlessContext(envelope, NullLogger.Instance);

		// Assert
		context.CloudProvider.ShouldBe("AWS");
	}

	[Fact]
	public void CloudProvider_DefaultsToUnknown_WhenNull()
	{
		// Arrange
		var envelope = CreateMinimalEnvelope();
		envelope.CloudProvider = null;

		// Act
		using var context = new ServerlessContext(envelope, NullLogger.Instance);

		// Assert
		context.CloudProvider.ShouldBe("unknown");
	}

	[Fact]
	public void Region_UsesEnvelopeRegion_WhenProvided()
	{
		// Arrange
		var envelope = CreateMinimalEnvelope();
		envelope.Region = "us-west-2";

		// Act
		using var context = new ServerlessContext(envelope, NullLogger.Instance);

		// Assert
		context.Region.ShouldBe("us-west-2");
	}

	[Fact]
	public void Region_DefaultsToUnknown_WhenNull()
	{
		// Arrange
		var envelope = CreateMinimalEnvelope();
		envelope.Region = null;

		// Act
		using var context = new ServerlessContext(envelope, NullLogger.Instance);

		// Assert
		context.Region.ShouldBe("unknown");
	}

	#endregion

	#region Platform Detection Tests

	[Theory]
	[InlineData("AWS", ServerlessPlatform.AwsLambda)]
	[InlineData("AZURE", ServerlessPlatform.AzureFunctions)]
	[InlineData("GOOGLE", ServerlessPlatform.GoogleCloudFunctions)]
	[InlineData("aws", ServerlessPlatform.AwsLambda)]
	[InlineData("azure", ServerlessPlatform.AzureFunctions)]
	[InlineData("google", ServerlessPlatform.GoogleCloudFunctions)]
	public void DeterminePlatform_MapsCloudProviderCorrectly(string cloudProvider, ServerlessPlatform expected)
	{
		// Arrange
		var envelope = CreateMinimalEnvelope();
		envelope.CloudProvider = cloudProvider;

		// Act
		using var context = new ServerlessContext(envelope, NullLogger.Instance);

		// Assert
		context.Platform.ShouldBe(expected);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("UnknownCloud")]
	[InlineData("")]
	public void DeterminePlatform_ReturnsUnknown_ForUnrecognizedProviders(string? cloudProvider)
	{
		// Arrange
		var envelope = CreateMinimalEnvelope();
		envelope.CloudProvider = cloudProvider;

		// Act
		using var context = new ServerlessContext(envelope, NullLogger.Instance);

		// Assert
		context.Platform.ShouldBe(ServerlessPlatform.Unknown);
	}

	#endregion

	#region TraceContext Tests

	[Fact]
	public void TraceContext_SetsFromTraceParent_WhenProvided()
	{
		// Arrange
		var envelope = CreateMinimalEnvelope();
		envelope.TraceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

		// Act
		using var context = new ServerlessContext(envelope, NullLogger.Instance);

		// Assert
		context.TraceContext.ShouldNotBeNull();
		context.TraceContext.TraceParent.ShouldBe("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");
	}

	[Fact]
	public void TraceContext_IsNull_WhenTraceParentIsNull()
	{
		// Arrange
		var envelope = CreateMinimalEnvelope();
		envelope.TraceParent = null;

		// Act
		using var context = new ServerlessContext(envelope, NullLogger.Instance);

		// Assert
		context.TraceContext.ShouldBeNull();
	}

	[Fact]
	public void TraceContext_IsNull_WhenTraceParentIsEmpty()
	{
		// Arrange
		var envelope = CreateMinimalEnvelope();
		envelope.TraceParent = string.Empty;

		// Act
		using var context = new ServerlessContext(envelope, NullLogger.Instance);

		// Assert
		context.TraceContext.ShouldBeNull();
	}

	#endregion

	#region ExecutionDeadline Tests

	[Fact]
	public void ExecutionDeadline_DefaultsToFifteenMinutes_WhenNotInMetadata()
	{
		// Arrange
		var envelope = CreateMinimalEnvelope();
		var beforeCreation = DateTimeOffset.UtcNow.AddMinutes(14);

		// Act
		using var context = new ServerlessContext(envelope, NullLogger.Instance);

		// Assert — should be approximately 15 minutes from now
		context.ExecutionDeadline.ShouldBeGreaterThan(beforeCreation);
	}

	#endregion

	#region Inherited Behavior Tests

	[Fact]
	public void MemoryLimitInMB_DefaultsToZero_WhenNotInMetadata()
	{
		// Arrange
		var envelope = CreateMinimalEnvelope();

		// Act
		using var context = new ServerlessContext(envelope, NullLogger.Instance);

		// Assert
		context.MemoryLimitInMB.ShouldBe(0);
	}

	[Fact]
	public void LogGroupName_DefaultsToEmpty_WhenNotInMetadata()
	{
		// Arrange
		var envelope = CreateMinimalEnvelope();

		// Act
		using var context = new ServerlessContext(envelope, NullLogger.Instance);

		// Assert
		context.LogGroupName.ShouldBe(string.Empty);
	}

	[Fact]
	public void LogStreamName_DefaultsToEmpty_WhenNotInMetadata()
	{
		// Arrange
		var envelope = CreateMinimalEnvelope();

		// Act
		using var context = new ServerlessContext(envelope, NullLogger.Instance);

		// Assert
		context.LogStreamName.ShouldBe(string.Empty);
	}

	[Fact]
	public void AccountId_DefaultsToEmpty_WhenNotInMetadata()
	{
		// Arrange
		var envelope = CreateMinimalEnvelope();

		// Act
		using var context = new ServerlessContext(envelope, NullLogger.Instance);

		// Assert
		context.AccountId.ShouldBe(string.Empty);
	}

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		// Arrange
		var envelope = CreateMinimalEnvelope();
		var context = new ServerlessContext(envelope, NullLogger.Instance);

		// Act & Assert — no exception
		context.Dispose();
		context.Dispose();
	}

	#endregion

	#region Helpers

	private static MessageEnvelope CreateMinimalEnvelope()
	{
		return new MessageEnvelope
		{
			MessageId = Guid.NewGuid().ToString(),
			MessageType = "TestMessage",
		};
	}

	#endregion
}
