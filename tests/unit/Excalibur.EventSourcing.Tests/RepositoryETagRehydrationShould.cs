// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing.InMemory;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Excalibur.EventSourcing.Tests;

public sealed class RepositoryETagRehydrationShould
{
	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public async Task RestoreTheCommittedETagForConditionalSave(bool useSnapshot, bool appendTail)
	{
		ISnapshot? snapshot = null;
		var snapshots = A.Fake<ISnapshotManager>();
		A.CallTo(() => snapshots.GetLatestSnapshotAsync(A<string>._, A<CancellationToken>._))
			.ReturnsLazily(() => Task.FromResult(snapshot));
		var services = new ServiceCollection();
		_ = services.AddLogging();
		if (useSnapshot)
		{
			_ = services.AddSingleton(snapshots);
		}
		_ = services.AddExcalibur(builder => builder.AddEventSourcing(events =>
		{
			_ = events.UseInMemory();
			_ = events.RegisterEventTypesFromAssembly(typeof(RepositoryETagRehydrationShould).Assembly);
			_ = events.AddRepository<TaggedAggregate, Guid>(id => new TaggedAggregate(id));
		}));
		await using var provider = services.BuildServiceProvider();
		var repository = provider.GetRequiredService<IEventSourcedRepository<TaggedAggregate, Guid>>();
		var store = provider.GetRequiredService<IEventStore>();
		var original = new TaggedAggregate(Guid.NewGuid());
		original.Change("first");
		_ = await repository.SaveAsync(original, CancellationToken.None);
		if (useSnapshot)
		{
			snapshot = Snapshot.Create(original.Id.ToString(), original.Version,
				Encoding.UTF8.GetBytes(original.Value), original.AggregateType);
		}
		if (appendTail)
		{
			original.Change("tail");
			_ = await repository.SaveAsync(original, CancellationToken.None);
		}

		var loaded = await repository.GetByIdAsync(original.Id, CancellationToken.None);
		loaded.ShouldNotBeNull();
		loaded.ETag.ShouldNotBeNullOrWhiteSpace();
		loaded.ETag.ShouldBe(original.ETag);
		loaded.Version.ShouldBe(original.Version);
		loaded.Value.ShouldBe(appendTail ? "tail" : "first");
		loaded.SnapshotApplied.ShouldBe(useSnapshot);
		var previousETag = loaded.ETag;
		loaded.Change("accepted");
		_ = await repository.SaveAsync(loaded, previousETag, CancellationToken.None);
		loaded.ETag.ShouldNotBe(previousETag);

		var current = await repository.GetByIdAsync(original.Id, CancellationToken.None);
		current.ShouldNotBeNull();
		current.ETag.ShouldBe(loaded.ETag);
		current.Change("must not append");
		await Should.ThrowAsync<ConcurrencyException>(() =>
			repository.SaveAsync(current, previousETag, CancellationToken.None));
		var persisted = await repository.GetByIdAsync(original.Id, CancellationToken.None);
		persisted.ShouldNotBeNull();
		persisted.Value.ShouldBe("accepted");
		persisted.Version.ShouldBe(appendTail ? 3 : 2);
		var history = await store.LoadAsync(original.Id.ToString(), original.AggregateType, -1, CancellationToken.None);
		history.Count.ShouldBe(appendTail ? 3 : 2);
	}

	[MessageName("Tests.RepositoryETagChanged")]
	internal sealed record TaggedValueChanged(string Value) : DomainEvent;

	internal sealed class TaggedAggregate : AggregateRoot<Guid>
	{
		public TaggedAggregate() { }
		public TaggedAggregate(Guid id) : base(id) { }
		public string Value { get; private set; } = string.Empty;
		public bool SnapshotApplied { get; private set; }
		public void Change(string value) => RaiseEvent(new TaggedValueChanged(value));
		protected override bool ApplyEventInternal(IDomainEvent @event)
		{
			if (@event is not TaggedValueChanged changed) { return false; }
			Value = changed.Value;
			return true;
		}
		protected override void ApplySnapshot(ISnapshot snapshot)
		{
			SnapshotApplied = true;
			Value = Encoding.UTF8.GetString(snapshot.Data.Span);
		}
	}
}

