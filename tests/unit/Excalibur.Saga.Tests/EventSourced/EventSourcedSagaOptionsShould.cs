// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.EventSourced;

namespace Excalibur.Saga.Tests.EventSourced;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventSourcedSagaOptionsShould
{
	[Fact]
	public void DefaultSnapshotIntervalTo50()
	{
		var sut = new EventSourcedSagaOptions();
		sut.SnapshotInterval.ShouldBe(50);
	}

	[Fact]
	public void DefaultStreamPrefixToSagaDash()
	{
		var sut = new EventSourcedSagaOptions();
		sut.StreamPrefix.ShouldBe("saga-");
	}

	[Fact]
	public void AllowSettingSnapshotInterval()
	{
		var sut = new EventSourcedSagaOptions { SnapshotInterval = 100 };
		sut.SnapshotInterval.ShouldBe(100);
	}

	[Fact]
	public void AllowDisablingSnapshotsWithZero()
	{
		var sut = new EventSourcedSagaOptions { SnapshotInterval = 0 };
		sut.SnapshotInterval.ShouldBe(0);
	}

	[Fact]
	public void AllowSettingStreamPrefix()
	{
		var sut = new EventSourcedSagaOptions { StreamPrefix = "workflow-" };
		sut.StreamPrefix.ShouldBe("workflow-");
	}
}
