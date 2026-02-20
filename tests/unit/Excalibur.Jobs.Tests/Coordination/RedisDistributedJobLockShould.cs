// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using StackExchange.Redis;

namespace Excalibur.Jobs.Tests.Coordination;

/// <summary>
/// Unit tests for RedisDistributedJobLock.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
public sealed class RedisDistributedJobLockShould
{
	private readonly IDatabase _database = A.Fake<IDatabase>();
	private readonly ILogger _logger = NullLogger.Instance;

	private IAsyncDisposable CreateLock(
		string lockKey = "test:lock",
		string jobKey = "test-job",
		string instanceId = "instance-1",
		DateTimeOffset? acquiredAt = null,
		DateTimeOffset? expiresAt = null)
	{
		var now = DateTimeOffset.UtcNow;
		// Use internal type via reflection
		var type = typeof(Excalibur.Jobs.Core.JobConfigHostedWatcherServiceFactory).Assembly
			.GetType("Excalibur.Jobs.Coordination.RedisDistributedJobLock")!;

		return (IAsyncDisposable)Activator.CreateInstance(
			type,
			_database,
			lockKey,
			jobKey,
			instanceId,
			acquiredAt ?? now,
			expiresAt ?? now.AddMinutes(5),
			_logger)!;
	}

	[Fact]
	public void HaveCorrectJobKey()
	{
		// Arrange & Act
		var lockObj = CreateLock(jobKey: "my-job");
		var jobKeyProp = lockObj.GetType().GetProperty("JobKey")!;

		// Assert
		jobKeyProp.GetValue(lockObj).ShouldBe("my-job");
	}

	[Fact]
	public void HaveCorrectInstanceId()
	{
		// Arrange & Act
		var lockObj = CreateLock(instanceId: "host-1");
		var instanceIdProp = lockObj.GetType().GetProperty("InstanceId")!;

		// Assert
		instanceIdProp.GetValue(lockObj).ShouldBe("host-1");
	}

	[Fact]
	public void IsValid_ReturnsTrueWhenNotExpired()
	{
		// Arrange
		var lockObj = CreateLock(expiresAt: DateTimeOffset.UtcNow.AddMinutes(5));
		var isValidProp = lockObj.GetType().GetProperty("IsValid")!;

		// Assert
		((bool)isValidProp.GetValue(lockObj)!).ShouldBeTrue();
	}

	[Fact]
	public void IsValid_ReturnsFalseWhenExpired()
	{
		// Arrange
		var lockObj = CreateLock(expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));
		var isValidProp = lockObj.GetType().GetProperty("IsValid")!;

		// Assert
		((bool)isValidProp.GetValue(lockObj)!).ShouldBeFalse();
	}

	[Fact]
	public async Task ExtendAsync_ExtendsTtl()
	{
		// Arrange — use method-name matching to avoid StackExchange.Redis overload ambiguity
		A.CallTo(_database)
			.Where(call => call.Method.Name == "KeyExpireAsync")
			.WithReturnType<Task<bool>>()
			.Returns(true);

		var lockObj = CreateLock();
		var extendMethod = lockObj.GetType().GetMethod("ExtendAsync")!;

		// Act
		var result = await (Task<bool>)extendMethod.Invoke(lockObj, [TimeSpan.FromMinutes(10), CancellationToken.None])!;

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task ExtendAsync_ReturnsFalseWhenDisposed()
	{
		// Arrange
		A.CallTo(() => _database.KeyDeleteAsync(A<RedisKey>._, A<CommandFlags>._))
			.Returns(true);

		var lockObj = CreateLock();

		// Dispose first
		await lockObj.DisposeAsync();

		// Act
		var extendMethod = lockObj.GetType().GetMethod("ExtendAsync")!;
		var result = await (Task<bool>)extendMethod.Invoke(lockObj, [TimeSpan.FromMinutes(10), CancellationToken.None])!;

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task ReleaseAsync_DeletesRedisKey()
	{
		// Arrange
		A.CallTo(() => _database.KeyDeleteAsync(A<RedisKey>._, A<CommandFlags>._))
			.Returns(true);

		var lockObj = CreateLock(lockKey: "mylock");
		var releaseMethod = lockObj.GetType().GetMethod("ReleaseAsync")!;

		// Act
		await (Task)releaseMethod.Invoke(lockObj, [CancellationToken.None])!;

		// Assert
		A.CallTo(() => _database.KeyDeleteAsync(
			(RedisKey)"mylock",
			A<CommandFlags>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReleaseAsync_DoesNothing_WhenAlreadyDisposed()
	{
		// Arrange
		A.CallTo(() => _database.KeyDeleteAsync(A<RedisKey>._, A<CommandFlags>._))
			.Returns(true);

		var lockObj = CreateLock();
		var releaseMethod = lockObj.GetType().GetMethod("ReleaseAsync")!;

		// Release once
		await (Task)releaseMethod.Invoke(lockObj, [CancellationToken.None])!;
		Fake.ClearRecordedCalls(_database);

		// Act — second release should do nothing
		await (Task)releaseMethod.Invoke(lockObj, [CancellationToken.None])!;

		// Assert
		A.CallTo(() => _database.KeyDeleteAsync(A<RedisKey>._, A<CommandFlags>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task DisposeAsync_ReleasesLock()
	{
		// Arrange
		A.CallTo(() => _database.KeyDeleteAsync(A<RedisKey>._, A<CommandFlags>._))
			.Returns(true);

		var lockObj = CreateLock();

		// Act
		await lockObj.DisposeAsync();

		// Assert
		A.CallTo(() => _database.KeyDeleteAsync(A<RedisKey>._, A<CommandFlags>._))
			.MustHaveHappenedOnceExactly();
	}
}
