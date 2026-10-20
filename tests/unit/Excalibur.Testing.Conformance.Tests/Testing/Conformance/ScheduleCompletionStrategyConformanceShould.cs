// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery;
using Excalibur.Testing;
using Excalibur.Testing.Conformance;

namespace Excalibur.Tests.Testing.Conformance;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
public sealed class ScheduleCompletionStrategyConformanceShould
{
	public static IEnumerable<TheoryDataRow<string, string>> CompletionStrategies()
	{
		foreach (var strategy in new[] { "retain", "delete", "archive", "normalize-retain" })
		{
			foreach (var arm in new[] { "disable", "repeat", "historical-retention-name", "unrelated" })
			{
				yield return new(strategy, arm);
			}
		}
	}

	[Theory]
	[MemberData(nameof(CompletionStrategies))]
	public async Task AcceptEachPublicCompletionStrategyWithoutDemandingRetention(string strategy, string arm)
	{
		var probe = new CompletionProbe(strategy);
		await InvokeAsync(probe, arm);
		var inventory = (await probe.Store.GetAllAsync(CancellationToken.None)).ToArray();
		inventory.Where(row => probe.Store.CompletedIds.Contains(row.Id)).ShouldAllBe(static row => !row.Enabled);
		var unrelated = inventory.Single(row => row.Id == CompletionStore.SentinelId);
		RowImage.Read(unrelated).ShouldBe(probe.Store.Originals[CompletionStore.SentinelId]);
		if (strategy == "normalize-retain")
		{
			probe.Store.NormalizationObserved.ShouldBeTrue("the positive control must actually normalize non-UTC input timestamps");
			inventory.ShouldAllBe(static row => row.NextExecutionUtc.HasValue && row.NextExecutionUtc.Value.Offset == TimeSpan.Zero
				&& row.LastExecutionUtc.HasValue && row.LastExecutionUtc.Value.Offset == TimeSpan.Zero);
		}
		if (strategy == "archive")
		{
			probe.Store.History.Count.ShouldBe(1, "archive moves a detached completed snapshot to real history; it is not just delete");
			var archived = probe.Store.History.Single();
			archived.ShouldBe(probe.Store.Originals[archived.Id] with { Enabled = false });
			inventory.ShouldNotContain(row => row.Id == archived.Id);
		}
		else if (strategy == "delete")
		{
			probe.Store.History.ShouldBeEmpty();
			inventory.ShouldNotContain(row => probe.Store.CompletedIds.Contains(row.Id));
		}
	}

	[Theory]
	[InlineData("retain")]
	[InlineData("delete")]
	[InlineData("archive")]
	[InlineData("normalize-retain")]
	public async Task LeaveExistingRowsUnchangedWhenCompletingAMissingIdentifier(string strategy)
	{
		var probe = new CompletionProbe(strategy);
		await probe.CompleteAsync_NonExistent_ShouldBeIdempotent();
		var inventory = (await probe.Store.GetAllAsync(CancellationToken.None)).ToArray();
		RowImage.Read(inventory.Single(row => row.Id == CompletionStore.SentinelId))
			.ShouldBe(probe.Store.Originals[CompletionStore.SentinelId]);
		probe.Store.History.ShouldBeEmpty();
		if (strategy == "normalize-retain") { probe.Store.NormalizationObserved.ShouldBeTrue(); }
	}

	[Theory]
	[InlineData("no-op", "disable")]
	[InlineData("no-op", "historical-retention-name")]
	[InlineData("reenable-on-second", "repeat")]
	[InlineData("delete-all", "unrelated")]
	[InlineData("delete-all", "missing")]
	[InlineData("corrupt-name", "disable")]
	[InlineData("corrupt-name", "repeat")]
	[InlineData("corrupt-name", "historical-retention-name")]
	[InlineData("corrupt-name", "unrelated")]
	[InlineData("corrupt-body", "disable")]
	[InlineData("corrupt-body", "repeat")]
	[InlineData("corrupt-body", "historical-retention-name")]
	[InlineData("corrupt-body", "unrelated")]
	[InlineData("disabled-before-active", "disable")]
	[InlineData("disabled-before-active", "repeat")]
	[InlineData("disabled-before-active", "historical-retention-name")]
	[InlineData("disabled-before-active", "unrelated")]
	[InlineData("corrupt-unrelated-body", "unrelated")]
	[InlineData("corrupt-unrelated-body", "missing")]
	[InlineData("alias-input-name-on-complete", "historical-retention-name")]
	[InlineData("alias-input-body-during-store", "historical-retention-name")]
	[InlineData("alias-readback-unrelated-body", "unrelated")]
	public async Task RejectCompletionFaultsInsteadOfCertifyingAMaskingSnapshot(string mutation, string arm)
	{
		var probe = new CompletionProbe("retain", mutation);
		await Should.ThrowAsync<TestFixtureAssertionException>(() => InvokeAsync(probe, arm));
	}

	[Fact]
	public void KeepTheTwelveHistoricalPublicArmNames()
	{
		var expected = new[]
		{
			nameof(ScheduleStoreConformanceTestKit.StoreAsync_ShouldPersistMessage),
			nameof(ScheduleStoreConformanceTestKit.StoreAsync_WithNullMessage_ShouldThrow),
			nameof(ScheduleStoreConformanceTestKit.StoreAsync_SameId_ShouldUpsert),
			nameof(ScheduleStoreConformanceTestKit.StoreAsync_MultipleMessages_ShouldPersistAll),
			nameof(ScheduleStoreConformanceTestKit.GetAllAsync_EmptyStore_ShouldReturnEmpty),
			nameof(ScheduleStoreConformanceTestKit.GetAllAsync_AfterStore_ShouldReturnMessage),
			nameof(ScheduleStoreConformanceTestKit.GetAllAsync_ShouldReturnAllMessages),
			nameof(ScheduleStoreConformanceTestKit.CompleteAsync_ShouldSetEnabledFalse),
			nameof(ScheduleStoreConformanceTestKit.CompleteAsync_NonExistent_ShouldBeIdempotent),
			nameof(ScheduleStoreConformanceTestKit.CompleteAsync_AlreadyCompleted_ShouldBeIdempotent),
			nameof(ScheduleStoreConformanceTestKit.StoreAsync_ThenComplete_MessageRemainsPersisted),
			nameof(ScheduleStoreConformanceTestKit.MultipleMessages_CompleteOne_OthersUnaffected)
		};
		var actual = typeof(ScheduleStoreConformanceTestKit)
			.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
			.Where(static method => method.ReturnType == typeof(Task) && method.GetParameters().Length == 0)
			.Select(static method => method.Name);
		actual.Order(StringComparer.Ordinal).ShouldBe(expected.Order(StringComparer.Ordinal));
	}

	private static Task InvokeAsync(CompletionProbe probe, string arm) => arm switch
	{
		"disable" => probe.CompleteAsync_ShouldSetEnabledFalse(),
		"repeat" => probe.CompleteAsync_AlreadyCompleted_ShouldBeIdempotent(),
		"historical-retention-name" => probe.StoreAsync_ThenComplete_MessageRemainsPersisted(),
		"unrelated" => probe.MultipleMessages_CompleteOne_OthersUnaffected(),
		"missing" => probe.CompleteAsync_NonExistent_ShouldBeIdempotent(),
		_ => throw new ArgumentOutOfRangeException(nameof(arm))
	};

	private sealed class CompletionProbe(string strategy, string? mutation = null) : ScheduleStoreConformanceTestKit
	{
		public CompletionStore Store { get; } = new(strategy, mutation);
		protected override IScheduleStore CreateStore() => Store;
		protected override Task ResetDataAsync() => Store.ResetAsync();
		protected override IScheduledMessage CreateScheduledMessage(Guid? id = null)
		{
			var message = base.CreateScheduledMessage(id);
			if (strategy == "normalize-retain")
			{
				message.NextExecutionUtc = new DateTimeOffset(3000, 1, 1, 12, 34, 56, TimeSpan.FromMinutes(330));
				message.LastExecutionUtc = new DateTimeOffset(2999, 1, 1, 12, 34, 56, TimeSpan.FromHours(-7));
			}
			return message;
		}
	}

	private sealed class CompletionStore(string strategy, string? mutation) : IScheduleStore
	{
		public static readonly Guid SentinelId = Guid.Parse("cddc5f41-92fd-49e0-8e9e-d2722ef2c532");
		private readonly List<IScheduledMessage> _active = [];
		private readonly Dictionary<Guid, int> _completionCounts = [];
		public Dictionary<Guid, RowImage> Originals { get; } = [];
		public HashSet<Guid> CompletedIds { get; } = [];
		public List<RowImage> History { get; } = [];
		public bool NormalizationObserved { get; private set; }
		public Task ResetAsync()
		{
			_active.Clear();
			_completionCounts.Clear();
			Originals.Clear();
			CompletedIds.Clear();
			History.Clear();
			NormalizationObserved = false;
			return StoreAsync(new ScheduledMessage
			{
				Id = SentinelId, MessageName = "unrelated-sentinel", MessageBody = "{\"preserve\":true}", Enabled = true,
				CorrelationId = "sentinel-correlation", CronExpression = "0 * * * *", TimeZoneId = "UTC",
				Interval = TimeSpan.FromMinutes(7), NextExecutionUtc = new DateTimeOffset(3000, 1, 1, 0, 0, 0, TimeSpan.Zero),
				TenantId = "sentinel-tenant", TraceParent = "sentinel-trace", UserId = "sentinel-user",
				LastExecutionUtc = new DateTimeOffset(2999, 1, 1, 0, 0, 0, TimeSpan.Zero),
				MissedExecutionBehavior = MissedExecutionBehavior.ExecuteLatestMissed
			}, CancellationToken.None);
		}
		public Task StoreAsync(IScheduledMessage message, CancellationToken token)
		{
			ArgumentNullException.ThrowIfNull(message);
			token.ThrowIfCancellationRequested();
			var image = RowImage.Read(message);
			if (strategy == "normalize-retain")
			{
				NormalizationObserved |= image.NextExecutionUtc?.Offset != TimeSpan.Zero || image.LastExecutionUtc?.Offset != TimeSpan.Zero;
				image = image with { NextExecutionUtc = image.NextExecutionUtc?.ToUniversalTime(), LastExecutionUtc = image.LastExecutionUtc?.ToUniversalTime() };
			}
			_active.RemoveAll(row => row.Id == image.Id);
			if (mutation is "alias-input-name-on-complete" or "alias-input-body-during-store")
			{
				if (mutation == "alias-input-body-during-store") { message.MessageBody = "corrupted-through-input-alias"; }
				_active.Add(message);
			}
			else { _active.Add(image.ToMessage()); }
			Originals[image.Id] = image;
			return Task.CompletedTask;
		}
		public Task<IEnumerable<IScheduledMessage>> GetAllAsync(CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			if (mutation == "alias-readback-unrelated-body")
			{
				return Task.FromResult<IEnumerable<IScheduledMessage>>(_active.ToArray());
			}
			return Task.FromResult<IEnumerable<IScheduledMessage>>(_active.Select(static row => RowImage.Read(row).ToMessage()).ToArray());
		}
		public Task CompleteAsync(Guid id, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			_completionCounts[id] = _completionCounts.GetValueOrDefault(id) + 1;
			CompletedIds.Add(id);
			if (mutation == "no-op") { return Task.CompletedTask; }
			if (mutation == "delete-all") { _active.Clear(); return Task.CompletedTask; }
			if (mutation == "disabled-before-active")
			{
				var index = _active.FindIndex(row => row.Id == id);
				if (index >= 0) { _active.Insert(index, (RowImage.Read(_active[index]) with { Enabled = false }).ToMessage()); }
				return Task.CompletedTask;
			}
			for (var index = _active.Count - 1; index >= 0; index--)
			{
				var row = _active[index];
				if (row.Id != id)
				{
					if (mutation is "corrupt-unrelated-body" or "alias-readback-unrelated-body") { row.MessageBody = "corrupted-unrelated-body"; }
					continue;
				}
				if (mutation == "alias-input-name-on-complete") { row.MessageName = "corrupted-through-input-alias"; }
				var completed = RowImage.Read(row) with { Enabled = mutation == "reenable-on-second" && _completionCounts[id] > 1 };
				if (mutation == "corrupt-name") { completed = completed with { MessageName = "corrupted-name" }; }
				if (mutation == "corrupt-body") { completed = completed with { MessageBody = "corrupted-body" }; }
				switch (strategy)
				{
					case "delete": _active.RemoveAt(index); break;
					case "archive": History.Add(completed); _active.RemoveAt(index); break;
					default: _active[index] = completed.ToMessage(); break;
				}
			}
			return Task.CompletedTask;
		}
	}

	private sealed record RowImage(Guid Id, string MessageName, string MessageBody, bool Enabled,
		string? CorrelationId, string CronExpression, string? TimeZoneId, TimeSpan? Interval,
		DateTimeOffset? NextExecutionUtc, string? TenantId, string? TraceParent, string? UserId,
		DateTimeOffset? LastExecutionUtc, MissedExecutionBehavior? MissedExecutionBehavior)
	{
		public static RowImage Read(IScheduledMessage row) => new(row.Id, row.MessageName, row.MessageBody, row.Enabled,
			row.CorrelationId, row.CronExpression, row.TimeZoneId, row.Interval, row.NextExecutionUtc,
			row.TenantId, row.TraceParent, row.UserId, row.LastExecutionUtc, row.MissedExecutionBehavior);
		public ScheduledMessage ToMessage() => new()
		{
			Id = Id, MessageName = MessageName, MessageBody = MessageBody, Enabled = Enabled,
			CorrelationId = CorrelationId, CronExpression = CronExpression, TimeZoneId = TimeZoneId,
			Interval = Interval, NextExecutionUtc = NextExecutionUtc, TenantId = TenantId, TraceParent = TraceParent,
			UserId = UserId, LastExecutionUtc = LastExecutionUtc, MissedExecutionBehavior = MissedExecutionBehavior
		};
	}
}
