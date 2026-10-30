// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

#pragma warning disable CA1506 // Avoid excessive class coupling -- engage-tests require many DI types

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Projections;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Subscriptions;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.Tests.Core.Projections;

// AN INLINE COMPOSITION THAT CAN NEVER COMPLETE A SAVE IS REFUSED AT STARTUP.
//
// An Inline save waits for the ordered processor to reach its committed global position. Two
// compositions make that impossible: a missing global-stream reader, and a missing
// checkpoint store. Both used to surface on the FIRST WRITE -- the worst possible moment, because by
// then the events are durably committed and the only honest answer is that the read model is behind
// and will stay behind.
//
// The pairing matters as much as the refusal. A guard asserted only on its safety half is satisfied by
// a guard that rejects EVERYTHING, so each refusal here is paired with a liveness arm proving the
// legal composition still passes.
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InlineStartupRefusalShould
{
	// SAFETY: without a reader, the processor cannot observe the committed position.
	[Fact]
	public void RefuseInlineWhenTheGlobalReaderIsNotRegistered()
	{
		using var provider = Container(withGlobalStream: false, withCheckpoints: true);

		var error = Should.Throw<InvalidOperationException>(
			() => provider.GetRequiredService<InlineProjectionPrerequisiteValidator>().Validate());

		error.Message.ShouldContain("no global-stream reader is registered");
		error.Message.ShouldContain("IGlobalStreamQuery", Case.Sensitive,
			"a refusal must name the remedy, not merely the fault");
		error.Message.ShouldContain("changing projection mode does not repair the registration");
	}

	// SAFETY: without a cursor, a completed fold cannot survive a restart.
	[Fact]
	public void RefuseInlineWhenNoCheckpointStoreIsRegistered()
	{
		using var provider = Container(withGlobalStream: true, withCheckpoints: false);

		Should.Throw<InvalidOperationException>(
				() => provider.GetRequiredService<InlineProjectionPrerequisiteValidator>().Validate())
			.Message.ShouldContain("durable checkpoint store");
	}

	// LIVENESS: the legal composition passes. Without this, both arms above are satisfied by a
	// validator that refuses every container it is ever handed.
	[Fact]
	public void AcceptInlineWhenTheStoreSuppliesPositionsAndACheckpointStore()
	{
		using var provider = Container(withGlobalStream: true, withCheckpoints: true);

		Should.NotThrow(() => provider.GetRequiredService<InlineProjectionPrerequisiteValidator>().Validate());
	}

	private static ServiceProvider Container(bool withGlobalStream, bool withCheckpoints)
	{
		var services = new ServiceCollection();
		services.AddLogging();

		if (withGlobalStream)
		{
			services.AddSingleton(A.Fake<IGlobalStreamQuery>());
		}

		if (withCheckpoints)
		{
			// The durability marker is what the coordinator's own host-build decision reads, so it is
			// what the startup refusal has to read too -- the same question asked earlier.
			services.AddSingleton(A.Fake<ISubscriptionCheckpointDurability>());
			services.AddSingleton<ISubscriptionCheckpointStore>(new InMemorySubscriptionCheckpointStore());
		}

		services.AddSingleton<InlineProjectionPrerequisiteValidator>();
		return services.BuildServiceProvider();
	}
}
