// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Amazon.SQS;

using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Transport;

/// <summary>
/// Regression lock (bead rlskyu, P1) proving that <see cref="AwsSqsMessageBus"/> is actually
/// DI-resolvable end-to-end via the public <c>AddAwsSqsTransport</c> registration path, not merely
/// advertised by a service descriptor.
/// </summary>
/// <remarks>
/// Named registrations expose keyed disposable services. These controls exercise constructor activation
/// with consumer-supplied logging and serialization, without making an AWS request.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
[Trait("Pattern", "DependencyInjection")]
public sealed class AwsSqsMessageBusDiResolutionShould : UnitTestBase
{
	private const string ValidRegion = "us-east-1";

	[Fact]
	public async Task ResolveAwsSqsMessageBus_FromPublicAddAwsSqsTransportPath()
	{
		// A keyed external client isolates bus activation from SDK credential resolution.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddPluggableSerialization();
		_ = services.AddKeyedSingleton<IAmazonSQS>(AwsSqsTransportServiceCollectionExtensions.DefaultTransportName, A.Fake<IAmazonSQS>());
		_ = services.AddAwsSqsTransport(sqs => sqs.UseRegion(ValidRegion));

		await using var provider = services.BuildServiceProvider();

		// The default overload uses the same keyed registration contract as a named transport.
		AwsSqsMessageBus? bus = null;
		Should.NotThrow(() => bus = provider.GetRequiredKeyedService<AwsSqsMessageBus>(AwsSqsTransportServiceCollectionExtensions.DefaultTransportName));
		_ = bus.ShouldNotBeNull();
	}

	[Fact]
	public async Task ResolveAwsSqsMessageBus_FromNamedAddAwsSqsTransportPath()
	{
		// Arrange — same public path via the named overload (fake IAmazonSQS as above).
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddPluggableSerialization();
		_ = services.AddKeyedSingleton<IAmazonSQS>("orders", A.Fake<IAmazonSQS>());
		_ = services.AddAwsSqsTransport("orders", sqs => sqs.UseRegion(ValidRegion));

		await using var provider = services.BuildServiceProvider();

		// Act & Assert
		AwsSqsMessageBus? bus = null;
		Should.NotThrow(() => bus = provider.GetRequiredKeyedService<AwsSqsMessageBus>("orders"));
		_ = bus.ShouldNotBeNull();
	}
}
