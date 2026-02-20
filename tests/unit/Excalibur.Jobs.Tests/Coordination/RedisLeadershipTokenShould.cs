// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;

using StackExchange.Redis;

namespace Excalibur.Jobs.Tests.Coordination;

/// <summary>
/// Unit tests for RedisLeadershipToken (internal type).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
public sealed class RedisLeadershipTokenShould
{
	private readonly IDatabase _database = A.Fake<IDatabase>();

	private IAsyncDisposable CreateToken(
		string leaderKey = "leader:key",
		string leaderInstanceId = "leader-1",
		DateTimeOffset? acquiredAt = null,
		DateTimeOffset? expiresAt = null)
	{
		var now = DateTimeOffset.UtcNow;
		var type = typeof(Excalibur.Jobs.Core.JobConfigHostedWatcherServiceFactory).Assembly
			.GetType("Excalibur.Jobs.Coordination.RedisLeadershipToken")!;

		return (IAsyncDisposable)Activator.CreateInstance(
			type,
			_database,
			leaderKey,
			leaderInstanceId,
			acquiredAt ?? now,
			expiresAt ?? now.AddMinutes(5),
			NullLogger.Instance)!;
	}

	[Fact]
	public void HaveCorrectLeaderInstanceId()
	{
		var token = CreateToken(leaderInstanceId: "my-leader");
		var prop = token.GetType().GetProperty("LeaderInstanceId")!;

		prop.GetValue(token).ShouldBe("my-leader");
	}

	[Fact]
	public void IsValid_ReturnsTrueWhenNotExpired()
	{
		var token = CreateToken(expiresAt: DateTimeOffset.UtcNow.AddMinutes(5));
		var prop = token.GetType().GetProperty("IsValid")!;

		((bool)prop.GetValue(token)!).ShouldBeTrue();
	}

	[Fact]
	public void IsValid_ReturnsFalseWhenExpired()
	{
		var token = CreateToken(expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));
		var prop = token.GetType().GetProperty("IsValid")!;

		((bool)prop.GetValue(token)!).ShouldBeFalse();
	}

	[Fact]
	public async Task ExtendAsync_ReturnsTrueWhenExtended()
	{
		A.CallTo(() => _database.KeyExpireAsync(
			A<RedisKey>._,
			A<TimeSpan>._,
			A<ExpireWhen>._,
			A<CommandFlags>._))
			.Returns(true);

		var token = CreateToken();
		var method = token.GetType().GetMethod("ExtendAsync")!;

		var result = await (Task<bool>)method.Invoke(token, [TimeSpan.FromMinutes(10), CancellationToken.None])!;

		result.ShouldBeTrue();
	}

	[Fact]
	public async Task ExtendAsync_ReturnsFalseWhenDisposed()
	{
		A.CallTo(() => _database.KeyDeleteAsync(A<RedisKey>._, A<CommandFlags>._))
			.Returns(true);

		var token = CreateToken();
		await token.DisposeAsync();

		var method = token.GetType().GetMethod("ExtendAsync")!;
		var result = await (Task<bool>)method.Invoke(token, [TimeSpan.FromMinutes(10), CancellationToken.None])!;

		result.ShouldBeFalse();
	}

	[Fact]
	public async Task ReleaseAsync_DeletesKey()
	{
		A.CallTo(() => _database.KeyDeleteAsync(A<RedisKey>._, A<CommandFlags>._))
			.Returns(true);

		var token = CreateToken(leaderKey: "leader:mykey");
		var method = token.GetType().GetMethod("ReleaseAsync")!;

		await (Task)method.Invoke(token, [CancellationToken.None])!;

		A.CallTo(() => _database.KeyDeleteAsync(
			(RedisKey)"leader:mykey",
			A<CommandFlags>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReleaseAsync_DoesNothing_WhenAlreadyReleased()
	{
		A.CallTo(() => _database.KeyDeleteAsync(A<RedisKey>._, A<CommandFlags>._))
			.Returns(true);

		var token = CreateToken();
		var method = token.GetType().GetMethod("ReleaseAsync")!;

		await (Task)method.Invoke(token, [CancellationToken.None])!;
		Fake.ClearRecordedCalls(_database);

		await (Task)method.Invoke(token, [CancellationToken.None])!;

		A.CallTo(() => _database.KeyDeleteAsync(A<RedisKey>._, A<CommandFlags>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task DisposeAsync_ReleasesLock()
	{
		A.CallTo(() => _database.KeyDeleteAsync(A<RedisKey>._, A<CommandFlags>._))
			.Returns(true);

		var token = CreateToken();

		await token.DisposeAsync();

		A.CallTo(() => _database.KeyDeleteAsync(A<RedisKey>._, A<CommandFlags>._))
			.MustHaveHappenedOnceExactly();
	}
}
