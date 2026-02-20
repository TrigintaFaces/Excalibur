// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.LeaderElection.Tests.Kubernetes;

/// <summary>
/// Tests for Sprint 542 P0 fixes in <see cref="KubernetesLeaderElection"/>:
/// S542.5 (bd-3et8f): async void timer callback -> ConcurrentBag Task tracking
/// S542.6 (bd-0pj5k): thread-unsafe IsLeader/CurrentLeaderId -> volatile + lock
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class KubernetesLeaderElectionShould
{
	// --- S542.5 (bd-3et8f): ConcurrentBag<Task> tracking ---

	[Fact]
	public void HaveTrackedTasksField()
	{
		var field = typeof(KubernetesLeaderElection)
			.GetField("_trackedTasks", BindingFlags.NonPublic | BindingFlags.Instance);

		field.ShouldNotBeNull("KubernetesLeaderElection should have _trackedTasks field for async void task tracking");
		field.FieldType.ShouldBe(typeof(ConcurrentBag<Task>));
	}

	[Fact]
	public void HaveNonAsyncVoidRenewCallback()
	{
		var renewMethod = typeof(KubernetesLeaderElection)
			.GetMethod("RenewLeadershipAsync", BindingFlags.NonPublic | BindingFlags.Instance);

		renewMethod.ShouldNotBeNull();
		renewMethod.ReturnType.ShouldBe(typeof(void),
			"RenewLeadershipAsync should return void (TimerCallback delegate)");

		renewMethod.GetCustomAttribute<AsyncStateMachineAttribute>().ShouldBeNull(
			"RenewLeadershipAsync should NOT be async void — use Task.Run tracking instead");
	}

	[Fact]
	public void ImplementIAsyncDisposable()
	{
		typeof(IAsyncDisposable).IsAssignableFrom(typeof(KubernetesLeaderElection)).ShouldBeTrue(
			"KubernetesLeaderElection should implement IAsyncDisposable");
	}

	// --- S542.6 (bd-0pj5k): volatile IsLeader + thread-safe CurrentLeaderId ---

	[Fact]
	public void HaveVolatileIsLeaderField()
	{
		var field = typeof(KubernetesLeaderElection)
			.GetField("_isLeader", BindingFlags.NonPublic | BindingFlags.Instance);

		field.ShouldNotBeNull("KubernetesLeaderElection should have _isLeader backing field");

		var modifiers = field.GetRequiredCustomModifiers();
		modifiers.ShouldContain(typeof(IsVolatile),
			"_isLeader should be volatile for lock-free hot-path reads");
	}

	[Fact]
	public void HaveVolatileCurrentLeaderIdField()
	{
		var field = typeof(KubernetesLeaderElection)
			.GetField("_currentLeaderId", BindingFlags.NonPublic | BindingFlags.Instance);

		field.ShouldNotBeNull("KubernetesLeaderElection should have _currentLeaderId backing field");

		var modifiers = field.GetRequiredCustomModifiers();
		modifiers.ShouldContain(typeof(IsVolatile),
			"_currentLeaderId should be volatile for thread-safe reads");
	}

	[Fact]
	public void NotUseAutoPropertiesForIsLeaderAndCurrentLeaderId()
	{
		// Auto-properties generate backing fields named "<PropertyName>k__BackingField"
		// We want explicit volatile fields instead
		var autoBackingField = typeof(KubernetesLeaderElection)
			.GetField("<IsLeader>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);

		autoBackingField.ShouldBeNull(
			"IsLeader should NOT use an auto-property — use explicit volatile field instead");

		var leaderIdAutoField = typeof(KubernetesLeaderElection)
			.GetField("<CurrentLeaderId>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);

		leaderIdAutoField.ShouldBeNull(
			"CurrentLeaderId should NOT use an auto-property — use explicit volatile field instead");
	}

	[Fact]
	public void HaveDisposedVolatileField()
	{
		var field = typeof(KubernetesLeaderElection)
			.GetField("_disposed", BindingFlags.NonPublic | BindingFlags.Instance);

		field.ShouldNotBeNull();

		var modifiers = field.GetRequiredCustomModifiers();
		modifiers.ShouldContain(typeof(IsVolatile),
			"_disposed should be volatile for thread-safe disposal checks");
	}
}
