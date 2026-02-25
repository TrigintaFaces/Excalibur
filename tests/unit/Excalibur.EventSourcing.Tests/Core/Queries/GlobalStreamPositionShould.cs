// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Queries;

namespace Excalibur.EventSourcing.Tests.Core.Queries;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class GlobalStreamPositionShould
{
	[Fact]
	public void ExposePositionAndTimestamp()
	{
		var ts = DateTimeOffset.UtcNow;
		var sut = new GlobalStreamPosition(42, ts);
		sut.Position.ShouldBe(42);
		sut.Timestamp.ShouldBe(ts);
	}

	[Fact]
	public void HaveStartAtPositionZero()
	{
		GlobalStreamPosition.Start.Position.ShouldBe(0);
	}

	[Fact]
	public void HaveStartAtMinTimestamp()
	{
		GlobalStreamPosition.Start.Timestamp.ShouldBe(DateTimeOffset.MinValue);
	}

	[Fact]
	public void SupportRecordEquality()
	{
		var ts = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var a = new GlobalStreamPosition(10, ts);
		var b = new GlobalStreamPosition(10, ts);
		a.ShouldBe(b);
	}

	[Fact]
	public void SupportRecordInequality()
	{
		var ts = DateTimeOffset.UtcNow;
		var a = new GlobalStreamPosition(1, ts);
		var b = new GlobalStreamPosition(2, ts);
		a.ShouldNotBe(b);
	}

	[Fact]
	public void ReturnSameSingletonForStart()
	{
		GlobalStreamPosition.Start.ShouldBeSameAs(GlobalStreamPosition.Start);
	}
}
