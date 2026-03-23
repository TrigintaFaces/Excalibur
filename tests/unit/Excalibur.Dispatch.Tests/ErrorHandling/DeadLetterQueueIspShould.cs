// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.ErrorHandling;

namespace Excalibur.Dispatch.Tests.ErrorHandling;

/// <summary>
/// ISP compliance tests for IDeadLetterQueue interface.
/// Verifies interface shape, method semantics, and ISP gate compliance.
/// IDeadLetterQueue currently has 8 methods -- the <=5 method gate requires splitting
/// into core (Enqueue, GetEntries, GetEntry, GetCount) + admin (Replay, ReplayBatch, Purge, PurgeOlderThan).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DeadLetterQueueIspShould
{
	private static readonly BindingFlags DeclaredPublicInstance =
		BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

	#region Interface Shape

	[Fact]
	public void HaveEightMethodsCurrently()
	{
		// IDeadLetterQueue currently has 8 methods (violates <=5 gate)
		// This test documents the current state and will be updated after ISP split
		var methods = typeof(IDeadLetterQueue).GetMethods(DeclaredPublicInstance);

		methods.Length.ShouldBe(8,
			"IDeadLetterQueue should have exactly 8 methods (pre-ISP split)");
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
	public void HaveReplayBatchAsyncMethod()
	{
		var method = typeof(IDeadLetterQueue).GetMethod("ReplayBatchAsync", DeclaredPublicInstance);

		method.ShouldNotBeNull("IDeadLetterQueue must have ReplayBatchAsync");
		method.ReturnType.ShouldBe(typeof(Task<int>));
	}

	[Fact]
	public void HavePurgeAsyncMethod()
	{
		var method = typeof(IDeadLetterQueue).GetMethod("PurgeAsync", DeclaredPublicInstance);

		method.ShouldNotBeNull("IDeadLetterQueue must have PurgeAsync");
		method.ReturnType.ShouldBe(typeof(Task<bool>));
	}

	[Fact]
	public void HavePurgeOlderThanAsyncMethod()
	{
		var method = typeof(IDeadLetterQueue).GetMethod("PurgeOlderThanAsync", DeclaredPublicInstance);

		method.ShouldNotBeNull("IDeadLetterQueue must have PurgeOlderThanAsync");
		method.ReturnType.ShouldBe(typeof(Task<int>));
	}

	#endregion

	#region ISP Split Categorization

	[Fact]
	public void HaveFourCoreMethods()
	{
		// Core operations: Enqueue, GetEntries, GetEntry, GetCount
		var coreMethodNames = new[] { "EnqueueAsync", "GetEntriesAsync", "GetEntryAsync", "GetCountAsync" };

		var methods = typeof(IDeadLetterQueue).GetMethods(DeclaredPublicInstance);
		var coreFound = methods.Where(m => coreMethodNames.Contains(m.Name)).ToList();

		coreFound.Count.ShouldBe(4,
			"IDeadLetterQueue should have exactly 4 core methods (Enqueue, GetEntries, GetEntry, GetCount)");
	}

	[Fact]
	public void HaveFourAdminMethods()
	{
		// Admin operations: Replay, ReplayBatch, Purge, PurgeOlderThan
		var adminMethodNames = new[] { "ReplayAsync", "ReplayBatchAsync", "PurgeAsync", "PurgeOlderThanAsync" };

		var methods = typeof(IDeadLetterQueue).GetMethods(DeclaredPublicInstance);
		var adminFound = methods.Where(m => adminMethodNames.Contains(m.Name)).ToList();

		adminFound.Count.ShouldBe(4,
			"IDeadLetterQueue should have exactly 4 admin methods (Replay, ReplayBatch, Purge, PurgeOlderThan)");
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
		// Arrange
		var dlq = A.Fake<IDeadLetterQueue>();
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
		// Arrange
		var dlq = A.Fake<IDeadLetterQueue>();
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
