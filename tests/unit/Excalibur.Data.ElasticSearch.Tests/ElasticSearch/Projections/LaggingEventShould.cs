// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Projections;

namespace Excalibur.Data.Tests.ElasticSearch.Projections;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class LaggingEventShould
{
	[Fact]
	public void SetRequiredProperties()
	{
		var timestamp = DateTimeOffset.UtcNow.AddSeconds(-30);
		var age = TimeSpan.FromSeconds(30);
		var pending = new List<string> { "OrderProjection", "CustomerProjection" };

		var sut = new LaggingEvent
		{
			EventId = "evt-123",
			AggregateId = "agg-456",
			EventType = "OrderCreated",
			WriteModelTimestamp = timestamp,
			Age = age,
			PendingProjections = pending,
		};

		sut.EventId.ShouldBe("evt-123");
		sut.AggregateId.ShouldBe("agg-456");
		sut.EventType.ShouldBe("OrderCreated");
		sut.WriteModelTimestamp.ShouldBe(timestamp);
		sut.Age.ShouldBe(age);
		sut.PendingProjections.ShouldBeSameAs(pending);
	}

	[Fact]
	public void HaveNullDefaultForErrorMessages()
	{
		var sut = new LaggingEvent
		{
			EventId = "evt-1",
			AggregateId = "agg-1",
			EventType = "Test",
			WriteModelTimestamp = DateTimeOffset.UtcNow,
			Age = TimeSpan.FromSeconds(5),
			PendingProjections = ["TestProjection"],
		};

		sut.ErrorMessages.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingErrorMessages()
	{
		var errors = new List<string> { "Timeout on OrderProjection", "Mapping conflict on CustomerProjection" };
		var sut = new LaggingEvent
		{
			EventId = "evt-2",
			AggregateId = "agg-2",
			EventType = "OrderUpdated",
			WriteModelTimestamp = DateTimeOffset.UtcNow,
			Age = TimeSpan.FromMinutes(2),
			PendingProjections = ["OrderProjection"],
			ErrorMessages = errors,
		};

		sut.ErrorMessages.ShouldBeSameAs(errors);
	}
}
