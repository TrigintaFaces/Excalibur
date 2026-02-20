// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Domain.Model;
using Excalibur.Testing;

namespace Excalibur.Dispatch.Testing.Tests.Fixtures;

[Trait("Category", "Unit")]
[Trait("Component", "Testing")]
public sealed class AggregateTestFixtureShould
{
	#region Given

	[Fact]
	public void AcceptGivenEventsViaParams()
	{
		var fixture = new AggregateTestFixture<TestAggregate>()
			.Given(new ItemAdded("item-1"));

		var result = fixture.When(_ => { }).Then();
		result.StateShould(a => a.Items.Contains("item-1"));
	}

	[Fact]
	public void AcceptGivenEventsViaEnumerable()
	{
		IEnumerable<IDomainEvent> events = [new ItemAdded("item-1"), new ItemAdded("item-2")];

		var fixture = new AggregateTestFixture<TestAggregate>()
			.Given(events);

		var result = fixture.When(_ => { }).Then();
		result.StateShould(a => a.Items.Count == 2);
	}

	[Fact]
	public void ThrowOnNullEnumerableGiven()
	{
		var fixture = new AggregateTestFixture<TestAggregate>();
		Should.Throw<ArgumentNullException>(() => fixture.Given((IEnumerable<IDomainEvent>)null!));
	}

	[Fact]
	public void AccumulateMultipleGivenCalls()
	{
		var result = new AggregateTestFixture<TestAggregate>()
			.Given(new ItemAdded("a"))
			.Given(new ItemAdded("b"))
			.When(_ => { })
			.Then();

		result.StateShould(a => a.Items.Count == 2);
	}

	#endregion

	#region When (sync)

	[Fact]
	public void ExecuteSyncActionOnAggregate()
	{
		var result = new AggregateTestFixture<TestAggregate>()
			.When(a => a.AddItem("new-item"))
			.Then();

		result.ShouldRaise<ItemAdded>();
		result.StateShould(a => a.Items.Contains("new-item"));
	}

	[Fact]
	public void ThrowOnNullSyncAction()
	{
		var fixture = new AggregateTestFixture<TestAggregate>();
		Should.Throw<ArgumentNullException>(() => fixture.When((Action<TestAggregate>)null!));
	}

	[Fact]
	public void CaptureExceptionFromSyncAction()
	{
		new AggregateTestFixture<TestAggregate>()
			.When(_ => throw new InvalidOperationException("test error"))
			.ShouldThrow<InvalidOperationException>();
	}

	#endregion

	#region WhenAsync

	[Fact]
	public async Task ExecuteAsyncActionOnAggregate()
	{
		var fixture = await new AggregateTestFixture<TestAggregate>()
			.WhenAsync(a =>
			{
				a.AddItem("async-item");
				return Task.CompletedTask;
			});

		var result = fixture.Then();
		result.ShouldRaise<ItemAdded>();
		result.StateShould(a => a.Items.Contains("async-item"));
	}

	[Fact]
	public async Task ThrowOnNullAsyncAction()
	{
		var fixture = new AggregateTestFixture<TestAggregate>();
		await Should.ThrowAsync<ArgumentNullException>(
			() => fixture.WhenAsync(null!));
	}

	[Fact]
	public async Task CaptureExceptionFromAsyncAction()
	{
		var fixture = await new AggregateTestFixture<TestAggregate>()
			.WhenAsync(_ => throw new InvalidOperationException("async error"));

		fixture.Then().ShouldThrow<InvalidOperationException>();
	}

	#endregion

	#region Then / ShouldRaise

	[Fact]
	public void ShouldRaisePassesWhenEventExists()
	{
		new AggregateTestFixture<TestAggregate>()
			.When(a => a.AddItem("x"))
			.Then()
			.ShouldRaise<ItemAdded>();
	}

	[Fact]
	public void ShouldRaiseThrowsWhenEventMissing()
	{
		var result = new AggregateTestFixture<TestAggregate>()
			.When(_ => { })
			.Then();

		Should.Throw<TestFixtureAssertionException>(() => result.ShouldRaise<ItemAdded>());
	}

	[Fact]
	public void ShouldRaiseWithPredicatePassesOnMatch()
	{
		new AggregateTestFixture<TestAggregate>()
			.When(a => a.AddItem("target"))
			.Then()
			.ShouldRaise<ItemAdded>(e => e.ItemId == "target");
	}

	[Fact]
	public void ShouldRaiseWithPredicateThrowsOnMismatch()
	{
		var result = new AggregateTestFixture<TestAggregate>()
			.When(a => a.AddItem("actual"))
			.Then();

		Should.Throw<TestFixtureAssertionException>(
			() => result.ShouldRaise<ItemAdded>(e => e.ItemId == "expected"));
	}

	[Fact]
	public void ShouldRaiseWithNullPredicateThrows()
	{
		var result = new AggregateTestFixture<TestAggregate>()
			.When(a => a.AddItem("x"))
			.Then();

		Should.Throw<ArgumentNullException>(() => result.ShouldRaise<ItemAdded>(null!));
	}

	[Fact]
	public void ShouldRaiseNoEventsPassesWhenEmpty()
	{
		new AggregateTestFixture<TestAggregate>()
			.When(_ => { })
			.Then()
			.ShouldRaiseNoEvents();
	}

	[Fact]
	public void ShouldRaiseNoEventsThrowsWhenEventsExist()
	{
		var result = new AggregateTestFixture<TestAggregate>()
			.When(a => a.AddItem("x"))
			.Then();

		Should.Throw<TestFixtureAssertionException>(() => result.ShouldRaiseNoEvents());
	}

	#endregion

	#region StateShould

	[Fact]
	public void StateShouldPassesWhenPredicateTrue()
	{
		new AggregateTestFixture<TestAggregate>()
			.Given(new ItemAdded("item-1"))
			.When(_ => { })
			.Then()
			.StateShould(a => a.Items.Count == 1);
	}

	[Fact]
	public void StateShouldThrowsWhenPredicateFalse()
	{
		var result = new AggregateTestFixture<TestAggregate>()
			.When(_ => { })
			.Then();

		Should.Throw<TestFixtureAssertionException>(() =>
			result.StateShould(a => a.Items.Count == 5));
	}

	[Fact]
	public void StateShouldThrowsWithCustomMessage()
	{
		var result = new AggregateTestFixture<TestAggregate>()
			.When(_ => { })
			.Then();

		var ex = Should.Throw<TestFixtureAssertionException>(() =>
			result.StateShould(_ => false, "Custom fail message"));
		ex.Message.ShouldBe("Custom fail message");
	}

	[Fact]
	public void StateShouldThrowsOnNullPredicate()
	{
		var result = new AggregateTestFixture<TestAggregate>()
			.When(_ => { })
			.Then();

		Should.Throw<ArgumentNullException>(() => result.StateShould(null!));
	}

	#endregion

	#region AssertAggregate

	[Fact]
	public void AssertAggregateExecutesAction()
	{
		var called = false;
		new AggregateTestFixture<TestAggregate>()
			.Given(new ItemAdded("item-1"))
			.When(_ => { })
			.Then()
			.AssertAggregate(a =>
			{
				a.Items.Count.ShouldBe(1);
				called = true;
			});

		called.ShouldBeTrue();
	}

	[Fact]
	public void AssertAggregateThrowsOnNullAction()
	{
		var result = new AggregateTestFixture<TestAggregate>()
			.When(_ => { })
			.Then();

		Should.Throw<ArgumentNullException>(() => result.AssertAggregate(null!));
	}

	#endregion

	#region ShouldThrow

	[Fact]
	public void ShouldThrowPassesOnCorrectExceptionType()
	{
		new AggregateTestFixture<TestAggregate>()
			.When(_ => throw new InvalidOperationException("boom"))
			.ShouldThrow<InvalidOperationException>();
	}

	[Fact]
	public void ShouldThrowWithMessagePassesOnMatch()
	{
		new AggregateTestFixture<TestAggregate>()
			.When(_ => throw new InvalidOperationException("something went wrong"))
			.ShouldThrow<InvalidOperationException>("went wrong");
	}

	[Fact]
	public void ShouldThrowFailsWhenNoException()
	{
		Should.Throw<TestFixtureAssertionException>(() =>
			new AggregateTestFixture<TestAggregate>()
				.When(_ => { })
				.ShouldThrow<InvalidOperationException>());
	}

	[Fact]
	public void ShouldThrowFailsOnWrongExceptionType()
	{
		Should.Throw<TestFixtureAssertionException>(() =>
			new AggregateTestFixture<TestAggregate>()
				.When(_ => throw new ArgumentException("wrong"))
				.ShouldThrow<InvalidOperationException>());
	}

	[Fact]
	public void ShouldThrowWithMessageFailsOnMismatch()
	{
		Should.Throw<TestFixtureAssertionException>(() =>
			new AggregateTestFixture<TestAggregate>()
				.When(_ => throw new InvalidOperationException("actual error"))
				.ShouldThrow<InvalidOperationException>("expected message"));
	}

	#endregion

	#region ShouldNotThrow

	[Fact]
	public void ShouldNotThrowPassesWhenNoException()
	{
		new AggregateTestFixture<TestAggregate>()
			.When(_ => { })
			.Then()
			.ShouldNotThrow();
	}

	[Fact]
	public void ShouldNotThrowFailsWhenExceptionExists()
	{
		var result = new AggregateTestFixture<TestAggregate>()
			.When(_ => throw new InvalidOperationException("boom"))
			.Then();

		Should.Throw<TestFixtureAssertionException>(() => result.ShouldNotThrow());
	}

	#endregion

	#region Chaining

	[Fact]
	public void SupportFluentChaining()
	{
		new AggregateTestFixture<TestAggregate>()
			.Given(new ItemAdded("existing"))
			.When(a => a.AddItem("new-item"))
			.Then()
			.ShouldRaise<ItemAdded>()
			.StateShould(a => a.Items.Contains("new-item"))
			.ShouldNotThrow();
	}

	#endregion

	#region Test doubles

	private sealed class ItemAdded : IDomainEvent
	{
		public ItemAdded(string itemId)
		{
			ItemId = itemId;
			EventId = Guid.NewGuid().ToString();
			AggregateId = "test-agg";
			Version = 0;
			OccurredAt = DateTimeOffset.UtcNow;
			EventType = nameof(ItemAdded);
		}

		public string ItemId { get; }
		public string EventId { get; set; }
		public string AggregateId { get; set; }
		public long Version { get; set; }
		public DateTimeOffset OccurredAt { get; set; }
		public string EventType { get; set; }
		public IDictionary<string, object>? Metadata { get; set; }
	}

	private sealed class TestAggregate : AggregateRoot
	{
		public List<string> Items { get; } = [];

		public void AddItem(string itemId)
		{
			RaiseEvent(new ItemAdded(itemId));
		}

		protected override void ApplyEventInternal(IDomainEvent @event)
		{
			if (@event is ItemAdded added)
			{
				Items.Add(added.ItemId);
			}
		}
	}

	#endregion
}
