// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.DependencyInjection;
using Excalibur.Inbox.DynamoDb;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Unit tests for <see cref="InboxBuilderDynamoDbExtensions"/>.
/// </summary>
/// <remarks>
/// Sprint 514 (S514.4): DynamoDB unit tests.
/// Phase C rewire: Updated from AddDynamoDbInboxStore to AddExcaliburInbox(inbox =&gt; inbox.UseDynamoDb(...)).
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait(TraitNames.Feature, TestFeatures.DependencyInjection)]
public sealed class DynamoDbInboxExtensionsShould
{
	#region UseDynamoDb Builder Tests

	[Fact]
	public void UseDynamoDb_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IInboxBuilder)null!).UseDynamoDb(db => db.ServiceUrl("http://localhost:8000")));
	}

	[Fact]
	public void UseDynamoDb_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddExcaliburInbox(inbox =>
				inbox.UseDynamoDb((Action<IDynamoDBInboxBuilder>)null!)));
	}

	[Fact]
	public void UseDynamoDb_RegistersDynamoDbInboxStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburInbox(inbox =>
			inbox.UseDynamoDb(db => db.ServiceUrl("http://localhost:8000")));

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(DynamoDbInboxStore));
	}

	[Fact]
	public void UseDynamoDb_ReturnsBuilderForFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		IInboxBuilder? capturedBuilder = null;

		// Act
		services.AddExcaliburInbox(inbox =>
		{
			var result = inbox.UseDynamoDb(db => db.ServiceUrl("http://localhost:8000"));
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
		typeof(InboxBuilderDynamoDbExtensions).IsAbstract.ShouldBeTrue();
		typeof(InboxBuilderDynamoDbExtensions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void IsPublic()
	{
		// Assert
		typeof(InboxBuilderDynamoDbExtensions).IsPublic.ShouldBeTrue();
	}

	#endregion
}
