// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using CloudStorageSnapshots.Archive;
using Excalibur.Dispatch;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Tests.TieredStorage;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "EventSourcing")]
public sealed class ManualArchiveRunnerShould
{
	private readonly IEventStoreArchive _archive = A.Fake<IEventStoreArchive>();
	private readonly IEventStoreArchiveReader _hot = A.Fake<IEventStoreArchiveReader>();
	private readonly IColdEventStore _cold = A.Fake<IColdEventStore>();
	private static StoredEvent Event(long version) => new($"e-{version}", "aggregate", "Order", "Created", [1], null, version, DateTimeOffset.UnixEpoch);

	private ManualArchiveRunner CreateRunner(KeyedTenantPartition tenant)
	{
		A.CallTo(() => _archive.GetArchiveCandidatesAsync(A<ArchivePolicy>._, A<int>._, A<CancellationToken>._))
			.Returns(new List<ArchiveCandidate> { new(tenant, "aggregate", "Order", 1, 2) });
		var options = A.Fake<IOptionsMonitor<ArchivePolicy>>();
		A.CallTo(() => options.CurrentValue).Returns(new ArchivePolicy { MaxAge = TimeSpan.FromDays(30) });
		return new ManualArchiveRunner(_archive, _hot, _cold, options, null, NullLogger<ManualArchiveRunner>.Instance);
	}

	[Fact]
	public async Task RejectAnotherTenantBeforeReadingHotEvents()
	{
		await Should.ThrowAsync<InvalidOperationException>(() => CreateRunner(KeyedTenantPartition.Scoped("other")).RunAsync(10, CancellationToken.None));
		A.CallTo(() => _hot.LoadArchiveEventsAsync(KeyedTenantPartition.Untenanted, A<string>._, A<string>._, A<long>._, A<CancellationToken>._)).MustNotHaveHappened();
		AssertNoWrites();
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task RefuseUnresolvedOrErasedPayloads(bool erased, bool replacement)
	{
		var first = Event(0) with { EventType = erased ? "$erased" : "Created", EventData = replacement ? [0] : null, ArchivedAt = erased ? DateTimeOffset.UnixEpoch : null };
		A.CallTo(() => _hot.LoadArchiveEventsAsync(KeyedTenantPartition.Untenanted, "aggregate", "Order", A<long>._, A<CancellationToken>._)).Returns(new List<StoredEvent> { first, Event(1) });
		await Should.ThrowAsync<InvalidOperationException>(() => CreateRunner(KeyedTenantPartition.Untenanted).RunAsync(10, CancellationToken.None));
		AssertNoWrites();
	}

	[Fact]
	public async Task SubmitOnlyNewPayloadsAndBoundHotRemovalByTheReceipt()
	{
		A.CallTo(() => _hot.LoadArchiveEventsAsync(KeyedTenantPartition.Untenanted, "aggregate", "Order", A<long>._, A<CancellationToken>._))
			.Returns(new List<StoredEvent> { Event(0) with { EventData = null, ArchivedAt = DateTimeOffset.UnixEpoch }, Event(1) });
		A.CallTo(() => _cold.WriteAsync(KeyedTenantPartition.Untenanted, "aggregate", "Order", A<IReadOnlyList<StoredEvent>>._, A<CancellationToken>._)).Returns(-1L);
		await CreateRunner(KeyedTenantPartition.Untenanted).RunAsync(10, CancellationToken.None);
		A.CallTo(() => _cold.WriteAsync(KeyedTenantPartition.Untenanted, "aggregate", "Order",
			A<IReadOnlyList<StoredEvent>>.That.Matches(events => events.Count == 1 && events[0].Version == 1 && events[0].EventData != null), A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _archive.TombstoneArchivedEventsUpToVersionAsync(A<KeyedTenantPartition>._, A<string>._, A<string>._, A<long>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	private void AssertNoWrites()
	{
		A.CallTo(() => _cold.WriteAsync(A<KeyedTenantPartition>._, A<string>._, "Order", A<IReadOnlyList<StoredEvent>>._, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => _archive.TombstoneArchivedEventsUpToVersionAsync(A<KeyedTenantPartition>._, A<string>._, A<string>._, A<long>._, A<CancellationToken>._)).MustNotHaveHappened();
	}
}
