// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Caching.Projections;
using Excalibur.EventSourcing.DependencyInjection;

namespace Excalibur.Caching.Tests.Projections;

/// <summary>
/// Paired behavioral tests for <see cref="EventSourcingBuilderProjectionCachingExtensions"/>
/// (S804 §804-C / bd-sdhocq A15).
/// </summary>
/// <remarks>
/// <para>
/// The <c>IEventSourcingBuilder.AddProjectionCaching()</c> bridge forwards to the now-internal
/// <c>IServiceCollection.AddExcaliburProjectionCaching()</c> aggregator one-to-one. These tests
/// exercise the bridge directly (against an <see cref="ExcaliburEventSourcingBuilder"/> instance
/// wrapping a fresh <see cref="IServiceCollection"/>) and pin the underlying registration shape
/// (Singleton <see cref="IProjectionCacheInvalidator"/>) so the bridge cannot silently drift
/// from the inner implementation — the paired-test invariant per ADR-325 §Secondary and the
/// S803 <c>bd-zqkbnq</c> regression lesson.
/// </para>
/// <para>
/// Projection caching is a sub-concern of event sourcing; the bridge attaches at
/// <see cref="IEventSourcingBuilder"/> rather than the root <c>IExcaliburBuilder</c>, per
/// ADR-321 canonical-path policy.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
[Trait("Feature", "Builders")]
public sealed class EventSourcingBuilderProjectionCachingExtensionsShould
{
	private static IEventSourcingBuilder CreateBuilder(IServiceCollection services)
		=> new ExcaliburEventSourcingBuilder(services);

	#region Null-guard

	[Fact]
	public void AddProjectionCaching_ThrowWhenBuilderIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			((IEventSourcingBuilder)null!).AddProjectionCaching());
	}

	#endregion

	#region Fluent chain

	[Fact]
	public void AddProjectionCaching_ReturnSameBuilderForFluentChain()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act
		var returned = builder.AddProjectionCaching();

		// Assert — bridge returns the same builder reference it received
		returned.ShouldBeSameAs(builder,
			"AddProjectionCaching must return the same IEventSourcingBuilder for fluent chaining.");
	}

	#endregion

	#region Registration-shape forwarding (paired 1:1 with AddExcaliburProjectionCaching)

	[Fact]
	public void AddProjectionCaching_RegisterProjectionCacheInvalidator()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act — via the bridge path
		_ = builder.AddProjectionCaching();

		// Assert — underlying contract registered (1:1 with AddExcaliburProjectionCaching).
		services.ShouldContain(
			sd => sd.ServiceType == typeof(IProjectionCacheInvalidator) &&
				sd.ImplementationType == typeof(ProjectionCacheInvalidator),
			customMessage: "IProjectionCacheInvalidator must be registered by AddProjectionCaching — the bridge must forward to AddExcaliburProjectionCaching 1:1.");
	}

	[Fact]
	public void AddProjectionCaching_RegisterInvalidatorAsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act
		_ = builder.AddProjectionCaching();

		// Assert — inner aggregator uses TryAddSingleton; the bridge must preserve that lifetime.
		var descriptor = services.First(sd => sd.ServiceType == typeof(IProjectionCacheInvalidator));
		descriptor.Lifetime.ShouldBe(
			ServiceLifetime.Singleton,
			"AddProjectionCaching must preserve Singleton lifetime from AddExcaliburProjectionCaching.");
	}

	[Fact]
	public void AddProjectionCaching_Idempotent_SecondCallDoesNotDuplicate()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act — invoke bridge twice
		_ = builder.AddProjectionCaching().AddProjectionCaching();

		// Assert — TryAdd semantics: single registration survives repeated invocation
		services.Count(sd => sd.ServiceType == typeof(IProjectionCacheInvalidator))
			.ShouldBe(1, "IProjectionCacheInvalidator must not duplicate under repeated AddProjectionCaching calls (TryAdd semantics).");
	}

	#endregion

	#region Bridge-vs-direct invariant (paired registration equivalence)

	[Fact]
	public void AddProjectionCaching_ViaBridge_MatchesDirectAddExcaliburProjectionCaching()
	{
		// Arrange — two paths that must produce the same IProjectionCacheInvalidator registration:
		//   (1) bridge:  builder.AddProjectionCaching()
		//   (2) direct:  services.AddExcaliburProjectionCaching()
		var bridgeServices = new ServiceCollection();
		_ = CreateBuilder(bridgeServices).AddProjectionCaching();

		var directServices = new ServiceCollection();
		_ = directServices.AddExcaliburProjectionCaching();

		// Act
		var bridgeDescriptor = bridgeServices.FirstOrDefault(sd => sd.ServiceType == typeof(IProjectionCacheInvalidator));
		var directDescriptor = directServices.FirstOrDefault(sd => sd.ServiceType == typeof(IProjectionCacheInvalidator));

		// Assert — both paths wire the same contract + implementation + lifetime
		bridgeDescriptor.ShouldNotBeNull();
		directDescriptor.ShouldNotBeNull();
		bridgeDescriptor.ImplementationType.ShouldBe(
			directDescriptor.ImplementationType,
			"Bridge and direct path must register the same implementation type (paired-test invariant per ADR-325 §Secondary / S803 bd-zqkbnq lesson).");
		bridgeDescriptor.Lifetime.ShouldBe(
			directDescriptor.Lifetime,
			"Bridge and direct path must register with the same lifetime.");
	}

	#endregion
}
