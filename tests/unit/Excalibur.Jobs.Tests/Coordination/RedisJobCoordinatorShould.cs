// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Coordination;

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using StackExchange.Redis;

namespace Excalibur.Jobs.Tests.Coordination;

/// <summary>
/// Unit tests for <see cref="RedisJobCoordinator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
public sealed class RedisJobCoordinatorShould
{
	private readonly IDatabase _database = A.Fake<IDatabase>();
	private readonly RedisJobCoordinator _coordinator;

	public RedisJobCoordinatorShould()
	{
		_coordinator = new RedisJobCoordinator(
			_database,
			NullLogger<RedisJobCoordinator>.Instance,
			"test:");
	}

	[Fact]
	public void ThrowArgumentNullException_WhenDatabaseIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new RedisJobCoordinator(null!, NullLogger<RedisJobCoordinator>.Instance));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new RedisJobCoordinator(_database, null!));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenKeyPrefixIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new RedisJobCoordinator(_database, NullLogger<RedisJobCoordinator>.Instance, null!));
	}

	[Fact]
	public async Task TryAcquireLockAsync_ThrowsOnNullJobKey()
	{
		await Should.ThrowAsync<ArgumentException>(() =>
			_coordinator.TryAcquireLockAsync(null!, TimeSpan.FromMinutes(1), CancellationToken.None));
	}

	[Fact]
	public async Task TryAcquireLockAsync_ThrowsOnEmptyJobKey()
	{
		await Should.ThrowAsync<ArgumentException>(() =>
			_coordinator.TryAcquireLockAsync("", TimeSpan.FromMinutes(1), CancellationToken.None));
	}

	[Fact]
	public async Task TryAcquireLockAsync_ReturnsLock_WhenAcquired()
	{
		// Arrange — match any StringSetAsync call on the fake and return true
		A.CallTo(_database)
			.Where(call => call.Method.Name == "StringSetAsync")
			.WithReturnType<Task<bool>>()
			.Returns(true);

		// Act
		var result = await _coordinator.TryAcquireLockAsync("test-job", TimeSpan.FromMinutes(1), CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.JobKey.ShouldBe("test-job");
	}

	[Fact]
	public async Task TryAcquireLockAsync_ReturnsNull_WhenNotAcquired()
	{
		// Arrange
		A.CallTo(_database)
			.Where(call => call.Method.Name == "StringSetAsync")
			.WithReturnType<Task<bool>>()
			.Returns(false);

		// Act
		var result = await _coordinator.TryAcquireLockAsync("test-job", TimeSpan.FromMinutes(1), CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task RegisterInstanceAsync_ThrowsOnNullInstanceId()
	{
		await Should.ThrowAsync<ArgumentException>(() =>
			_coordinator.RegisterInstanceAsync(null!,
				new JobInstanceInfo("id", "host", new JobInstanceCapabilities(1, ["*"])),
				CancellationToken.None));
	}

	[Fact]
	public async Task RegisterInstanceAsync_ThrowsOnNullInstanceInfo()
	{
		await Should.ThrowAsync<ArgumentNullException>(() =>
			_coordinator.RegisterInstanceAsync("id", null!, CancellationToken.None));
	}

	[Fact]
	public async Task RegisterInstanceAsync_SetsRedisKeyAndAddsToSet()
	{
		// Arrange
		A.CallTo(_database)
			.Where(call => call.Method.Name == "StringSetAsync")
			.WithReturnType<Task<bool>>()
			.Returns(true);

		var instanceInfo = new JobInstanceInfo("test-instance", "host-1", new JobInstanceCapabilities(5, ["*"]));

		// Act
		await _coordinator.RegisterInstanceAsync("test-instance", instanceInfo, CancellationToken.None);

		// Assert — verify StringSetAsync was called with the expected key
		A.CallTo(_database)
			.Where(call => call.Method.Name == "StringSetAsync"
				&& call.Arguments.Count > 0
				&& Equals(call.Arguments[0], (RedisKey)"test:instances:test-instance"))
			.MustHaveHappenedOnceExactly();

		A.CallTo(() => _database.SetAddAsync(
			(RedisKey)"test:instances:active",
			(RedisValue)"test-instance",
			A<CommandFlags>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task UnregisterInstanceAsync_ThrowsOnNullInstanceId()
	{
		await Should.ThrowAsync<ArgumentException>(() =>
			_coordinator.UnregisterInstanceAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task UnregisterInstanceAsync_DeletesKeyAndRemovesFromSet()
	{
		// Act
		await _coordinator.UnregisterInstanceAsync("test-instance", CancellationToken.None);

		// Assert
		A.CallTo(() => _database.KeyDeleteAsync(
			(RedisKey)"test:instances:test-instance",
			A<CommandFlags>._))
			.MustHaveHappenedOnceExactly();

		A.CallTo(() => _database.SetRemoveAsync(
			(RedisKey)"test:instances:active",
			(RedisValue)"test-instance",
			A<CommandFlags>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GetActiveInstancesAsync_ReturnsEmptyList_WhenNoActiveInstances()
	{
		// Arrange
		A.CallTo(() => _database.SetMembersAsync(A<RedisKey>._, A<CommandFlags>._))
			.Returns(Array.Empty<RedisValue>());

		// Act
		var result = await _coordinator.GetActiveInstancesAsync(CancellationToken.None);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task DistributeJobAsync_ThrowsOnNullJobKey()
	{
		await Should.ThrowAsync<ArgumentException>(() =>
			_coordinator.DistributeJobAsync(null!, new { }, CancellationToken.None));
	}

	[Fact]
	public async Task DistributeJobAsync_ReturnsNull_WhenNoAvailableInstances()
	{
		// Arrange
		A.CallTo(() => _database.SetMembersAsync(A<RedisKey>._, A<CommandFlags>._))
			.Returns(Array.Empty<RedisValue>());

		// Act
		var result = await _coordinator.DistributeJobAsync("test-job", new { data = "test" }, CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task ReportJobCompletionAsync_ThrowsOnNullJobKey()
	{
		await Should.ThrowAsync<ArgumentException>(() =>
			_coordinator.ReportJobCompletionAsync(null!, "instance", true, null, CancellationToken.None));
	}

	[Fact]
	public async Task ReportJobCompletionAsync_ThrowsOnNullInstanceId()
	{
		await Should.ThrowAsync<ArgumentException>(() =>
			_coordinator.ReportJobCompletionAsync("job", null!, true, null, CancellationToken.None));
	}

	[Fact]
	public async Task ReportJobCompletionAsync_StoresCompletionData()
	{
		// Arrange
		A.CallTo(_database)
			.Where(call => call.Method.Name == "StringSetAsync")
			.WithReturnType<Task<bool>>()
			.Returns(true);

		// Act
		await _coordinator.ReportJobCompletionAsync("test-job", "instance-1", true, "result", CancellationToken.None);

		// Assert — verify StringSetAsync was called with the completions key
		A.CallTo(_database)
			.Where(call => call.Method.Name == "StringSetAsync"
				&& call.Arguments.Count > 0
				&& Equals(call.Arguments[0], (RedisKey)"test:completions:test-job"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReportJobCompletionAsync_HandlesNullResult()
	{
		// Arrange
		A.CallTo(_database)
			.Where(call => call.Method.Name == "StringSetAsync")
			.WithReturnType<Task<bool>>()
			.Returns(true);

		// Act
		await _coordinator.ReportJobCompletionAsync("test-job", "instance-1", false, null, CancellationToken.None);

		// Assert
		A.CallTo(_database)
			.Where(call => call.Method.Name == "StringSetAsync")
			.MustHaveHappenedOnceExactly();
	}
}
