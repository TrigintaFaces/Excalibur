// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Outbox.Tests.Core;

/// <summary>
/// Tests verifying that OutboxProcessor and InboxProcessor use ConcurrentBag
/// for thread-safe parallel batch processing (AD-540.1 / S540.2).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class OutboxProcessorConcurrentBagShould : UnitTestBase
{
	[Fact]
	public void UseConcurrentBagInProcessBatchParallelAsync()
	{
		// Async methods compile to state machine classes whose fields hold captured locals.
		// Look for a nested type matching the state machine naming pattern for ProcessBatchParallelAsync,
		// then verify it has fields of type ConcurrentBag<>.
		var type = typeof(OutboxProcessor);

		// The method must exist
		var method = type.GetMethod(
			"ProcessBatchParallelAsync",
			BindingFlags.NonPublic | BindingFlags.Instance);
		method.ShouldNotBeNull("ProcessBatchParallelAsync must exist on OutboxProcessor");

		// Find ConcurrentBag references in the state machine or closure types
		var nestedTypes = type.GetNestedTypes(BindingFlags.NonPublic);
		var hasConcurrentBag = nestedTypes
			.SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
			.Any(f => f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(ConcurrentBag<>));

		hasConcurrentBag.ShouldBeTrue("ProcessBatchParallelAsync should use ConcurrentBag<T> for thread-safe collection");
	}

	[Fact]
	public void InboxProcessorUseConcurrentBagInProcessBatchParallelAsync()
	{
		// Same verification for InboxProcessor
		var type = typeof(InboxProcessor);

		var method = type.GetMethod(
			"ProcessBatchParallelAsync",
			BindingFlags.NonPublic | BindingFlags.Instance);
		method.ShouldNotBeNull("ProcessBatchParallelAsync must exist on InboxProcessor");

		var nestedTypes = type.GetNestedTypes(BindingFlags.NonPublic);
		var hasConcurrentBag = nestedTypes
			.SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
			.Any(f => f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(ConcurrentBag<>));

		hasConcurrentBag.ShouldBeTrue("InboxProcessor ProcessBatchParallelAsync should use ConcurrentBag<T>");
	}
}
