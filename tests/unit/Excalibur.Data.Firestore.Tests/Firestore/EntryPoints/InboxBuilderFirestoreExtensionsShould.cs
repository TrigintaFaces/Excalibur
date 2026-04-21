// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.DependencyInjection;
using Excalibur.Inbox.Firestore;

namespace Excalibur.Data.Tests.Firestore.EntryPoints;

/// <summary>
/// Unit tests for <see cref="InboxBuilderFirestoreExtensions.UseFirestore(IInboxBuilder, Action{IFirestoreInboxBuilder})"/>.
/// Validates null guards, fluent chaining, configure invocation, and options registration
/// for the <c>Action&lt;IFirestoreInboxBuilder&gt;</c> entry point.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "Firestore")]
public sealed class InboxBuilderFirestoreExtensionsShould : UnitTestBase
{
    [Fact]
    public void UseFirestore_ThrowWhenBuilderIsNull()
    {
        // Arrange
        IInboxBuilder builder = null!;

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
        var builder = A.Fake<IInboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.UseFirestore((Action<IFirestoreInboxBuilder>)null!));
    }

    [Fact]
    public void UseFirestore_ReturnSameBuilderForChaining()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IInboxBuilder>();
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
        var builder = A.Fake<IInboxBuilder>();
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
    public void UseFirestore_RegisterInboxOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IInboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseFirestore(fs =>
            fs.ProjectId("test-project"));

        // Assert
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(IConfigureOptions<FirestoreInboxOptions>));
    }

    [Fact]
    public void UseFirestore_ConfiguresCollectionNameViaBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IInboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseFirestore(fs => fs
            .ProjectId("test-project")
            .CollectionName("my-inbox"));

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<FirestoreInboxOptions>>();
        options.Value.CollectionName.ShouldBe("my-inbox");
    }
}
