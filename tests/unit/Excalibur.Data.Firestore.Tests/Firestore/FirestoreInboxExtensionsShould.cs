// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.DependencyInjection;
using Excalibur.Inbox.Firestore;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.Tests.Firestore;

/// <summary>
/// Unit tests for <see cref="InboxBuilderFirestoreExtensions"/>.
/// </summary>
/// <remarks>
/// Sprint 515 (S515.2): Firestore unit tests.
/// Phase C rewire: Updated from AddFirestoreInboxStore(Action&lt;FirestoreInboxOptions&gt;) to
/// AddExcaliburInbox(inbox =&gt; inbox.UseFirestore(Action&lt;IFirestoreInboxBuilder&gt;)).
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Firestore")]
[Trait(TraitNames.Feature, TestFeatures.DependencyInjection)]
public sealed class FirestoreInboxExtensionsShould
{
	#region UseFirestore Builder Tests

	[Fact]
	public void UseFirestore_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IInboxBuilder)null!).UseFirestore(fs => fs.ProjectId("test-project")));
	}

	[Fact]
	public void UseFirestore_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddExcaliburInbox(inbox =>
				inbox.UseFirestore((Action<IFirestoreInboxBuilder>)null!)));
	}

	[Fact]
	public void UseFirestore_RegistersFirestoreInboxStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburInbox(inbox =>
			inbox.UseFirestore(fs => fs.ProjectId("test-project")));

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(FirestoreInboxStore));
	}

	[Fact]
	public void UseFirestore_ReturnsBuilderForFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		IInboxBuilder? capturedBuilder = null;

		// Act
		services.AddExcaliburInbox(inbox =>
		{
			var result = inbox.UseFirestore(fs => fs.ProjectId("test-project"));
			capturedBuilder = result;
		});

		// Assert
		capturedBuilder.ShouldNotBeNull();
	}

	#endregion

	#region Type Tests

	[Fact]
	public void IsStatic()
	{
		// Assert
		typeof(InboxBuilderFirestoreExtensions).IsAbstract.ShouldBeTrue();
		typeof(InboxBuilderFirestoreExtensions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(InboxBuilderFirestoreExtensions).IsPublic.ShouldBeTrue();
	}

	#endregion
}
