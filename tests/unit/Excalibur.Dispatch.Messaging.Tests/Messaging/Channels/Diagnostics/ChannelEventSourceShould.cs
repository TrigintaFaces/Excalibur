// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Tracing;

using Excalibur.Dispatch.Channels.Diagnostics;

namespace Excalibur.Dispatch.Tests.Messaging.Channels.Diagnostics;

/// <summary>
///     Tests for the <see cref="ChannelEventSource" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ChannelEventSourceShould
{
	[Fact]
	public void HaveSingletonInstance()
	{
		ChannelEventSource.Log.ShouldNotBeNull();
	}

	[Fact]
	public void HaveSameInstanceOnMultipleAccesses()
	{
		var first = ChannelEventSource.Log;
		var second = ChannelEventSource.Log;
		ReferenceEquals(first, second).ShouldBeTrue();
	}

	[Fact]
	public void DeriveFromEventSource()
	{
		ChannelEventSource.Log.ShouldBeAssignableTo<EventSource>();
	}

	[Fact]
	public void HaveCorrectEventSourceName()
	{
		ChannelEventSource.Log.Name.ShouldBe("Excalibur-Dispatch-Channels");
	}

	[Fact]
	public void GetChannelTypeNameForStringType()
	{
		var name = ChannelEventSource.GetChannelTypeName<string>();
		name.ShouldBe("String");
	}

	[Fact]
	public void GetChannelTypeNameForIntType()
	{
		var name = ChannelEventSource.GetChannelTypeName<int>();
		name.ShouldBe("Int32");
	}

	[Fact]
	public void ThrowForNullMetricsOnWriteChannelMetrics()
	{
		Should.Throw<ArgumentNullException>(
			() => ChannelEventSource.Log.WriteChannelMetrics("test", null!));
	}

	[Fact]
	public void NotThrowOnSpinWaitStatistics()
	{
		Should.NotThrow(
			() => ChannelEventSource.Log.SpinWaitStatistics("channel-1", 10, 1.5, false));
	}
}
