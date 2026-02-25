// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Performance;

namespace Excalibur.Dispatch.Tests.Performance;

/// <summary>
///     Tests for the <see cref="CacheFreezeStatus" /> record.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CacheFreezeStatusShould
{
	[Fact]
	public void HaveAllFrozenWhenEverythingFrozen()
	{
		var sut = new CacheFreezeStatus(
			HandlerInvokerFrozen: true,
			HandlerRegistryFrozen: true,
			HandlerActivatorFrozen: true,
			ResultFactoryFrozen: true,
			MiddlewareEvaluatorFrozen: true,
			FrozenAt: DateTimeOffset.UtcNow);

		sut.AllFrozen.ShouldBeTrue();
	}

	[Fact]
	public void NotHaveAllFrozenWhenPartiallyFrozen()
	{
		var sut = new CacheFreezeStatus(
			HandlerInvokerFrozen: true,
			HandlerRegistryFrozen: false,
			HandlerActivatorFrozen: true,
			ResultFactoryFrozen: true,
			MiddlewareEvaluatorFrozen: true,
			FrozenAt: DateTimeOffset.UtcNow);

		sut.AllFrozen.ShouldBeFalse();
	}

	[Fact]
	public void HaveUnfrozenStaticInstance()
	{
		var unfrozen = CacheFreezeStatus.Unfrozen;

		unfrozen.HandlerInvokerFrozen.ShouldBeFalse();
		unfrozen.HandlerRegistryFrozen.ShouldBeFalse();
		unfrozen.HandlerActivatorFrozen.ShouldBeFalse();
		unfrozen.ResultFactoryFrozen.ShouldBeFalse();
		unfrozen.MiddlewareEvaluatorFrozen.ShouldBeFalse();
		unfrozen.FrozenAt.ShouldBeNull();
		unfrozen.AllFrozen.ShouldBeFalse();
	}

	[Fact]
	public void SupportValueEquality()
	{
		var a = CacheFreezeStatus.Unfrozen;
		var b = new CacheFreezeStatus(false, false, false, false, false, null);

		a.ShouldBe(b);
	}

	[Fact]
	public void TrackFrozenAtTimestamp()
	{
		var frozenAt = new DateTimeOffset(2026, 2, 13, 10, 0, 0, TimeSpan.Zero);
		var sut = new CacheFreezeStatus(true, true, true, true, true, frozenAt);

		sut.FrozenAt.ShouldBe(frozenAt);
	}
}
