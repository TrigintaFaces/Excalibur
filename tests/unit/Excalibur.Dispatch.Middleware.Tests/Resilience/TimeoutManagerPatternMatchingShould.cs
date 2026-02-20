// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Additional pattern matching tests for <see cref="TimeoutManager"/>.
/// Covers all branches in GetTimeoutByPattern.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class TimeoutManagerPatternMatchingShould : UnitTestBase
{
	private readonly TimeoutManager _manager;

	public TimeoutManagerPatternMatchingShould()
	{
		var options = MsOptions.Create(new TimeoutManagerOptions
		{
			DefaultTimeout = TimeSpan.FromSeconds(30),
			DatabaseTimeout = TimeSpan.FromSeconds(10),
			HttpTimeout = TimeSpan.FromSeconds(15),
			MessageQueueTimeout = TimeSpan.FromSeconds(20),
			CacheTimeout = TimeSpan.FromSeconds(5),
		});
		_manager = new TimeoutManager(options, A.Fake<ILogger<TimeoutManager>>());
	}

	#region Database patterns

	[Theory]
	[InlineData("Database.Insert")]
	[InlineData("Database.Update")]
	[InlineData("ExecuteSqlStatement")]
	[InlineData("RunQueryBatch")]
	public void GetTimeout_WithDatabasePattern_ReturnsDatabaseTimeout(string operationName)
	{
		_manager.GetTimeout(operationName).ShouldBe(TimeSpan.FromSeconds(10));
	}

	#endregion

	#region HTTP patterns

	[Theory]
	[InlineData("Http.Patch")]
	[InlineData("CallExternalApi")]
	[InlineData("InvokeRestEndpoint")]
	public void GetTimeout_WithHttpPattern_ReturnsHttpTimeout(string operationName)
	{
		_manager.GetTimeout(operationName).ShouldBe(TimeSpan.FromSeconds(15));
	}

	#endregion

	#region MessageQueue patterns

	[Theory]
	[InlineData("Queue.Acknowledge")]
	[InlineData("PublishMessage")]
	[InlineData("ServiceBusDispatch")]
	public void GetTimeout_WithMessagePattern_ReturnsMessageQueueTimeout(string operationName)
	{
		_manager.GetTimeout(operationName).ShouldBe(TimeSpan.FromSeconds(20));
	}

	#endregion

	#region Cache patterns

	[Theory]
	[InlineData("Cache.Invalidate")]
	[InlineData("RedisClusterLookup")]
	[InlineData("InMemoryCache")]
	public void GetTimeout_WithCachePattern_ReturnsCacheTimeout(string operationName)
	{
		_manager.GetTimeout(operationName).ShouldBe(TimeSpan.FromSeconds(5));
	}

	#endregion

	#region Default fallback

	[Theory]
	[InlineData("FileUpload")]
	[InlineData("BackgroundJob")]
	[InlineData("EmailSend")]
	public void GetTimeout_WithUnmatchedPattern_ReturnsDefaultTimeout(string operationName)
	{
		_manager.GetTimeout(operationName).ShouldBe(TimeSpan.FromSeconds(30));
	}

	#endregion

	#region Custom timeout preconfigured via options

	[Fact]
	public void GetTimeout_WithPreconfiguredOperationTimeouts_UsesConfiguredValue()
	{
		// Arrange
		var options = MsOptions.Create(new TimeoutManagerOptions());
		options.Value.OperationTimeouts["Custom.Op"] = TimeSpan.FromSeconds(99);
		var manager = new TimeoutManager(options, A.Fake<ILogger<TimeoutManager>>());

		// Act
		var timeout = manager.GetTimeout("Custom.Op");

		// Assert
		timeout.ShouldBe(TimeSpan.FromSeconds(99));
	}

	#endregion
}
