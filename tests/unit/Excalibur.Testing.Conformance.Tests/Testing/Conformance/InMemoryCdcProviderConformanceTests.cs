// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch;
using Excalibur.Testing.Conformance;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Self-test proving <see cref="CdcProviderConformanceTestKit"/> runs end-to-end against a minimal correct
/// in-memory <see cref="ICdcStateStore"/> reference implementation and reports pass/fail (wired-and-tested).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "STORE")]
public sealed class InMemoryCdcProviderConformanceTests : CdcProviderConformanceTestKit
{
	/// <inheritdoc />
	protected override Task<ICdcStateStore> CreateStateStoreAsync() =>
		Task.FromResult<ICdcStateStore>(new InMemoryCdcStateStore());

	/// <inheritdoc />
	protected override ChangePosition CreateTestPosition(int index) =>
		new TokenChangePosition($"cdc-token-{index:D6}");

	[Fact] public Task SaveAndGetPosition_RoundTrips_Test() => SaveAndGetPosition_RoundTrips();
	[Fact] public Task GetPosition_NoCheckpoint_ReturnsNull_Test() => GetPosition_NoCheckpoint_ReturnsNull();
	[Fact] public Task SavePosition_MultipleConsumers_Independent_Test() => SavePosition_MultipleConsumers_Independent();
	[Fact] public Task SavePosition_Overwrites_PreviousCheckpoint_Test() => SavePosition_Overwrites_PreviousCheckpoint();
	[Fact] public Task SavePosition_PreservesPositionValidity_Test() => SavePosition_PreservesPositionValidity();
	[Fact] public Task Resume_FromSavedCheckpoint_ReturnsCorrectPosition_Test() => Resume_FromSavedCheckpoint_ReturnsCorrectPosition();
	[Fact] public Task Resume_AfterDelete_ReturnsNull_Test() => Resume_AfterDelete_ReturnsNull();
	[Fact] public Task DeletePosition_ExistingCheckpoint_ReturnsTrue_Test() => DeletePosition_ExistingCheckpoint_ReturnsTrue();
	[Fact] public Task DeletePosition_NonExistentCheckpoint_ReturnsFalse_Test() => DeletePosition_NonExistentCheckpoint_ReturnsFalse();
	[Fact] public Task DeletePosition_DoesNotAffectOtherConsumers_Test() => DeletePosition_DoesNotAffectOtherConsumers();
	[Fact] public Task GetAllPositions_ReturnsAllConsumerCheckpoints_Test() => GetAllPositions_ReturnsAllConsumerCheckpoints();
	[Fact] public Task GetAllPositions_EmptyStore_ReturnsEmpty_Test() => GetAllPositions_EmptyStore_ReturnsEmpty();
	[Fact] public Task ConcurrentSavePosition_AllSucceed_Test() => ConcurrentSavePosition_AllSucceed();
	[Fact] public Task ConcurrentSavePosition_SameConsumer_LastWriteWins_Test() => ConcurrentSavePosition_SameConsumer_LastWriteWins();

	/// <summary>
	/// Minimal correct in-memory <see cref="ICdcStateStore"/> reference implementation used only to
	/// exercise the conformance kit.
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

			_positions[consumerId] = position;
			return Task.CompletedTask;
		}

		public Task<bool> DeletePositionAsync(string consumerId, CancellationToken cancellationToken) =>
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
