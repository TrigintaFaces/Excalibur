// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.InMemory;
using Excalibur.Outbox.Outbox;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.InMemory.Tests.InMemory;

/// <summary>
/// Locks the poll interval an operator configures to the value the drain loop and the failure-floor
/// validators actually read.
/// </summary>
/// <remarks>
/// <para>
/// The knob and the reader were two option types with no translation between them. The builder's
/// polling-interval verb lands on <c>OutboxOptions</c>; the drain loop and every provider's
/// failure-floor validator read <c>OutboxProcessingOptions</c>, which nothing bound, so it always
/// yielded its own five-second default.
/// </para>
/// <para>
/// The second arm is the one that matters. A silently-ignored poll interval is a performance surprise; a
/// validator holding <c>FailureBackoffFloorSeconds &gt; PollingInterval</c> against an interval the system
/// does not use is a gate that passes the exact configuration it exists to reject -- a floor beneath the
/// operator's real poll interval, which re-admits a failed message on the next cycle with no backoff.
/// </para>
/// <para>
/// Resolved from a real container built by the production registration, not from hand-constructed options:
/// the defect was entirely in the wiring, so options assembled by the test would have agreed with each
/// other and proved nothing.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class OutboxPollingIntervalReachesTheDrainLoopShould
{
	private static ServiceProvider BuildProvider(TimeSpan operatorPoll, int? floorSeconds = null)
	{
		var services = new ServiceCollection();

		_ = services.AddExcaliburOutbox(outbox => outbox
			.UseInMemory()
			.WithProcessing(processing => processing.PollingInterval(operatorPoll)));

		if (floorSeconds is { } floor)
		{
			_ = services.Configure<InMemoryOutboxOptions>(o => o.FailureBackoffFloorSeconds = floor);
		}

		return services.BuildServiceProvider();
	}

	/// <summary>
	/// LIVENESS: the interval the operator set is the interval the drain loop delays on.
	/// </summary>
	[Fact]
	public void OperatorPollingInterval_IsTheValueTheDrainLoopReads()
	{
		using var provider = BuildProvider(TimeSpan.FromMilliseconds(250));

		var loopValue = provider.GetRequiredService<IOptions<OutboxProcessingOptions>>().Value.PollingInterval;

		loopValue.ShouldBe(
			TimeSpan.FromMilliseconds(250),
			"OutboxProcessingOptions.PollingInterval is what OutboxBackgroundService delays on. A five-second "
			+ "value here means the operator's setting was discarded between the builder and the loop.");
	}

	/// <summary>
	/// SAFETY: a backoff floor beneath the operator's real poll interval is refused at startup.
	/// </summary>
	/// <remarks>
	/// The floor (10s) sits above the type's own five-second default and below the operator's configured
	/// interval (60s), so this configuration is rejected only if the validator reads the operator's value.
	/// Against the unbound type it passed -- the arm is red on the defect and on nothing else.
	/// </remarks>
	[Fact]
	public async Task FloorBeneathTheOperatorPollingInterval_IsRefusedAtStartup()
	{
		using var provider = BuildProvider(TimeSpan.FromSeconds(60), floorSeconds: 10);

		var ex = await Should.ThrowAsync<OptionsValidationException>(
			() => Task.Run(() => provider.GetRequiredService<IOptions<InMemoryOutboxOptions>>().Value));

		ex.Message.ShouldContain("FailureBackoffFloorSeconds");
	}

	/// <summary>
	/// LIVENESS control: a floor above the operator's interval still starts, so the arm above is not a
	/// validator that rejects everything.
	/// </summary>
	[Fact]
	public void FloorAboveTheOperatorPollingInterval_Starts()
	{
		using var provider = BuildProvider(TimeSpan.FromSeconds(5), floorSeconds: 30);

		var options = provider.GetRequiredService<IOptions<InMemoryOutboxOptions>>().Value;

		options.FailureBackoffFloorSeconds.ShouldBe(30);
	}
}
