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
		string ownerToken = "owner-token-1",
		DateTimeOffset? acquiredAt = null,
		DateTimeOffset? expiresAt = null)
	{
		var now = DateTimeOffset.UtcNow;
		// Use internal type via reflection (moved to Jobs.Redis package).
		// jqlqc8: the ctor now takes a per-acquisition ownerToken (the bare Redis lock value)
		// between instanceId and acquiredAt — it backs the owner-checked release/extend Lua.
		var type = typeof(Excalibur.Jobs.Redis.Coordination.RedisJobCoordinator).Assembly
			.GetType("Excalibur.Jobs.Redis.Coordination.RedisDistributedJobLock")!;

		return (IAsyncDisposable)Activator.CreateInstance(
			type,
			_database,
			lockKey,
			jobKey,
			instanceId,
			ownerToken,
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
		// Arrange — jqlqc8: ExtendAsync now runs an owner-checked compare-and-PEXPIRE Lua script
		// (ScriptEvaluateAsync) instead of an unconditional KeyExpireAsync. A Lua return of 1 means
		// "this acquisition still owns the lock → extended". Match by method name (overload ambiguity).
		A.CallTo(_database)
			.Where(call => call.Method.Name == "ScriptEvaluateAsync")
			.WithReturnType<Task<RedisResult>>()
			.Returns(Task.FromResult(RedisResult.Create((RedisValue)1)));

		var lockObj = CreateLock();
		var extendMethod = lockObj.GetType().GetMethod("ExtendAsync")!;

		// Act
		var result = await (Task<bool>)extendMethod.Invoke(lockObj, [TimeSpan.FromMinutes(10), CancellationToken.None])!;

		// Assert — owner-checked extend succeeded, and the Lua ran exactly once.
		result.ShouldBeTrue();
		A.CallTo(_database)
			.Where(call => call.Method.Name == "ScriptEvaluateAsync")
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ExtendAsync_ReturnsFalseWhenDisposed()
	{
		// Arrange — the dispose path now runs the owner-checked release Lua (ScriptEvaluateAsync).
		A.CallTo(_database)
			.Where(call => call.Method.Name == "ScriptEvaluateAsync")
			.WithReturnType<Task<RedisResult>>()
			.Returns(Task.FromResult(RedisResult.Create((RedisValue)1)));

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
	public async Task ReleaseAsync_RunsOwnerCheckedDeleteScript()
	{
		// Arrange — jqlqc8: ReleaseAsync now runs an owner-checked compare-and-DEL Lua (ScriptEvaluateAsync)
		// keyed on the per-acquisition owner token, NOT an unconditional KeyDeleteAsync.
		A.CallTo(_database)
			.Where(call => call.Method.Name == "ScriptEvaluateAsync")
			.WithReturnType<Task<RedisResult>>()
			.Returns(Task.FromResult(RedisResult.Create((RedisValue)1)));

		var lockObj = CreateLock(lockKey: "mylock");
		var releaseMethod = lockObj.GetType().GetMethod("ReleaseAsync")!;

		// Act
		await (Task)releaseMethod.Invoke(lockObj, [CancellationToken.None])!;

		// Assert — the owner-checked release script ran exactly once (the unconditional KeyDeleteAsync is gone).
		A.CallTo(_database)
			.Where(call => call.Method.Name == "ScriptEvaluateAsync")
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _database.KeyDeleteAsync(A<RedisKey>._, A<CommandFlags>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ReleaseAsync_DoesNothing_WhenAlreadyDisposed()
	{
		// Arrange — owner-checked release runs ScriptEvaluateAsync on the first call.
		A.CallTo(_database)
			.Where(call => call.Method.Name == "ScriptEvaluateAsync")
			.WithReturnType<Task<RedisResult>>()
			.Returns(Task.FromResult(RedisResult.Create((RedisValue)1)));

		var lockObj = CreateLock();
		var releaseMethod = lockObj.GetType().GetMethod("ReleaseAsync")!;

		// Release once
		await (Task)releaseMethod.Invoke(lockObj, [CancellationToken.None])!;
		Fake.ClearRecordedCalls(_database);

		// Act — second release should do nothing
		await (Task)releaseMethod.Invoke(lockObj, [CancellationToken.None])!;

		// Assert — the owner-checked script does NOT run again after disposal.
		A.CallTo(_database)
			.Where(call => call.Method.Name == "ScriptEvaluateAsync")
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task DisposeAsync_ReleasesLock()
	{
		// Arrange — disposal releases via the owner-checked release Lua (ScriptEvaluateAsync).
		A.CallTo(_database)
			.Where(call => call.Method.Name == "ScriptEvaluateAsync")
			.WithReturnType<Task<RedisResult>>()
			.Returns(Task.FromResult(RedisResult.Create((RedisValue)1)));

		var lockObj = CreateLock();

		// Act
		await lockObj.DisposeAsync();

		// Assert — the owner-checked release script ran exactly once.
		A.CallTo(_database)
			.Where(call => call.Method.Name == "ScriptEvaluateAsync")
			.MustHaveHappenedOnceExactly();
	}
}
