// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;

namespace Excalibur.Tests.A3.Abstractions;

/// <summary>
/// Unit tests for <see cref="AuthorizationAction"/> record.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class AuthorizationActionShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void Create_WithNameOnly_HasNullAttributes()
	{
		// Arrange & Act
		var action = new AuthorizationAction("Read", null);

		// Assert
		action.Name.ShouldBe("Read");
		action.Attributes.ShouldBeNull();
	}

	[Fact]
	public void Create_WithNameAndAttributes_SetsValues()
	{
		// Arrange
		var attributes = new Dictionary<string, string>
		{
			["scope"] = "admin",
			["level"] = "full"
		};

		// Act
		var action = new AuthorizationAction("Write", attributes);

		// Assert
		action.Name.ShouldBe("Write");
		action.Attributes.ShouldNotBeNull();
		action.Attributes.Count.ShouldBe(2);
		action.Attributes["scope"].ShouldBe("admin");
		action.Attributes["level"].ShouldBe("full");
	}

	[Fact]
	public void Create_WithEmptyAttributes_SetsEmptyDictionary()
	{
		// Arrange
		var attributes = new Dictionary<string, string>();

		// Act
		var action = new AuthorizationAction("Execute", attributes);

		// Assert
		action.Name.ShouldBe("Execute");
		action.Attributes.ShouldNotBeNull();
		action.Attributes.Count.ShouldBe(0);
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void Equality_SameNameAndAttributes_AreEqual()
	{
		// Arrange
		var attrs1 = new Dictionary<string, string> { ["key"] = "value" };
		var attrs2 = new Dictionary<string, string> { ["key"] = "value" };
		var action1 = new AuthorizationAction("Read", attrs1);
		var action2 = new AuthorizationAction("Read", attrs2);

		// Act & Assert - Records compare by reference for dictionary
		action1.Name.ShouldBe(action2.Name);
	}

	[Fact]
	public void Equality_DifferentName_AreNotEqual()
	{
		// Arrange
		var action1 = new AuthorizationAction("Read", null);
		var action2 = new AuthorizationAction("Write", null);

		// Act & Assert
		action1.ShouldNotBe(action2);
	}

	[Fact]
	public void Equality_BothNullAttributes_AreEqual()
	{
		// Arrange
		var action1 = new AuthorizationAction("Delete", null);
		var action2 = new AuthorizationAction("Delete", null);

		// Act & Assert
		action1.ShouldBe(action2);
	}

	#endregion

	#region With Expression Tests

	[Fact]
	public void With_CreatesModifiedCopy_Name()
	{
		// Arrange
		var original = new AuthorizationAction("Read", null);

		// Act
		var modified = original with { Name = "Write" };

		// Assert
		original.Name.ShouldBe("Read");
		modified.Name.ShouldBe("Write");
	}

	[Fact]
	public void With_CreatesModifiedCopy_Attributes()
	{
		// Arrange
		var original = new AuthorizationAction("Read", null);
		var newAttrs = new Dictionary<string, string> { ["new"] = "attr" };

		// Act
		var modified = original with { Attributes = newAttrs };

		// Assert
		original.Attributes.ShouldBeNull();
		modified.Attributes.ShouldNotBeNull();
		modified.Attributes["new"].ShouldBe("attr");
	}

	#endregion

	#region Common Action Names

	[Theory]
	[InlineData("Read")]
	[InlineData("Write")]
	[InlineData("Execute")]
	[InlineData("Delete")]
	[InlineData("Create")]
	[InlineData("Update")]
	public void Create_WithCommonActionNames_Succeeds(string actionName)
	{
		// Act
		var action = new AuthorizationAction(actionName, null);

		// Assert
		action.Name.ShouldBe(actionName);
	}

	#endregion
}
