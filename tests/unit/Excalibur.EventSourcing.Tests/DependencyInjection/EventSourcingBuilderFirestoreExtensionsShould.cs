// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Firestore;
using Excalibur.EventSourcing.DependencyInjection;

namespace Excalibur.EventSourcing.Tests.DependencyInjection;

/// <summary>
/// Unit tests for <see cref="EventSourcingBuilderFirestoreExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventSourcingBuilderFirestoreExtensionsShould
{
	private static IEventSourcingBuilder CreateBuilder(ServiceCollection? services = null)
	{
		var svc = services ?? new ServiceCollection();
		return new ExcaliburEventSourcingBuilder(svc);
	}

	#region UseFirestore(Action<IFirestoreEventSourcingBuilder>) Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForConfigureOverload()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IEventSourcingBuilder)null!).UseFirestore((Action<IFirestoreEventSourcingBuilder>)(_ => { })));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.UseFirestore((Action<IFirestoreEventSourcingBuilder>)null!));
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining_ConfigureOverload()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.UseFirestore(fs =>
			fs.ProjectId("test-project").CollectionName("events"));

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterEventStore_WhenCalledWithConfigureAction()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act
		builder.UseFirestore(fs =>
			fs.ProjectId("test-project").CollectionName("events"));

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IEventStore));
	}

	#endregion

	#region Fluent Chaining Tests

	[Fact]
	public void SupportFluentChaining_WithOtherBuilderMethods()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateBuilder(services);

		// Act -- verify chaining compiles and returns builder
		var result = builder
			.UseFirestore(fs =>
				fs.ProjectId("test-project").CollectionName("events"))
			.UseIntervalSnapshots(100);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion
}
