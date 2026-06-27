// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
/// <para>
/// <b>Bug under lock</b>: <c>AddAwsSqsTransport</c> registered <see cref="AwsSqsMessageBus"/>
/// (<c>TryAddSingleton&lt;AwsSqsMessageBus&gt;()</c>) but its constructor required a concrete
/// <see cref="AwsSqsOptions"/> that nothing registered, so the advertised public path
/// (<c>AddAwsSqsTransport(...)</c> ⇒ <c>BuildServiceProvider()</c> ⇒
/// <c>GetRequiredService&lt;AwsSqsMessageBus&gt;()</c>) threw at runtime on the missing dependency.
/// The fix (a) changed the ctor to take <c>IOptions&lt;AwsSqsOptions&gt;</c> and (b) registered the
/// options via <c>AddOptions&lt;AwsSqsOptions&gt;().Configure(...)</c> in the extension
/// (<c>AwsSqsTransportServiceCollectionExtensions.cs</c> lines 124-137).
/// </para>
/// <para>
/// <b>Non-vacuity (RED pre-fix)</b>: on the pre-fix code the only resolvable constructor required a
/// concrete <see cref="AwsSqsOptions"/> service that was never registered, so
/// <c>GetRequiredService&lt;AwsSqsMessageBus&gt;()</c> throws (no satisfiable constructor) ⇒ <b>RED</b>;
/// on the fixed code <c>IOptions&lt;AwsSqsOptions&gt;</c> resolves and the bus is constructed ⇒
/// <b>GREEN</b>. The production RED-proof is deferred to post-commit because the impl files
/// (<c>AwsSqsMessageBus.cs</c>, <c>AwsSqsTransportServiceCollectionExtensions.cs</c>) are reserved by
/// the rlskyu implementation lane and are not mutated here. This lock is author≠impl: it is authored
/// independently of the rlskyu fix and binds only the public surface.
/// </para>
/// <para>
/// <b>Assertion approach</b>: DI-resolution only (resolve-only, not resolve+publish). A
/// resolve+publish round-trip is NOT performed here because <c>AddAwsSqsTransport</c> registers a
/// plain <c>new AmazonSQSClient()</c> (real AWS endpoint), not a LocalStack-pointed client — driving
/// a publish through the DI-resolved bus would hit real AWS, not the <c>AwsSqsContainerFixture</c>.
/// Real-LocalStack publish behavior is already covered by the sibling i0rr4m lock
/// (<c>AwsSqsFifoMessageBusIntegrationShould</c>) via direct construction. The DI-resolution
/// assertion is therefore the core lock for rlskyu, and this stays a Unit test (no container).
/// </para>
/// <para>
/// <b>Finding (IPayloadSerializer)</b>: <c>AddAwsSqsTransport</c> does NOT register
/// <see cref="IPayloadSerializer"/> — it is supplied by the consumer's serialization registration
/// (<c>AddPluggableSerialization()</c>). The bus ctor depends on it, so the public minimal-valid
/// config must include serialization (and logging) alongside the transport. This is documented and
/// asserted by <see cref="AddAwsSqsTransport_DoesNotRegister_IPayloadSerializer_ConsumerSuppliesIt"/>
/// so the seam is explicit; it is a separate concern from the rlskyu <see cref="AwsSqsOptions"/> gap.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class AwsSqsMessageBusDiResolutionShould : UnitTestBase
{
	private const string ValidRegion = "us-east-1";

	[Fact]
	public async Task ResolveAwsSqsMessageBus_FromPublicAddAwsSqsTransportPath()
	{
		// Arrange — minimal valid public config: logging + serialization (consumer concerns)
		// plus the transport registration under test. A fake IAmazonSQS is registered first so the
		// transport's TryAddSingleton(new AmazonSQSClient()) is a no-op — this isolates the rlskyu
		// dependency (IOptions<AwsSqsOptions>) from the unrelated, environment-dependent
		// AmazonSQSClient construction (which throws without an ambient AWS region).
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddPluggableSerialization();
		_ = services.AddSingleton(A.Fake<IAmazonSQS>());
		_ = services.AddAwsSqsTransport(sqs => sqs.UseRegion(ValidRegion));

		await using var provider = services.BuildServiceProvider();

		// Act & Assert — pre-rlskyu-fix this throws (concrete AwsSqsOptions unregistered ⇒ no
		// satisfiable ctor); post-fix it resolves via IOptions<AwsSqsOptions>.
		AwsSqsMessageBus? bus = null;
		Should.NotThrow(() => bus = provider.GetRequiredService<AwsSqsMessageBus>());
		_ = bus.ShouldNotBeNull();
	}

	[Fact]
	public async Task ResolveAwsSqsMessageBus_FromNamedAddAwsSqsTransportPath()
	{
		// Arrange — same public path via the named overload (fake IAmazonSQS as above).
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddPluggableSerialization();
		_ = services.AddSingleton(A.Fake<IAmazonSQS>());
		_ = services.AddAwsSqsTransport("orders", sqs => sqs.UseRegion(ValidRegion));

		await using var provider = services.BuildServiceProvider();

		// Act & Assert
		AwsSqsMessageBus? bus = null;
		Should.NotThrow(() => bus = provider.GetRequiredService<AwsSqsMessageBus>());
		_ = bus.ShouldNotBeNull();
	}

	[Fact]
	public void AddAwsSqsTransport_DoesNotRegister_IPayloadSerializer_ConsumerSuppliesIt()
	{
		// Documents the SA finding: serialization is a consumer concern, NOT registered by the
		// transport extension. AddAwsSqsTransport alone must not contribute an IPayloadSerializer
		// descriptor — the consumer supplies it (e.g. AddPluggableSerialization()).
		var services = new ServiceCollection();
		_ = services.AddAwsSqsTransport(sqs => sqs.UseRegion(ValidRegion));

		services.Any(d => d.ServiceType == typeof(IPayloadSerializer)).ShouldBeFalse(
			"AddAwsSqsTransport must not register IPayloadSerializer; serialization is a consumer concern");
	}
}
