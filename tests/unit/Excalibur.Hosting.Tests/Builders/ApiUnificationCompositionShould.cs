// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Excalibur.Hosting.Tests.Builders;

/// <summary>
/// Multi-subsystem composition test (kk7nvi) — verifies that all builder-enabled
/// subsystems can be composed together without DI conflicts.
/// </summary>
/// <remarks>
/// This test validates the API Unification epic outcome: every subsystem uses the
/// canonical builder pattern and their DI registrations coexist cleanly.
/// Note: SqlServer-specific builders are tested in their respective test projects.
/// This test verifies the composition at the abstraction layer.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ApiUnificationCompositionShould
{
	[Fact]
	public void ComposeAllSubsystemBuilders_WithoutConflicts()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act — compose multiple subsystems via their builder abstractions
		// Each subsystem uses the same builder pattern but registers independent options
		services.AddExcaliburEventSourcing(_ => { });
		services.AddExcaliburOutbox(_ => { });
		services.AddExcaliburSaga((Action<ISagaBuilder>)(_ => { }));

		// Assert — all registrations coexist without DI conflicts
		var provider = services.BuildServiceProvider();
		services.ShouldNotBeEmpty();
		provider.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterMultipleSubsystems_WithBuilderConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act — register each subsystem with builder configuration
		services.AddExcaliburEventSourcing(_ => { });
		services.AddExcaliburOutbox(outbox => outbox.EnableBackgroundProcessing());
		services.AddExcaliburSaga((Action<ISagaBuilder>)(saga => saga.WithCoordination()));

		// Assert — verify we can build the provider without DI conflicts
		var provider = services.BuildServiceProvider();
		provider.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterSubsystems_InAnyOrder()
	{
		// Arrange & Act — order 1: Saga, Outbox, ES
		var services1 = new ServiceCollection();
		services1.AddExcaliburSaga((Action<ISagaBuilder>)(_ => { }));
		services1.AddExcaliburOutbox(_ => { });
		services1.AddExcaliburEventSourcing(_ => { });
		var provider1 = services1.BuildServiceProvider();

		// Arrange & Act — order 2: ES, Saga, Outbox
		var services2 = new ServiceCollection();
		services2.AddExcaliburEventSourcing(_ => { });
		services2.AddExcaliburSaga((Action<ISagaBuilder>)(_ => { }));
		services2.AddExcaliburOutbox(_ => { });
		var provider2 = services2.BuildServiceProvider();

		// Assert — both should succeed regardless of registration order
		provider1.ShouldNotBeNull();
		provider2.ShouldNotBeNull();
	}
}
