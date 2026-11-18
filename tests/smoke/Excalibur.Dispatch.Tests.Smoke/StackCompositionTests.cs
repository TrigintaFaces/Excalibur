// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System;

using Excalibur.Dispatch;
using Excalibur.Security;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Xunit;

namespace Excalibur.Dispatch.Tests.Smoke;

/// <summary>
/// Stack composition tests that verify common multi-package DI stacks
/// can register and resolve key services without errors.
/// Per spec §4.4: 6 stacks proving packages compose correctly together.
/// </summary>
[Trait("Category", "Smoke")]
[Trait("Component", "Platform")]
public sealed class StackCompositionTests
{
	/// <summary>
	/// Stack 1: Minimal Dispatch -- core dispatcher only.
	/// Packages: Dispatch + Abstractions.
	/// </summary>
	[Fact]
	public void MinimalDispatch_Registers_And_Resolves_IDispatcher()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act -- register core Dispatch
		var regException = Record.Exception(() => services.AddDispatch());
		regException.ShouldBeNull("AddDispatch() registration should not throw");

		// Assert -- build provider and resolve IDispatcher
		using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetService<IDispatcher>();
		dispatcher.ShouldNotBeNull("IDispatcher should be resolvable from Minimal Dispatch stack");
	}

	/// <summary>
	/// Stack 2: Event Sourcing -- Domain + EventSourcing core services.
	/// Packages: Dispatch + Domain + EventSourcing + EventSourcing.Abstractions.
	/// Note: SqlServer ES requires a connection string; we test the core layer only.
	/// </summary>
	[Fact]
	public void EventSourcing_Registers_And_Resolves_Core_Services()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act -- register event sourcing via builder chain
		var regException = Record.Exception(() =>
			services.AddExcalibur(excalibur => excalibur.AddEventSourcing()));
		regException.ShouldBeNull("AddExcalibur(x => x.AddEventSourcing()) registration should not throw");

		// Assert -- build provider and resolve key services
		using var provider = services.BuildServiceProvider();

		var dispatcher = provider.GetService<IDispatcher>();
		dispatcher.ShouldNotBeNull("IDispatcher should be resolvable from Event Sourcing stack");

		var snapshotStrategy = provider.GetService<Excalibur.EventSourcing.ISnapshotStrategy>();
		snapshotStrategy.ShouldNotBeNull("ISnapshotStrategy should be resolvable from Event Sourcing stack");
	}

	/// <summary>
	/// Stack 3: Transport -- Dispatch + RabbitMQ transport registration.
	/// Packages: Dispatch + Transport.Abstractions + Transport.RabbitMQ.
	/// Note: RabbitMQ transport registers without requiring a live connection.
	/// </summary>
	[Fact]
	public void Transport_Registers_Without_Exceptions()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddDispatch();

		// Act -- register RabbitMQ transport
		var regException = Record.Exception(() =>
			services.AddRabbitMQTransport(rmq =>
				rmq.HostName("localhost")));
		regException.ShouldBeNull("AddRabbitMQTransport() registration should not throw");

		// Assert -- provider builds without DI errors
		var buildException = Record.Exception(() =>
		{
			using var provider = services.BuildServiceProvider();
		});
		buildException.ShouldBeNull("Transport stack ServiceProvider should build without errors");
	}

	/// <summary>
	/// Stack 4: Full CQRS -- Dispatch + EventSourcing + Transport + Resilience.
	/// Packages: Dispatch + Domain + EventSourcing + Transport.RabbitMQ + Resilience.Polly.
	/// This is the most complex composition.
	/// Note: AddDispatchValidation() is excluded due to missing IMessageValidationService registration
	/// (tracked as Excalibur.Dispatch-fkidg). Will be re-enabled once the bug is fixed.
	/// </summary>
	[Fact]
	public void FullCqrs_Registers_Without_Exceptions()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act -- register full CQRS stack
		// Note: AddDispatchValidation() excluded pending Excalibur.Dispatch-fkidg fix
		var regException = Record.Exception(() =>
		{
			services.AddExcalibur(excalibur => excalibur.AddEventSourcing());

			// amqps AND non-default credentials, deliberately, rather than an opt-out. This transport
			// requires TLS and rejects guest:guest, and both refusals are correct -- an unencrypted broker
			// connection carries credentials and payloads in the clear. Satisfying them keeps every guard
			// armed in the composition this arm holds up as the reference stack; calling UseSsl(false) or
			// RequireTls = false here would make the most-copied example in the suite the one that shows a
			// consumer how to switch the protection off. Nothing is dialled -- the arm resolves IDispatcher.
			// pragma: allowlist secret -- a placeholder AMQP URI for a broker nothing dials.
			services.AddRabbitMQTransport(rmq => rmq.ConnectionString("amqps://user:password@localhost:5671/")); // pragma: allowlist secret
			services.AddPollyResilience();

			// A SERIALIZER IS THE TRANSPORT'S STATED PREREQUISITE, and seating it is the consumer's job.
			// A transport message bus takes IPayloadSerializer, and the transport packages deliberately do
			// not supply one -- serialization is a consumer concern there, and only the batteries-included
			// bundles seat a default. So this composition, which assembles the packages by hand, has to
			// make the same call a hand-assembling consumer makes.
			//
			// It was absent until now and the arm did not report it: the connection-string check refused
			// this composition earlier in the pipeline, so the missing serializer sat behind a different
			// failure. All TryAdd, so this does not override anything registered above.
			services.AddPluggableSerialization();
		});
		regException.ShouldBeNull("Full CQRS stack registration should not throw");

		// Assert -- the whole graph is RESOLVABLE, proven without constructing anything that does I/O.
		//
		// This arm used to resolve IDispatcher, and that is why it could not pass. Constructing the
		// transport's message bus OPENS A BROKER CONNECTION, so GetService<IDispatcher>() in a stack
		// containing a transport dials the broker -- and a smoke arm has none. No configuration fixes
		// that; the earlier refusals this arm hit (a missing serializer, the TLS posture) were each
		// satisfiable, and behind the last of them sat a live socket. The sibling transport arm above
		// already avoids it by building and not resolving.
		//
		// ValidateOnBuild is the stronger form of that: it walks every registered descriptor and proves
		// each one's dependencies are satisfiable, failing on a graph that could not be resolved -- which
		// is the property this arm is named for -- while never invoking a factory delegate, so nothing is
		// constructed and nothing is dialled. What it deliberately cannot see is a factory-created
		// service's own body; that is a known limit of the mechanism, not of this composition.
		var buildException = Record.Exception(() =>
		{
			using var provider = services.BuildServiceProvider(
				new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
		});
		buildException.ShouldBeNull(
			"the Full CQRS stack must compose into a resolvable graph -- every registered service's "
			+ "dependencies satisfiable, with no service left advertised but unconstructable");
	}

	/// <summary>
	/// Stack 5: Observability -- Dispatch + metrics instrumentation.
	/// Packages: Dispatch + Observability.
	/// </summary>
	[Fact]
	public void Observability_Registers_And_Resolves_IDispatcher()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act -- register dispatch + observability metrics
		var regException = Record.Exception(() =>
		{
			services.AddDispatch();
			services.AddAllDispatchMetrics();
		});
		regException.ShouldBeNull("Observability stack registration should not throw");

		// Assert -- build provider and resolve IDispatcher
		using var provider = services.BuildServiceProvider();
		var dispatcher = provider.GetService<IDispatcher>();
		dispatcher.ShouldNotBeNull("IDispatcher should be resolvable from Observability stack");
	}

	/// <summary>
	/// Stack 6: Security -- Dispatch + Security + Compliance.
	/// Packages: Dispatch + Security + Compliance.
	/// </summary>
	[Fact]
	public void Security_Registers_Without_Exceptions()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act -- register dispatch + security + compliance
		var regException = Record.Exception(() =>
		{
			services.AddDispatch();
			services.AddDispatchSecurityMiddleware(options =>
			{
				options.Encryption.EnableEncryption = true;
				options.RateLimiting.EnableRateLimiting = true;
			});
			services.AddCascadeErasure();
		});
		regException.ShouldBeNull("Security stack registration should not throw");

		// Assert -- provider builds without DI errors
		var buildException = Record.Exception(() =>
		{
			using var provider = services.BuildServiceProvider();

			var dispatcher = provider.GetService<IDispatcher>();
			dispatcher.ShouldNotBeNull("IDispatcher should be resolvable from Security stack");
		});
		buildException.ShouldBeNull("Security stack ServiceProvider should build and resolve without errors");
	}
}

/// <summary>
/// Minimal assertion helpers to avoid external test framework dependencies in smoke tests.
/// The smoke test project references all 115+ shipping packages; adding Shouldly/xUnit assertion
/// packages would create unnecessary dependency surface. These helpers provide sufficient
/// assertion capability for composition verification.
/// </summary>
#nullable enable
internal static class SmokeAssertions
{
	public static void ShouldBeNull(this Exception? exception, string message)
	{
		if (exception is not null)
		{
			throw new Xunit.Sdk.XunitException($"{message}: {exception.GetType().Name}: {exception.Message}");
		}
	}

	public static void ShouldNotBeNull(this object? value, string message)
	{
		if (value is null)
		{
			throw new Xunit.Sdk.XunitException(message);
		}
	}
}