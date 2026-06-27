// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch;

using Tests.Shared.Conformance.Cdc;

namespace Excalibur.Cdc.Tests.Conformance;

/// <summary>
/// Conformance tests for the generic <see cref="ICdcStateStore"/> contract, wired with a minimal
/// in-memory reference implementation.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 851 / <c>qxatfw</c>: this is the FIRST concrete deriver of
/// <see cref="CdcProviderConformanceTestBase"/> — before it, the base's 15 contract facts had ZERO
/// derivers and executed against nothing (dead-contract / false confidence). Wiring a deriver makes the
/// checkpoint save/get/delete/resume + multi-consumer isolation + concurrent-access invariants actually
/// run in unit CI.
/// </para>
/// <para>
/// A <b>local reference</b> store is used deliberately rather than a shipped provider's in-memory shim:
/// the shipped <c>InMemory*CdcStateStore</c> types are <c>internal</c> and <em>provider-specific</em>, and
/// they diverge from the generic contract (e.g. <c>InMemoryPostgresCdcStateStore.DeletePositionAsync</c>
/// always returns <see langword="true"/>, violating
/// <see cref="CdcProviderConformanceTestBase"/>'s "delete non-existent ⇒ false" fact). This reference impl
/// is the minimal correct generic behaviour the contract requires; per-provider derivers
/// (emulator/TestContainers-gated) can be layered on later.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Cdc")]
public sealed class InMemoryCdcStateStoreConformanceShould : CdcProviderConformanceTestBase
{
	/// <inheritdoc/>
	protected override Task<ICdcStateStore> CreateStateStoreAsync() =>
		Task.FromResult<ICdcStateStore>(new InMemoryCdcStateStore());

	/// <inheritdoc/>
	protected override ChangePosition CreateTestPosition(int index) =>
		new TokenChangePosition($"cdc-token-{index:D6}");

	/// <inheritdoc/>
	protected override Task CleanupAsync() => Task.CompletedTask;

	/// <summary>
	/// Minimal correct in-memory <see cref="ICdcStateStore"/> reference implementation.
	/// </summary>
	private sealed class InMemoryCdcStateStore : ICdcStateStore
	{
		private readonly ConcurrentDictionary<string, ChangePosition> _positions = new(StringComparer.Ordinal);

		public Task<ChangePosition?> GetPositionAsync(string consumerId, CancellationToken cancellationToken) =>
			Task.FromResult(_positions.TryGetValue(consumerId, out var position) ? position : null);

		public Task SavePositionAsync(string consumerId, ChangePosition position, CancellationToken cancellationToken)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(consumerId);
			ArgumentNullException.ThrowIfNull(position);

			// Last-write-wins atomic upsert (ConcurrentDictionary indexer is atomic).
			_positions[consumerId] = position;
			return Task.CompletedTask;
		}

		public Task<bool> DeletePositionAsync(string consumerId, CancellationToken cancellationToken) =>
			// Contract: true only when a checkpoint actually existed and was removed.
			Task.FromResult(_positions.TryRemove(consumerId, out _));

		public async IAsyncEnumerable<(string ConsumerId, ChangePosition Position)> GetAllPositionsAsync(
			[EnumeratorCancellation] CancellationToken cancellationToken)
		{
			await Task.CompletedTask.ConfigureAwait(false);
			foreach (var kvp in _positions)
			{
				yield return (kvp.Key, kvp.Value);
			}
		}
	}
}
