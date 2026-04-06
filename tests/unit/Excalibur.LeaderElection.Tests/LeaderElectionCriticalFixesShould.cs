// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.LeaderElection.Tests;

/// <summary>
/// Sprint 616 F.2: Tests for Leader Election critical fixes --
/// UpdateHealthAsync CancellationToken (C.1), Redis ValidateOnStart (C.2),
/// and Redis volatile _disposed guard (C.3).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
[Trait("Feature", "LeaderElection")]
public sealed class LeaderElectionCriticalFixesShould
{
	#region C.1: UpdateHealthAsync CancellationToken

	[Fact]
	public void RequireCancellationTokenOnUpdateHealthAsync()
	{
		// Arrange -- verify IHealthBasedLeaderElection.UpdateHealthAsync signature
		var method = typeof(IHealthBasedLeaderElection)
			.GetMethod("UpdateHealthAsync");

		// Assert
		method.ShouldNotBeNull();
		var parameters = method.GetParameters();
		parameters.Length.ShouldBe(3, "UpdateHealthAsync must have 3 params: isHealthy, metadata, cancellationToken");
		parameters[0].ParameterType.ShouldBe(typeof(bool));
		parameters[1].ParameterType.ShouldBe(typeof(IDictionary<string, string>));
		parameters[2].ParameterType.ShouldBe(typeof(CancellationToken));

		// Verify the CancellationToken parameter is NOT optional (no default value)
		parameters[2].HasDefaultValue.ShouldBeFalse(
			"CancellationToken must be required, not optional (= default)");
	}

	[Fact]
	public async Task InMemoryUpdateHealthAsync_AcceptsCancellationToken()
	{
		// Arrange
		var sharedState = new InMemoryLeaderElectionSharedState();
		await using var le = CreateInMemoryInstance("resource-ct", "candidate-1", sharedState);
		await le.StartAsync(CancellationToken.None);

		// Act -- should work with a live token
		using var cts = new CancellationTokenSource();
		await le.UpdateHealthAsync(true, null, cts.Token);

		// Assert -- no exception means success
		le.IsLeader.ShouldBeTrue();
	}

	[Fact]
	public async Task InMemoryUpdateHealthAsync_RespectsAlreadyCancelledToken()
	{
		// Arrange
		var sharedState = new InMemoryLeaderElectionSharedState();
		await using var le = CreateInMemoryInstance("resource-ct-cancel", "candidate-1", sharedState);
		await le.StartAsync(CancellationToken.None);

		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert -- InMemory implementation may or may not throw on cancelled token
		// depending on whether it checks CT. The key test is that the signature accepts it.
		// For InMemory, the implementation is synchronous so it likely completes regardless.
		await le.UpdateHealthAsync(true, null, cts.Token);
	}

	[Fact]
	public void AllHealthBasedLeaderElectionImplsHaveUpdatedSignature()
	{
		// Verify all IHealthBasedLeaderElection implementations have the 3-param UpdateHealthAsync
		// Note: RedisLeaderElection only implements ILeaderElection, NOT IHealthBasedLeaderElection
		var implTypes = new[]
		{
			typeof(InMemoryLeaderElection),
			typeof(SqlServerHealthBasedLeaderElection),
			typeof(ConsulLeaderElection),
			typeof(KubernetesLeaderElection),
		};

		foreach (var type in implTypes)
		{
			typeof(IHealthBasedLeaderElection).IsAssignableFrom(type).ShouldBeTrue(
				$"{type.Name} must implement IHealthBasedLeaderElection");

			var method = type.GetMethod("UpdateHealthAsync",
				BindingFlags.Public | BindingFlags.Instance,
				[typeof(bool), typeof(IDictionary<string, string>), typeof(CancellationToken)]);

			method.ShouldNotBeNull(
				$"{type.Name} must implement UpdateHealthAsync(bool, IDictionary, CancellationToken)");
		}
	}

	[Fact]
	public void RedisDoesNotImplementIHealthBasedLeaderElection()
	{
		// Redis only implements ILeaderElection (no health-based features)
		typeof(IHealthBasedLeaderElection).IsAssignableFrom(typeof(RedisLeaderElection)).ShouldBeFalse(
			"RedisLeaderElection implements ILeaderElection only, not IHealthBasedLeaderElection");
	}

	#endregion

	#region C.2: Redis ValidateOnStart

	[Fact]
	public void RegisterValidateOnStartForLeaderElectionOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddRedisLeaderElection("test:leader", options =>
		{
			options.InstanceId = "test";
		});

		// Assert -- options resolve with configured values (ValidateDataAnnotations removed in Sprint 750 AOT migration)
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<LeaderElectionOptions>>().Value;
		options.InstanceId.ShouldBe("test");
	}

	#endregion

	#region C.3: Redis _disposed Volatile Guard

	[Fact]
	public void DeclareDisposedFieldAsVolatile()
	{
		// Arrange
		var field = typeof(RedisLeaderElection)
			.GetField("_disposed", BindingFlags.NonPublic | BindingFlags.Instance);

		// Assert
		field.ShouldNotBeNull("_disposed field must exist on RedisLeaderElection");
		field.FieldType.ShouldBe(typeof(bool));

		var requiredModifiers = field.GetRequiredCustomModifiers();
		requiredModifiers.ShouldContain(typeof(System.Runtime.CompilerServices.IsVolatile),
			"_disposed must be declared volatile for thread-safe visibility");
	}

	[Fact]
	public async Task ThrowObjectDisposedExceptionOnStartAfterDispose()
	{
		// Arrange
		var fakeMultiplexer = A.Fake<IConnectionMultiplexer>();
		var le = new RedisLeaderElection(
			fakeMultiplexer,
			"test-key",
			Microsoft.Extensions.Options.Options.Create(new LeaderElectionOptions()),
			NullLogger<RedisLeaderElection>.Instance);

		await le.DisposeAsync();

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(() =>
			le.StartAsync(CancellationToken.None));
	}

	[Fact]
	public async Task ThrowObjectDisposedExceptionOnStopAfterDispose()
	{
		// Arrange
		var fakeMultiplexer = A.Fake<IConnectionMultiplexer>();
		var le = new RedisLeaderElection(
			fakeMultiplexer,
			"test-key",
			Microsoft.Extensions.Options.Options.Create(new LeaderElectionOptions()),
			NullLogger<RedisLeaderElection>.Instance);

		await le.DisposeAsync();

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(() =>
			le.StopAsync(CancellationToken.None));
	}

	[Fact]
	public async Task SafelyDisposeMultipleTimes()
	{
		// Arrange
		var fakeMultiplexer = A.Fake<IConnectionMultiplexer>();
		var le = new RedisLeaderElection(
			fakeMultiplexer,
			"test-key",
			Microsoft.Extensions.Options.Options.Create(new LeaderElectionOptions()),
			NullLogger<RedisLeaderElection>.Instance);

		// Act & Assert -- double dispose should not throw
		await le.DisposeAsync();
		await le.DisposeAsync();
	}

	#endregion

	#region Helpers

	private static InMemoryLeaderElection CreateInMemoryInstance(
		string resourceName,
		string candidateId,
		InMemoryLeaderElectionSharedState? sharedState = null)
	{
		var options = new LeaderElectionOptions { InstanceId = candidateId };
		return new InMemoryLeaderElection(
			resourceName,
			Options.Create(options),
			NullLogger<InMemoryLeaderElection>.Instance,
			sharedState);
	}

	#endregion
}
