// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Migration;

namespace Excalibur.EventSourcing.Tests.Core.Migration;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class MigrationPlanShould
{
	[Fact]
	public void ExposeSourceAndTargetStreams()
	{
		var plan = new MigrationPlan("source-stream", "target-stream");
		plan.SourceStream.ShouldBe("source-stream");
		plan.TargetStream.ShouldBe("target-stream");
	}

	[Fact]
	public void DefaultFilterToNull()
	{
		var plan = new MigrationPlan("s", "t");
		plan.EventFilter.ShouldBeNull();
	}

	[Fact]
	public void DefaultTransformToNull()
	{
		var plan = new MigrationPlan("s", "t");
		plan.TransformFunc.ShouldBeNull();
	}

	[Fact]
	public void AcceptCustomFilter()
	{
		Func<StoredEvent, bool> filter = e => e.EventType == "MyEvent";
		var plan = new MigrationPlan("s", "t", EventFilter: filter);
		plan.EventFilter.ShouldNotBeNull();
	}

	[Fact]
	public void AcceptCustomTransform()
	{
		Func<StoredEvent, StoredEvent> transform = e => e;
		var plan = new MigrationPlan("s", "t", TransformFunc: transform);
		plan.TransformFunc.ShouldNotBeNull();
	}

	[Fact]
	public void SupportRecordEquality()
	{
		var a = new MigrationPlan("s", "t");
		var b = new MigrationPlan("s", "t");
		a.ShouldBe(b);
	}

	[Fact]
	public void SupportRecordInequality()
	{
		var a = new MigrationPlan("s1", "t1");
		var b = new MigrationPlan("s2", "t2");
		a.ShouldNotBe(b);
	}
}
