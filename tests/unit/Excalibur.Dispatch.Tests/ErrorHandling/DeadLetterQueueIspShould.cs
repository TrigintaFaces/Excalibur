// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.ErrorHandling;

namespace Excalibur.Dispatch.Tests.ErrorHandling;

/// <summary>
/// ISP compliance tests for IDeadLetterQueue and IDeadLetterQueueAdmin interfaces.
/// Verifies interface shape, method semantics, and ISP gate compliance.
/// IDeadLetterQueue has 5 core methods; IDeadLetterQueueAdmin has 3 admin methods (ReplayBatch, Purge, PurgeOlderThan).
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class DeadLetterQueueIspShould
{
	private static readonly BindingFlags DeclaredPublicInstance =
		BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

	#region Interface Shape

	[Fact]
	public void HaveFiveMethodsAfterIspSplit()
	{
		// IDeadLetterQueue has 5 core methods after ISP split (meets <=5 gate)
		var methods = typeof(IDeadLetterQueue).GetMethods(DeclaredPublicInstance);

		methods.Length.ShouldBe(5,
			"IDeadLetterQueue should have exactly 5 methods after ISP split");
	}

	[Fact]
	public void HaveEnqueueAsyncMethod()
	{
		var method = typeof(IDeadLetterQueue).GetMethod("EnqueueAsync", DeclaredPublicInstance);

		method.ShouldNotBeNull("IDeadLetterQueue must have EnqueueAsync");
		method.ReturnType.ShouldBe(typeof(Task<Guid>));
		method.IsGenericMethod.ShouldBeTrue("EnqueueAsync should be generic <T>");
	}

	[Fact]
	public void HaveGetEntriesAsyncMethod()
	{
		var method = typeof(IDeadLetterQueue).GetMethod("GetEntriesAsync", DeclaredPublicInstance);

		method.ShouldNotBeNull("IDeadLetterQueue must have GetEntriesAsync");
		method.ReturnType.ShouldBe(typeof(Task<IReadOnlyList<DeadLetterEntry>>));
	}

	[Fact]
	public void HaveGetEntryAsyncMethod()
	{
		var method = typeof(IDeadLetterQueue).GetMethod("GetEntryAsync", DeclaredPublicInstance);

		method.ShouldNotBeNull("IDeadLetterQueue must have GetEntryAsync");
		method.ReturnType.ShouldBe(typeof(Task<DeadLetterEntry?>));
	}

	[Fact]
	public void HaveGetCountAsyncMethod()
	{
		var method = typeof(IDeadLetterQueue).GetMethod("GetCountAsync", DeclaredPublicInstance);

		method.ShouldNotBeNull("IDeadLetterQueue must have GetCountAsync");
		method.ReturnType.ShouldBe(typeof(Task<long>));
	}

	[Fact]
	public void HaveReplayAsyncMethod()
	{
		var method = typeof(IDeadLetterQueue).GetMethod("ReplayAsync", DeclaredPublicInstance);

		method.ShouldNotBeNull("IDeadLetterQueue must have ReplayAsync");
		method.ReturnType.ShouldBe(typeof(Task<bool>));
	}

	[Fact]
	public void AdminHaveReplayBatchAsyncMethod()
	{
		var method = typeof(IDeadLetterQueueAdmin).GetMethod("ReplayBatchAsync", DeclaredPublicInstance);

		method.ShouldNotBeNull("IDeadLetterQueueAdmin must have ReplayBatchAsync");
		method.ReturnType.ShouldBe(typeof(Task<int>));
	}

	[Fact]
	public void AdminHavePurgeAsyncMethod()
	{
		var method = typeof(IDeadLetterQueueAdmin).GetMethod("PurgeAsync", DeclaredPublicInstance);

		method.ShouldNotBeNull("IDeadLetterQueueAdmin must have PurgeAsync");
		method.ReturnType.ShouldBe(typeof(Task<bool>));
	}

	[Fact]
	public void AdminHavePurgeOlderThanAsyncMethod()
	{
		var method = typeof(IDeadLetterQueueAdmin).GetMethod("PurgeOlderThanAsync", DeclaredPublicInstance);

		method.ShouldNotBeNull("IDeadLetterQueueAdmin must have PurgeOlderThanAsync");
		method.ReturnType.ShouldBe(typeof(Task<int>));
	}

	#endregion

	#region ISP Split Categorization

	[Fact]
	public void HaveFiveCoreMethods()
	{
		// Core operations: Enqueue, GetEntries, GetEntry, GetCount, Replay
		var coreMethodNames = new[] { "EnqueueAsync", "GetEntriesAsync", "GetEntryAsync", "GetCountAsync", "ReplayAsync" };

		var methods = typeof(IDeadLetterQueue).GetMethods(DeclaredPublicInstance);
		var coreFound = methods.Where(m => coreMethodNames.Contains(m.Name)).ToList();

		coreFound.Count.ShouldBe(5,
			"IDeadLetterQueue should have exactly 5 core methods (Enqueue, GetEntries, GetEntry, GetCount, Replay)");
	}

	[Fact]
	public void HaveThreeAdminMethods()
	{
		// Admin operations moved to IDeadLetterQueueAdmin: ReplayBatch, Purge, PurgeOlderThan
		var adminMethodNames = new[] { "ReplayBatchAsync", "PurgeAsync", "PurgeOlderThanAsync" };

		var methods = typeof(IDeadLetterQueueAdmin).GetMethods(DeclaredPublicInstance);
		var adminFound = methods.Where(m => adminMethodNames.Contains(m.Name)).ToList();

		adminFound.Count.ShouldBe(3,
			"IDeadLetterQueueAdmin should have exactly 3 admin methods (ReplayBatch, Purge, PurgeOlderThan)");
	}

	#endregion

	#region Call Semantics via Fake

	[Fact]
	public async Task AcceptEnqueueWithRequiredParameters()
	{
		// Arrange
		var dlq = A.Fake<IDeadLetterQueue>();
		var expectedId = Guid.NewGuid();
		A.CallTo(() => dlq.EnqueueAsync(
			A<string>.Ignored,
			A<DeadLetterReason>.Ignored,
			A<CancellationToken>.Ignored,
			A<Exception?>.Ignored,
			A<IDictionary<string, string>?>.Ignored))
			.Returns(expectedId);

		// Act
		var result = await dlq.EnqueueAsync(
			"test-message",
			DeadLetterReason.MaxRetriesExceeded,
			CancellationToken.None);

		// Assert
		result.ShouldBe(expectedId);
	}

	[Fact]
	public async Task AcceptGetEntriesWithOptionalFilter()
	{
		// Arrange
		var dlq = A.Fake<IDeadLetterQueue>();
		var entries = new List<DeadLetterEntry>();
		A.CallTo(() => dlq.GetEntriesAsync(
			A<CancellationToken>.Ignored,
			A<DeadLetterQueryFilter?>.Ignored,
			A<int>.Ignored))
			.Returns(entries);

		// Act -- call with defaults
		var result = await dlq.GetEntriesAsync(CancellationToken.None);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task AcceptGetEntryById()
	{
		// Arrange
		var dlq = A.Fake<IDeadLetterQueue>();
		var entryId = Guid.NewGuid();
		A.CallTo(() => dlq.GetEntryAsync(entryId, A<CancellationToken>.Ignored))
			.Returns(Task.FromResult<DeadLetterEntry?>(null));

		// Act
		var result = await dlq.GetEntryAsync(entryId, CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task AcceptReplayById()
	{
		// Arrange
		var dlq = A.Fake<IDeadLetterQueue>();
		var entryId = Guid.NewGuid();
		A.CallTo(() => dlq.ReplayAsync(entryId, A<CancellationToken>.Ignored))
			.Returns(true);

		// Act
		var result = await dlq.ReplayAsync(entryId, CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task AcceptPurgeById()
	{
		// Arrange -- PurgeAsync moved to IDeadLetterQueueAdmin ISP sub-interface
		var dlq = A.Fake<IDeadLetterQueueAdmin>();
		A.CallTo(() => dlq.PurgeAsync(A<Guid>.Ignored, A<CancellationToken>.Ignored))
			.Returns(true);

		// Act
		var result = await dlq.PurgeAsync(Guid.NewGuid(), CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task AcceptPurgeOlderThan()
	{
		// Arrange -- PurgeOlderThanAsync moved to IDeadLetterQueueAdmin ISP sub-interface
		var dlq = A.Fake<IDeadLetterQueueAdmin>();
		A.CallTo(() => dlq.PurgeOlderThanAsync(A<TimeSpan>.Ignored, A<CancellationToken>.Ignored))
			.Returns(5);

		// Act
		var result = await dlq.PurgeOlderThanAsync(TimeSpan.FromDays(30), CancellationToken.None);

		// Assert
		result.ShouldBe(5);
	}

	[Fact]
	public async Task AcceptGetCountWithOptionalFilter()
	{
		// Arrange
		var dlq = A.Fake<IDeadLetterQueue>();
		A.CallTo(() => dlq.GetCountAsync(A<CancellationToken>.Ignored, A<DeadLetterQueryFilter?>.Ignored))
			.Returns(42L);

		// Act
		var count = await dlq.GetCountAsync(CancellationToken.None);

		// Assert
		count.ShouldBe(42L);
	}

	#endregion

	#region NullDeadLetterQueue Implementation Compliance

	[Fact]
	public void NullImplementation_ShouldImplementInterface()
	{
		typeof(IDeadLetterQueue).IsAssignableFrom(typeof(NullDeadLetterQueue)).ShouldBeTrue(
			"NullDeadLetterQueue must implement IDeadLetterQueue");
	}

	[Fact]
	public void NullImplementation_ShouldBeSealed()
	{
		typeof(NullDeadLetterQueue).IsSealed.ShouldBeTrue(
			"NullDeadLetterQueue should be sealed");
	}

	[Fact]
	public async Task NullImplementation_EnqueueShouldReturnEmptyGuid()
	{
		// Arrange
		var dlq = NullDeadLetterQueue.Instance;

		// Act
		var result = await dlq.EnqueueAsync(
			"message",
			DeadLetterReason.MaxRetriesExceeded,
			CancellationToken.None);

		// Assert
		result.ShouldBe(Guid.Empty);
	}

	[Fact]
	public async Task NullImplementation_GetEntriesShouldReturnEmpty()
	{
		// Arrange
		var dlq = NullDeadLetterQueue.Instance;

		// Act
		var result = await dlq.GetEntriesAsync(CancellationToken.None);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task NullImplementation_GetCountShouldReturnZero()
	{
		// Arrange
		var dlq = NullDeadLetterQueue.Instance;

		// Act
		var count = await dlq.GetCountAsync(CancellationToken.None);

		// Assert
		count.ShouldBe(0L);
	}

	#endregion
}
