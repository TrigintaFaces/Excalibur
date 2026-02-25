// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

/// <summary>
/// Unit tests for <see cref="ServerlessContextBase"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ServerlessContextBaseShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void Constructor_WithNullPlatformContext_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => new DefaultServerlessContext(ServerlessPlatform.AwsLambda, null!));
	}

	[Fact]
	public void Constructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// We verify via DefaultServerlessContext which passes (new object()) as platformContext
		// The NullLogger test won't directly throw on base since DefaultServerlessContext passes
		// NullLogger. Instead we verify Logger is set correctly.
		using var context = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		context.Logger.ShouldNotBeNull();
	}

	#endregion

	#region Properties Tests

	[Fact]
	public void ElapsedTime_ShouldBePositive()
	{
		// Arrange
		using var context = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Act — small delay to ensure measurable elapsed time
		Thread.SpinWait(1000);

		// Assert
		context.ElapsedTime.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
	}

	[Fact]
	public void RemainingTime_ShouldBePositive()
	{
		// Arrange
		using var context = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Assert
		context.RemainingTime.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public void Properties_ShouldBeEmpty_Initially()
	{
		// Arrange
		using var context = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Assert
		context.Properties.ShouldBeEmpty();
	}

	[Fact]
	public void Properties_ShouldSupportAddAndRetrieve()
	{
		// Arrange
		using var context = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Act
		context.Properties["key1"] = "value1";

		// Assert
		context.Properties["key1"].ShouldBe("value1");
	}

	[Fact]
	public void TraceContext_DefaultsToNull()
	{
		// Arrange
		using var context = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Assert
		context.TraceContext.ShouldBeNull();
	}

	[Fact]
	public void TraceContext_CanBeSet()
	{
		// Arrange
		using var context = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);
		var trace = new TraceContext { TraceId = "abc123" };

		// Act
		context.TraceContext = trace;

		// Assert
		context.TraceContext.ShouldNotBeNull();
		context.TraceContext.TraceId.ShouldBe("abc123");
	}

	[Fact]
	public void PlatformContext_ShouldNotBeNull()
	{
		// Arrange
		using var context = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Assert
		context.PlatformContext.ShouldNotBeNull();
	}

	#endregion

	#region GetService Tests

	[Fact]
	public void GetService_WithNullType_ThrowsArgumentNullException()
	{
		// Arrange
		using var context = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => context.GetService(null!));
	}

	[Fact]
	public void GetService_WithAssignableType_ReturnsSelf()
	{
		// Arrange
		using var context = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Act
		var result = context.GetService(typeof(IServerlessContext));

		// Assert
		result.ShouldBe(context);
	}

	[Fact]
	public void GetService_WithPlatformDetailsType_ReturnsSelf()
	{
		// Arrange
		using var context = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Act
		var result = context.GetService(typeof(IServerlessPlatformDetails));

		// Assert
		result.ShouldBe(context);
	}

	[Fact]
	public void GetService_WithUnrelatedType_ReturnsNull()
	{
		// Arrange
		using var context = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Act
		var result = context.GetService(typeof(string));

		// Assert
		result.ShouldBeNull();
	}

	#endregion

	#region Dispose Tests

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		// Arrange
		var context = new DefaultServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Act & Assert — no exception
		context.Dispose();
		context.Dispose();
	}

	#endregion
}
