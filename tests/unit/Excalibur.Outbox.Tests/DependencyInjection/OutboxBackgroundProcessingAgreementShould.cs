// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Outbox.Outbox;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Outbox.Tests.DependencyInjection;

/// <summary>
/// Locks the agreement between what the outbox options REPORT about background processing and what the
/// container actually REGISTERS to do it.
/// </summary>
/// <remarks>
/// <para>
/// These two could disagree, and did. <c>EnableBackgroundProcessing</c> defaulted to <see
/// langword="true"/> on the configuration the builder route creates, while that route registered no
/// hosted service and nothing on it ever read the flag. A consumer calling
/// <c>AddOutbox(o =&gt; o.UseSqlServer(...))</c> therefore resolved options stating that background
/// processing was enabled in a host that had no drain — the options described a decision the consumer
/// had never made.
/// </para>
/// <para>
/// That is the silent class: rows are accepted into the outbox for delivery, nothing publishes them,
/// and the one surface a consumer would check to find out says it is switched on. A crash is loud; a
/// message marked for delivery that is never delivered is not.
/// </para>
/// <para>
/// THE PROPERTY THESE ARMS BIND IS AGREEMENT, NOT A VALUE. They deliberately do not assert "the default
/// is false" as a fact about a field — that would pass just as well if the registration moved somewhere
/// else and started disagreeing again. Each arm reads the report and the registration together and
/// requires them to say the same thing, in both directions.
/// </para>
/// <para>
/// A store is not registered, because these arms assert nothing about storage. The surrounding suite
/// uses this same storeless <c>AddOutbox(_ =&gt; { })</c> shape for registration-only assertions.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class OutboxBackgroundProcessingAgreementShould : UnitTestBase
{
	[Fact]
	public void ReportBackgroundProcessingOffWhenTheBuilderRouteRegistersNoDrain()
	{
		var services = new ServiceCollection();

		// No EnableBackgroundProcessing() call: the consumer has not opted in.
		_ = services.AddExcalibur(x => x.AddOutbox(_ => { }));

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<OutboxOptions>();

		var registered = services.Any(
			static d => d.ServiceType == typeof(IHostedService)
				&& (d.ImplementationType == typeof(OutboxBackgroundService)
					|| d.ImplementationFactory?.Method.ReturnType == typeof(OutboxBackgroundService)));

		registered.ShouldBeFalse(
			"the builder route registers no OutboxBackgroundService unless the consumer opts in, so "
			+ "nothing in this host drains the outbox.");

		options.EnableBackgroundProcessing.ShouldBeFalse(
			"and therefore the options must not claim background processing is enabled. A consumer "
			+ "checking this property is asking 'is something draining my outbox in this process?' -- "
			+ "answering true while no drain is registered is the silent failure this arm exists to "
			+ "prevent. Mutation that reddens this: restore the '= true' initializer on "
			+ "OutboxConfiguration.EnableBackgroundProcessing.");
	}

	[Fact]
	public void ReportBackgroundProcessingOnWhenTheBuilderRouteRegistersTheDrain()
	{
		var services = new ServiceCollection();

		_ = services.AddExcalibur(x => x.AddOutbox(o => o.EnableBackgroundProcessing()));

		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<OutboxOptions>();

		var registered = services.Any(
			static d => d.ServiceType == typeof(IHostedService)
				&& (d.ImplementationType == typeof(OutboxBackgroundService)
					|| d.ImplementationFactory?.Method.ReturnType == typeof(OutboxBackgroundService)));

		// THE OTHER DIRECTION, and it is why this is two arms rather than one. An arm that only checked
		// the off case would be satisfied by a flag hard-wired to false and a method that registered
		// nothing -- agreement by paralysis. This requires the opt-in to actually do both things.
		registered.ShouldBeTrue(
			"EnableBackgroundProcessing() must register the hosted service in the same call that sets "
			+ "the flag. One writer, one act, is what keeps the report and the registration from "
			+ "drifting apart again. Mutation that reddens this: delete the AddHostedService line from "
			+ "OutboxBuilder.EnableBackgroundProcessing().");

		options.EnableBackgroundProcessing.ShouldBeTrue(
			"and the options must report the opt-in the consumer made. Mutation that reddens this: "
			+ "delete the flag assignment from OutboxBuilder.EnableBackgroundProcessing().");
	}
}
