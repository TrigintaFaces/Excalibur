// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

/// <summary>
/// Depth tests for <see cref="ServerlessContextBase"/> covering ThrowIfDisposed,
/// concurrent properties, Platform, Logger, and additional disposal paths.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ServerlessContextBaseDepthShould : UnitTestBase
{
	#region ThrowIfDisposed

	[Fact]
	public void ThrowIfDisposed_AfterDispose_ThrowsObjectDisposedException()
	{
		// Arrange
		var context = new ConcreteServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);
		context.Dispose();

		// Act & Assert
		Should.Throw<ObjectDisposedException>(() => context.InvokeThrowIfDisposed());
	}

	[Fact]
	public void ThrowIfDisposed_BeforeDispose_DoesNotThrow()
	{
		// Arrange
		using var context = new ConcreteServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Act & Assert — should not throw
		context.InvokeThrowIfDisposed();
	}

	#endregion

	#region Platform Property

	[Theory]
	[InlineData(ServerlessPlatform.AwsLambda)]
	[InlineData(ServerlessPlatform.AzureFunctions)]
	[InlineData(ServerlessPlatform.GoogleCloudFunctions)]
	[InlineData(ServerlessPlatform.Unknown)]
	public void Platform_ReturnsConstructorValue(ServerlessPlatform platform)
	{
		// Act
		using var context = new ConcreteServerlessContext(platform, NullLogger.Instance);

		// Assert
		context.Platform.ShouldBe(platform);
	}

	#endregion

	#region Properties — ConcurrentDictionary Behavior

	[Fact]
	public void Properties_IsConcurrentDictionary()
	{
		// Arrange
		using var context = new ConcreteServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Assert
		context.Properties.ShouldBeOfType<ConcurrentDictionary<string, object>>();
	}

	[Fact]
	public void Properties_SupportsMultipleEntries()
	{
		// Arrange
		using var context = new ConcreteServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Act
		context.Properties["key1"] = "value1";
		context.Properties["key2"] = 42;
		context.Properties["key3"] = true;

		// Assert
		context.Properties.Count.ShouldBe(3);
		context.Properties["key1"].ShouldBe("value1");
		context.Properties["key2"].ShouldBe(42);
		context.Properties["key3"].ShouldBe(true);
	}

	[Fact]
	public void Properties_OverwritesExistingKey()
	{
		// Arrange
		using var context = new ConcreteServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);
		context.Properties["key"] = "original";

		// Act
		context.Properties["key"] = "updated";

		// Assert
		context.Properties["key"].ShouldBe("updated");
	}

	#endregion

	#region Logger

	[Fact]
	public void Logger_ReturnsProvidedInstance()
	{
		// Arrange
		var logger = NullLogger.Instance;
		using var context = new ConcreteServerlessContext(ServerlessPlatform.AwsLambda, logger);

		// Assert
		context.Logger.ShouldBeSameAs(logger);
	}

	#endregion

	#region PlatformContext — Null Guard

	[Fact]
	public void Constructor_WithNullPlatformContext_ThrowsArgumentNullException()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new ConcreteServerlessContext(null!, ServerlessPlatform.AwsLambda, NullLogger.Instance));
	}

	[Fact]
	public void Constructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new ConcreteServerlessContext(ServerlessPlatform.AwsLambda, null!));
	}

	#endregion

	#region GetService — Concrete Type

	[Fact]
	public void GetService_WithConcreteType_ReturnsSelf()
	{
		// Arrange
		using var context = new ConcreteServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Act
		var result = context.GetService(typeof(ConcreteServerlessContext));

		// Assert
		result.ShouldBeSameAs(context);
	}

	[Fact]
	public void GetService_WithServerlessContextBaseType_ReturnsSelf()
	{
		// Arrange
		using var context = new ConcreteServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Act
		var result = context.GetService(typeof(ServerlessContextBase));

		// Assert
		result.ShouldBeSameAs(context);
	}

	#endregion

	#region ElapsedTime — Monotonically Increasing

	[Fact]
	public void ElapsedTime_IncreasesOverTime()
	{
		// Arrange
		using var context = new ConcreteServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);
		var first = context.ElapsedTime;

		// Act — small busy wait
		Thread.SpinWait(100_000);
		var second = context.ElapsedTime;

		// Assert
		second.ShouldBeGreaterThanOrEqualTo(first);
	}

	#endregion

	#region Dispose — Idempotent

	[Fact]
	public void Dispose_CalledThreeTimes_DoesNotThrow()
	{
		// Arrange
		var context = new ConcreteServerlessContext(ServerlessPlatform.AwsLambda, NullLogger.Instance);

		// Act & Assert — should be safe to call multiple times
		context.Dispose();
		context.Dispose();
		context.Dispose();
	}

	#endregion

	#region Test Implementation

	/// <summary>
	/// Concrete implementation exposing protected members for testing.
	/// </summary>
	private sealed class ConcreteServerlessContext : ServerlessContextBase
	{
		public ConcreteServerlessContext(ServerlessPlatform platform, ILogger logger)
			: base(new object(), platform, logger)
		{
		}

		public ConcreteServerlessContext(object? platformContext, ServerlessPlatform platform, ILogger logger)
			: base(platformContext!, platform, logger)
		{
		}

		public override string RequestId => "test-request-id";
		public override string FunctionName => "test-function";
		public override string FunctionVersion => "1.0.0";
		public override string InvokedFunctionArn => "test:arn";
		public override int MemoryLimitInMB => 256;
		public override string LogGroupName => "/test/group";
		public override string LogStreamName => "test-stream";
		public override string CloudProvider => "TestCloud";
		public override string Region => "test-region";
		public override string AccountId => "test-account";
		public override DateTimeOffset ExecutionDeadline => DateTimeOffset.UtcNow.AddMinutes(5);

		public void InvokeThrowIfDisposed() => ThrowIfDisposed();
	}

	#endregion
}
