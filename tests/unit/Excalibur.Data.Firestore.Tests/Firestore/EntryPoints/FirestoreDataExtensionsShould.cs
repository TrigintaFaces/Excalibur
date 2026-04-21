// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore;

namespace Excalibur.Data.Tests.Firestore.EntryPoints;

/// <summary>
/// Unit tests for <see cref="FirestoreServiceCollectionExtensions.AddExcaliburFirestore(IServiceCollection, Action{IFirestoreDataBuilder})"/>.
/// Validates null guards, fluent chaining, configure invocation, and options registration
/// for the <c>Action&lt;IFirestoreDataBuilder&gt;</c> entry point.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "Firestore")]
public sealed class FirestoreDataExtensionsShould : UnitTestBase
{
    [Fact]
    public void AddExcaliburFirestore_ThrowWhenServicesIsNull()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            services.AddExcaliburFirestore(fs =>
                fs.ProjectId("test-project")));
    }

    [Fact]
    public void AddExcaliburFirestore_ThrowWhenConfigureIsNull()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            services.AddExcaliburFirestore((Action<IFirestoreDataBuilder>)null!));
    }

    [Fact]
    public void AddExcaliburFirestore_ReturnSameServiceCollectionForChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddExcaliburFirestore(fs =>
            fs.ProjectId("test-project"));

        // Assert
        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddExcaliburFirestore_InvokeConfigureAction()
    {
        // Arrange
        var services = new ServiceCollection();
        var configureInvoked = false;

        // Act
        services.AddExcaliburFirestore(fs =>
        {
            fs.ProjectId("test-project");
            configureInvoked = true;
        });

        // Assert
        configureInvoked.ShouldBeTrue();
    }

    [Fact]
    public void AddExcaliburFirestore_RegisterFirestoreOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddExcaliburFirestore(fs =>
            fs.ProjectId("test-project"));

        // Assert
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(IConfigureOptions<FirestoreOptions>));
    }

    [Fact]
    public void AddExcaliburFirestore_ConfiguresCollectionNameViaBuilder()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddExcaliburFirestore(fs => fs
            .ProjectId("test-project")
            .CollectionName("my-data"));

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<FirestoreOptions>>();
        options.Value.DefaultCollection.ShouldBe("my-data");
    }
}
