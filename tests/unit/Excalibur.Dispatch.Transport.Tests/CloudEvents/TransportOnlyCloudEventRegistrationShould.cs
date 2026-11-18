// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Transport.Tests.CloudEvents;

/// <summary>
/// The registration recipe the published transport documentation tells a consumer to use.
/// <para>
/// This is the arm that distinguishes "the tests pass" from "the documented path works". Five
/// conformance suites use the transport-only path and were made to pass by registering a serializer in
/// the test — which is the right thing for a test harness and would quietly hide a broken recipe. A
/// consumer following the transport page does not get to edit our test setup, so the behaviour of the
/// bare recipe is asserted here on its own.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.CloudEvents")]
public sealed class TransportOnlyCloudEventRegistrationShould
{
	[Fact]
	public void ReportActionablyWhenTheDocumentedTransportRecipeOmitsSerialization()
	{
		// The recipe as published: a transport's own CloudEvents registration, and nothing else.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddCloudEventsForKafka();

		using var provider = services.BuildServiceProvider();

		var ex = Should.Throw<InvalidOperationException>(
			() => provider.GetRequiredService<IKafkaCloudEventAdapter>());

		// If this ever becomes a bare "unable to resolve service" the recipe has become a puzzle
		// instead of an instruction, so the arm binds the content of the message and not just its type.
		ex.Message.ShouldContain("AddCloudEvents");
	}

	[Fact]
	public void ResolveTheAdapterWhenTheRecipeAlsoConfiguresSerialization()
	{
		// LIVENESS: the arm above is satisfied by a registration that can never resolve at all. This is
		// the completed recipe, and it must actually build the adapter.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		services.AddSingleton(TestCloudEventSerializers.Reflection);
		_ = services.AddCloudEventsForKafka();

		using var provider = services.BuildServiceProvider();

		provider.GetRequiredService<IKafkaCloudEventAdapter>().ShouldNotBeNull();
	}
}
