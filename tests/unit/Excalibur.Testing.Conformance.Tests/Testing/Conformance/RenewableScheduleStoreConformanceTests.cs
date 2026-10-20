// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery;
using Excalibur.Testing;
using Excalibur.Testing.Conformance;
using Shouldly;
using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>Exercises the renewal kit against a test-only, expiring single-process authority.</summary>
/// <remarks>The provider-owned logical clock gives deterministic expiry; this suite makes no database or durability claim.</remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Inherited conformance arm names are fixed test contracts.")]
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "STORE")]
public sealed class RenewableScheduleStoreConformanceTests : ScheduleExecutionLeaseRenewalConformanceTestKit
{
	private readonly ExpiringAuthority _store = new();

	protected override IScheduleExecutionLeaseRenewal CreateStore() => _store;

	protected override Task ResetDataAsync()
	{
		_store.Reset();
		return Task.CompletedTask;
	}

	protected override Task WaitForLeaseTimeAsync(TimeSpan duration, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		_store.Advance(duration);
		return Task.CompletedTask;
	}

	[Fact] public override Task StoreAsync_ShouldPersistMessage() => base.StoreAsync_ShouldPersistMessage();
	[Fact] public override Task StoreAsync_WithNullMessage_ShouldThrow() => base.StoreAsync_WithNullMessage_ShouldThrow();
	[Fact] public override Task StoreAsync_SameId_ShouldUpsert() => base.StoreAsync_SameId_ShouldUpsert();
	[Fact] public override Task StoreAsync_MultipleMessages_ShouldPersistAll() => base.StoreAsync_MultipleMessages_ShouldPersistAll();
	[Fact] public override Task GetAllAsync_EmptyStore_ShouldReturnEmpty() => base.GetAllAsync_EmptyStore_ShouldReturnEmpty();
	[Fact] public override Task GetAllAsync_AfterStore_ShouldReturnMessage() => base.GetAllAsync_AfterStore_ShouldReturnMessage();
	[Fact] public override Task GetAllAsync_ShouldReturnAllMessages() => base.GetAllAsync_ShouldReturnAllMessages();
	[Fact] public override Task CompleteAsync_ShouldSetEnabledFalse() => base.CompleteAsync_ShouldSetEnabledFalse();
	[Fact] public override Task CompleteAsync_NonExistent_ShouldBeIdempotent() => base.CompleteAsync_NonExistent_ShouldBeIdempotent();
	[Fact] public override Task CompleteAsync_AlreadyCompleted_ShouldBeIdempotent() => base.CompleteAsync_AlreadyCompleted_ShouldBeIdempotent();
	[Fact] public override Task StoreAsync_ThenComplete_MessageRemainsPersisted() => base.StoreAsync_ThenComplete_MessageRemainsPersisted();
	[Fact] public override Task MultipleMessages_CompleteOne_OthersUnaffected() => base.MultipleMessages_CompleteOne_OthersUnaffected();
	[Fact] public override Task StoreInputRemainsDetached() => base.StoreInputRemainsDetached();
	[Fact] public override Task ReturnedSnapshotsRemainDetached() => base.ReturnedSnapshotsRemainDetached();
	[Fact] public override Task SimultaneousAdmissionHasOneWinner() => base.SimultaneousAdmissionHasOneWinner();
	[Fact] public override Task AdministrativeChangesInvalidateOldAuthority() => base.AdministrativeChangesInvalidateOldAuthority();
	[Fact] public override Task ProgressAndReleaseRespectCurrentAuthority() => base.ProgressAndReleaseRespectCurrentAuthority();
	[Fact] public override Task CanceledOperationsLeaveAuthorityUnchanged() => base.CanceledOperationsLeaveAuthorityUnchanged();
	[Fact] public override Task RenewalExtendsCurrentOwnership() => base.RenewalExtendsCurrentOwnership();
	[Fact] public override Task ExpiredAuthorityCannotRenewOrProgress() => base.ExpiredAuthorityCannotRenewOrProgress();
	[Fact] public override Task ReleasedAndForeignAuthorityCannotRenewSuccessor() => base.ReleasedAndForeignAuthorityCannotRenewSuccessor();
	[Fact] public override Task AdministrativeMutationInvalidatesRenewal() => base.AdministrativeMutationInvalidatesRenewal();
	[Fact] public override Task RenewalPreservesProgressOnceIncludingAfterCommit() => base.RenewalPreservesProgressOnceIncludingAfterCommit();
	[Fact] public override Task CanceledRenewalLeavesOwnershipUnchanged() => base.CanceledRenewalLeavesOwnershipUnchanged();
	[Fact] public override Task ConformanceSuite_ShouldWireEveryArm() => base.ConformanceSuite_ShouldWireEveryArm();

	[Fact]
	public async Task DetectRenewalThatDoesNotExtendTheGrant()
	{
		_store.IgnoreExtension = true;
		var error = await Should.ThrowAsync<TestFixtureAssertionException>(() => base.RenewalExtendsCurrentOwnership());
		error.Message.ShouldContain("Current ownership was not renewed");
	}

	[Fact]
	public async Task DetectRenewalThatResetsProgress()
	{
		_store.ResetProgressOnRenewal = true;
		var error = await Should.ThrowAsync<TestFixtureAssertionException>(() => base.RenewalPreservesProgressOnceIncludingAfterCommit());
		error.Message.ShouldContain("one-progress limit");
	}

	[Fact]
	public async Task DetectResurrectionOfExpiredOwnership()
	{
		_store.IgnoreExpiry = true;
		var error = await Should.ThrowAsync<TestFixtureAssertionException>(() => base.ExpiredAuthorityCannotRenewOrProgress());
		error.Message.ShouldContain("Expired ownership was resurrected");
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	[InlineData(4)]
	[InlineData(5)]
	public async Task DetectEachIgnoredBearerField(int ignoredField)
	{
		_store.IgnoredBearerField = ignoredField;
		var error = await Should.ThrowAsync<TestFixtureAssertionException>(() => base.ReleasedAndForeignAuthorityCannotRenewSuccessor());
		error.Message.ShouldContain("Foreign or released authority renewed a successor");
	}

	[Fact]
	public async Task DetectAdministrativeReplacementThatRetainsAuthority()
	{
		_store.RetainAuthorityOnReplacement = true;
		var error = await Should.ThrowAsync<TestFixtureAssertionException>(() => base.AdministrativeMutationInvalidatesRenewal());
		error.Message.ShouldContain("Replacement retained old renewal authority");
	}

	[Fact]
	public async Task DetectCanceledRenewalThatExtendsBeforeThrowing()
	{
		_store.ExtendBeforeCancellation = true;
		var error = await Should.ThrowAsync<TestFixtureAssertionException>(() => base.CanceledRenewalLeavesOwnershipUnchanged());
		error.Message.ShouldContain("Canceled renewal extended the original grant");
	}

	private sealed class ExpiringAuthority : IScheduleExecutionLeaseRenewal
	{
		private sealed record Entry(IScheduledMessage Message, Guid Generation, ScheduleExecutionLease? Owner = null,
			long Expiry = 0, bool Committed = false);
		private readonly System.Threading.Lock _gate = new();
		private readonly Guid _storeId = Guid.NewGuid();
		private readonly Dictionary<Guid, Entry> _entries = [];
		private long _ticks;
		public bool IgnoreExtension { get; set; }
		public bool ResetProgressOnRenewal { get; set; }
		public bool IgnoreExpiry { get; set; }
		public int IgnoredBearerField { get; set; }
		public bool RetainAuthorityOnReplacement { get; set; }
		public bool ExtendBeforeCancellation { get; set; }
		public TimeSpan LeaseDuration => TimeSpan.FromSeconds(3);

		public void Reset()
		{
			lock (_gate) { _entries.Clear(); _ticks = 0; }
		}

		public void Advance(TimeSpan duration)
		{
			lock (_gate) { _ticks += duration.Ticks; }
		}

		private static IScheduledMessage Copy(IScheduledMessage message) => new ScheduleExecutionSnapshot(Guid.NewGuid(), message).Message;

		public Task StoreAsync(IScheduledMessage message, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(message);
			lock (_gate)
			{
				cancellationToken.ThrowIfCancellationRequested();
				_entries[message.Id] = RetainAuthorityOnReplacement && _entries.TryGetValue(message.Id, out var existing)
					? existing with { Message = Copy(message) }
					: new Entry(Copy(message), Guid.NewGuid());
			}
			return Task.CompletedTask;
		}

		public Task<IEnumerable<IScheduledMessage>> GetAllAsync(CancellationToken cancellationToken)
		{
			lock (_gate)
			{
				cancellationToken.ThrowIfCancellationRequested();
				return Task.FromResult<IEnumerable<IScheduledMessage>>(_entries.Values.Select(entry => Copy(entry.Message)).ToArray());
			}
		}

		public Task CompleteAsync(Guid scheduleId, CancellationToken cancellationToken)
		{
			lock (_gate)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (_entries.TryGetValue(scheduleId, out var entry))
				{
					var message = Copy(entry.Message);
					message.Enabled = false;
					_entries[scheduleId] = new Entry(message, Guid.NewGuid());
				}
			}
			return Task.CompletedTask;
		}

		public Task<IReadOnlyList<ScheduleExecutionSnapshot>> GetExecutionSnapshotsAsync(CancellationToken cancellationToken)
		{
			lock (_gate)
			{
				cancellationToken.ThrowIfCancellationRequested();
				return Task.FromResult<IReadOnlyList<ScheduleExecutionSnapshot>>(_entries.Values
					.Select(entry => new ScheduleExecutionSnapshot(entry.Generation, entry.Message)).ToArray());
			}
		}

		public Task<ScheduleExecutionLease?> TryAcquireExecutionAsync(Guid scheduleId, Guid generation,
			DateTimeOffset occurrenceUtc, CancellationToken cancellationToken)
		{
			lock (_gate)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (!_entries.TryGetValue(scheduleId, out var entry) || !entry.Message.Enabled || entry.Generation != generation
					|| entry.Message.NextExecutionUtc != occurrenceUtc || (entry.Owner is not null && entry.Expiry > _ticks))
				{
					return Task.FromResult<ScheduleExecutionLease?>(null);
				}
				var lease = new ScheduleExecutionLease(_storeId, scheduleId, generation, occurrenceUtc, Guid.NewGuid());
				_entries[scheduleId] = entry with { Owner = lease, Expiry = _ticks + LeaseDuration.Ticks, Committed = false };
				return Task.FromResult<ScheduleExecutionLease?>(lease);
			}
		}

		public Task<bool> TryRenewExecutionAsync(ScheduleExecutionLease lease, CancellationToken cancellationToken)
		{
			lock (_gate)
			{
				if (ExtendBeforeCancellation && cancellationToken.IsCancellationRequested && Matches(lease, out var canceledEntry))
				{
					_entries[lease.ScheduleId] = canceledEntry with { Expiry = _ticks + LeaseDuration.Ticks };
				}
				cancellationToken.ThrowIfCancellationRequested();
				if (!Matches(lease, out var entry)) { return Task.FromResult(false); }
				_entries[entry.Message.Id] = entry with
				{
					Expiry = IgnoreExtension ? entry.Expiry : _ticks + LeaseDuration.Ticks,
					Committed = ResetProgressOnRenewal ? false : entry.Committed,
				};
				return Task.FromResult(true);
			}
		}

		public Task<bool> TryUpdateProgressAsync(ScheduleExecutionLease lease, ScheduleExecutionResult result, CancellationToken cancellationToken)
		{
			lock (_gate)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (!Matches(lease, out var entry) || entry.Committed) { return Task.FromResult(false); }
				var message = Copy(entry.Message);
				message.LastExecutionUtc = result.LastExecutionUtc;
				message.NextExecutionUtc = result.NextExecutionUtc;
				if (result.Disable) { message.Enabled = false; }
				_entries[entry.Message.Id] = entry with { Message = message, Committed = true };
				return Task.FromResult(true);
			}
		}

		public Task<bool> ReleaseExecutionAsync(ScheduleExecutionLease lease, CancellationToken cancellationToken)
		{
			lock (_gate)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (!Matches(lease, out var entry)) { return Task.FromResult(false); }
				_entries[entry.Message.Id] = entry with { Owner = null };
				return Task.FromResult(true);
			}
		}

		private bool Matches(ScheduleExecutionLease lease, out Entry entry)
		{
			var current = IgnoredBearerField == 2
				? _entries.Values.SingleOrDefault(item => item.Owner?.OwnerToken == lease.OwnerToken)
				: _entries.GetValueOrDefault(lease.ScheduleId);
			if (current?.Owner is { } owner
				&& (IgnoredBearerField == 1 || lease.StoreId == owner.StoreId)
				&& (IgnoredBearerField == 2 || lease.ScheduleId == owner.ScheduleId)
				&& (IgnoredBearerField == 3 || lease.Generation == owner.Generation)
				&& (IgnoredBearerField == 4 || lease.OccurrenceUtc == owner.OccurrenceUtc)
				&& (IgnoredBearerField == 5 || lease.OwnerToken == owner.OwnerToken)
				&& (IgnoreExpiry || current.Expiry > _ticks))
			{
				entry = current;
				return true;
			}
			entry = null!;
			return false;
		}
	}
}
