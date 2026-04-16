// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Firestore;

namespace Excalibur.Data.Tests.Firestore.EntryPoints;

/// <summary>
/// Unit tests for <see cref="EventSourcingBuilderFirestoreExtensions.UseFirestore(IEventSourcingBuilder, Action{IFirestoreEventSourcingBuilder})"/>.
/// Validates null guards, fluent chaining, configure invocation, and options registration
/// for the <c>Action&lt;IFirestoreEventSourcingBuilder&gt;</c> entry point.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "Firestore")]
public sealed class EventSourcingBuilderFirestoreExtensionsShould : UnitTestBase
{
    [Fact]
    public void UseFirestore_ThrowWhenBuilderIsNull()
    {
        // Arrange
        IEventSourcingBuilder builder = null!;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.UseFirestore(fs =>
                fs.ProjectId("test-project")));
    }

    [Fact]
    public void UseFirestore_ThrowWhenConfigureIsNull()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IEventSourcingBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.UseFirestore((Action<IFirestoreEventSourcingBuilder>)null!));
    }

    [Fact]
    public void UseFirestore_ReturnSameBuilderForChaining()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IEventSourcingBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        var result = builder.UseFirestore(fs =>
            fs.ProjectId("test-project"));

        // Assert
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void UseFirestore_InvokeConfigureAction()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IEventSourcingBuilder>();
        A.CallTo(() => builder.Services).Returns(services);
        var configureInvoked = false;

        // Act
        builder.UseFirestore(fs =>
        {
            fs.ProjectId("test-project");
            configureInvoked = true;
        });

        // Assert
        configureInvoked.ShouldBeTrue();
    }

    [Fact]
    public void UseFirestore_RegisterEventStoreOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IEventSourcingBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseFirestore(fs =>
            fs.ProjectId("test-project"));

        // Assert
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(IConfigureOptions<FirestoreEventStoreOptions>));
    }

    [Fact]
    public void UseFirestore_ConfiguresCollectionNameViaBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IEventSourcingBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseFirestore(fs => fs
            .ProjectId("test-project")
            .CollectionName("my-events"));

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<FirestoreEventStoreOptions>>();
        options.Value.EventsCollectionName.ShouldBe("my-events");
    }
}
