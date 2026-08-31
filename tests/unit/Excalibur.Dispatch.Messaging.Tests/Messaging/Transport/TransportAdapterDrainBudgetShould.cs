// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Hosting;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

/// <summary>
/// The host cancels the shutdown token at <c>HostOptions.ShutdownTimeout</c>. A drain budget equal to it is
/// not inside it — the host can abandon the drain at the instant the drain is still entitled to run — so the
/// drain default must be strictly smaller than the host default.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class TransportAdapterDrainBudgetShould
{
	/// <summary>
	/// Read from <see cref="HostOptions" /> rather than hard-coded, so that if Microsoft changes the host
	/// default this assertion tracks it instead of silently comparing against a stale number.
	/// </summary>
	private static readonly TimeSpan HostShutdownDefault = new HostOptions().ShutdownTimeout;

	[Fact]
	public void DefaultToADrainBudgetStrictlyInsideTheHostShutdownBudget()
	{
		var drain = new TransportAdapterHostedServiceOptions().DrainTimeout;

		drain.ShouldBeLessThan(HostShutdownDefault);
	}

	/// <summary>
	/// Liveness arm: strictly-less is trivially satisfied by a drain budget of nothing. The margin must be
	/// small enough that the drain remains useful — it is a fraction of the host budget, not a token value.
	/// </summary>
	[Fact]
	public void LeaveADrainBudgetLargeEnoughToBeUseful()
	{
		var drain = new TransportAdapterHostedServiceOptions().DrainTimeout;

		drain.ShouldBeGreaterThan(HostShutdownDefault * 0.5);
	}

	[Fact]
	public void ExposeTheDefaultConstantAndThePropertyConsistently()
	{
		var options = new TransportAdapterHostedServiceOptions();

		options.DrainTimeoutSeconds.ShouldBe(TransportAdapterHostedServiceOptions.DefaultDrainTimeoutSeconds);
		options.DrainTimeout.ShouldBe(TimeSpan.FromSeconds(options.DrainTimeoutSeconds));
	}
}
