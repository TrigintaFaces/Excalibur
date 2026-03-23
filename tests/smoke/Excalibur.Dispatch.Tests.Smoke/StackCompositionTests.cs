// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Security;

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

		// Act -- register event sourcing (includes AddDispatch internally)
		var regException = Record.Exception(() => services.AddExcaliburEventSourcing());
		regException.ShouldBeNull("AddExcaliburEventSourcing() registration should not throw");

		// Assert -- build provider and resolve key services
		using var provider = services.BuildServiceProvider();

		var dispatcher = provider.GetService<IDispatcher>();
		dispatcher.ShouldNotBeNull("IDispatcher should be resolvable from Event Sourcing stack");

		var snapshotStrategy = provider.GetService<Excalibur.EventSourcing.Abstractions.ISnapshotStrategy>();
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
	/// Note: AddDispatchValidation() is excluded due to missing IValidationService registration
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
			services.AddExcaliburEventSourcing();
			services.AddRabbitMQTransport(rmq => rmq.HostName("localhost"));
			services.AddPollyResilience();
		});
		regException.ShouldBeNull("Full CQRS stack registration should not throw");

		// Assert -- provider builds without DI errors
		var buildException = Record.Exception(() =>
		{
			using var provider = services.BuildServiceProvider();

			var dispatcher = provider.GetService<IDispatcher>();
			dispatcher.ShouldNotBeNull("IDispatcher should be resolvable from Full CQRS stack");
		});
		buildException.ShouldBeNull("Full CQRS stack ServiceProvider should build and resolve without errors");
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
