// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authorization;

using Microsoft.AspNetCore.Authorization;

namespace Excalibur.Tests.A3.Authorization;

/// <summary>
/// Unit tests for <see cref="GrantsAuthorizationRequirement"/> public class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authorization")]
public sealed class GrantsAuthorizationRequirementShould
{
	[Fact]
	public void ImplementIAuthorizationRequirement()
	{
		// Arrange
		var requirement = new GrantsAuthorizationRequirement("activity", ["resource"]);

		// Assert
		requirement.ShouldBeAssignableTo<IAuthorizationRequirement>();
	}

	[Fact]
	public void BePublic()
	{
		// Assert
		typeof(GrantsAuthorizationRequirement).IsPublic.ShouldBeTrue();
	}

	[Fact]
	public void StoreActivityName()
	{
		// Arrange
		var requirement = new GrantsAuthorizationRequirement("TestActivity", ["resource"]);

		// Assert
		requirement.ActivityName.ShouldBe("TestActivity");
	}

	[Fact]
	public void StoreResourceTypes()
	{
		// Arrange
		var resourceTypes = new[] { "Type1", "Type2" };
		var requirement = new GrantsAuthorizationRequirement("activity", resourceTypes);

		// Assert
		requirement.ResourceTypes.ShouldBe(resourceTypes);
	}

	[Fact]
	public void StoreResourceId()
	{
		// Arrange
		var requirement = new GrantsAuthorizationRequirement("activity", ["resource"], "resource-123");

		// Assert
		requirement.ResourceId.ShouldBe("resource-123");
	}

	[Fact]
	public void AllowNullResourceId()
	{
		// Arrange
		var requirement = new GrantsAuthorizationRequirement("activity", ["resource"]);

		// Assert
		requirement.ResourceId.ShouldBeNull();
	}

	[Fact]
	public void HandleEmptyResourceTypes()
	{
		// Arrange
		var requirement = new GrantsAuthorizationRequirement("activity", []);

		// Assert
		requirement.ResourceTypes.ShouldBeEmpty();
	}

	[Fact]
	public void HandleMultipleResourceTypes()
	{
		// Arrange
		var resourceTypes = new[] { "Document", "Report", "Image", "Video" };
		var requirement = new GrantsAuthorizationRequirement("activity", resourceTypes);

		// Assert
		requirement.ResourceTypes.Length.ShouldBe(4);
		requirement.ResourceTypes.ShouldContain("Document");
		requirement.ResourceTypes.ShouldContain("Video");
	}
}
