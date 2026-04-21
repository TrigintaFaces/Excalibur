// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;
using Excalibur.Testing.Conformance.DependencyInjection;

using FakeItEasy;

namespace Excalibur.Tests.MinimalWiring;

/// <summary>
/// Marker type for <c>AddEventBindings(cfg =&gt; cfg.FromTransport("..."))</c>.
/// </summary>
public sealed class AddEventBindingsMarker { }

/// <summary>
/// Regression pin for S790 FIX 4 (commit <c>1ccfa1da6</c>):
/// <see cref="Microsoft.Extensions.DependencyInjection.ServiceCollectionTransportExtensions.AddEventBindings(IServiceCollection, Action{IBindingConfigBuilder})"/>
/// must defer <c>ITransportRegistry</c> resolution from registration time to
/// <c>ValidateOnStart</c> via <c>BindingRegistrationValidator</c>, and emit a
/// <b>named-sibling error</b> when a binding references a transport that is not registered
/// at host-start time.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bucket B canonical exemplar</b> per COMPASS msg 1360 — this is the spec §5.2
/// reference row. The error message shape is:
/// </para>
/// <code>
/// "AddEventBindings references transports that are not registered: 'missing-transport'.
///  Register them via services.AddInMemoryTransport(name) / AddKafkaTransport(name, ...) /
///  AddRabbitMQTransport(name, ...) / AddAzureServiceBusTransport(name, ...) / etc.
///  before building the host."
/// </code>
/// <para>
/// The Isolation gate matches on <c>"AddEventBindings"</c> (case-insensitive substring) —
/// guaranteed to appear in both current DI surface text and any future curated message.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "MINIMAL-WIRING")]
public sealed class AddEventBindingsMinimalWiringConformanceTests
	: MinimalWiringConformanceTestKit<AddEventBindingsMarker>
{
	private const string MissingTransportName = "conformance-missing-transport";

	/// <inheritdoc />
	protected override Action<IServiceCollection> Invoke =>
		static services => services.AddEventBindings(cfg =>
			cfg.FromTransport(MissingTransportName).RouteName("NonexistentMessage"));

	/// <inheritdoc />
	protected override MinimalWiringBucket Bucket => MinimalWiringBucket.ExplicitPrerequisite;

	/// <inheritdoc />
	/// <remarks>
	/// The curated validator message (<c>BindingRegistrationValidator.Validate</c>) begins
	/// with the literal prefix <c>"AddEventBindings references transports that are not
	/// registered"</c>. The harness only needs a case-insensitive substring match — the
	/// extension name gives an unambiguous fingerprint.
	/// </remarks>
	protected override string ExpectedPrerequisiteMessageFragment => "AddEventBindings";

	/// <inheritdoc />
	/// <remarks>
	/// Bucket B Override gate: pre-register a fake <see cref="ITransportRegistry"/> that
	/// reports the required transport name as resolved, satisfying the
	/// <see cref="BindingRegistrationValidator"/> pre-condition. This verifies the extension
	/// succeeds once the consumer registers the transport the binding references.
	/// </remarks>
	protected override Action<IServiceCollection>? PreRegisterOverride =>
		static services =>
		{
			var fakeRegistry = A.Fake<ITransportRegistry>();
			// Make the missing transport appear registered by returning a non-null adapter
			// for the named transport.
			var fakeAdapter = A.Fake<ITransportAdapter>();
			A.CallTo(() => fakeRegistry.GetTransportAdapter(MissingTransportName)).Returns(fakeAdapter);
			services.AddSingleton(fakeRegistry);
		};

	/// <summary>Bucket B isolation gate — ValidateOnStart fails with named-sibling error.</summary>
	[Fact]
	public void Gate_Isolation() => ExecuteIsolationGate();

	/// <summary>Bucket B idempotence gate — with transport stub-supplied, second invocation is no-op.</summary>
	[Fact]
	public void Gate_Idempotence() => ExecuteIdempotenceGate();

	/// <summary>Bucket B override gate — with registered transport, extension composes cleanly.</summary>
	[Fact]
	public void Gate_Override() => ExecuteOverrideGate();
}
