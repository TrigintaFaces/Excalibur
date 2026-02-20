// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Unit tests for <see cref="ContextMutation"/> internal class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Context")]
public sealed class ContextMutationShould
{
	#region Required Property Tests

	[Fact]
	public void RequireField()
	{
		// Arrange & Act
		var mutation = new ContextMutation
		{
			Field = "CorrelationId",
			Type = MutationType.Added,
			Stage = "PreHandler",
		};

		// Assert
		mutation.Field.ShouldBe("CorrelationId");
	}

	[Fact]
	public void RequireType()
	{
		// Arrange & Act
		var mutation = new ContextMutation
		{
			Field = "UserId",
			Type = MutationType.Modified,
			Stage = "Handler",
		};

		// Assert
		mutation.Type.ShouldBe(MutationType.Modified);
	}

	[Fact]
	public void RequireStage()
	{
		// Arrange & Act
		var mutation = new ContextMutation
		{
			Field = "RequestId",
			Type = MutationType.Removed,
			Stage = "PostHandler",
		};

		// Assert
		mutation.Stage.ShouldBe("PostHandler");
	}

	#endregion

	#region Optional Property Tests

	[Fact]
	public void HaveNullOldValueByDefault()
	{
		// Arrange & Act
		var mutation = new ContextMutation
		{
			Field = "Test",
			Type = MutationType.Added,
			Stage = "Test",
		};

		// Assert
		mutation.OldValue.ShouldBeNull();
	}

	[Fact]
	public void HaveNullNewValueByDefault()
	{
		// Arrange & Act
		var mutation = new ContextMutation
		{
			Field = "Test",
			Type = MutationType.Removed,
			Stage = "Test",
		};

		// Assert
		mutation.NewValue.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingOldValue()
	{
		// Arrange & Act
		var mutation = new ContextMutation
		{
			Field = "Counter",
			Type = MutationType.Modified,
			Stage = "Handler",
			OldValue = 10,
		};

		// Assert
		mutation.OldValue.ShouldBe(10);
	}

	[Fact]
	public void AllowSettingNewValue()
	{
		// Arrange & Act
		var mutation = new ContextMutation
		{
			Field = "Counter",
			Type = MutationType.Modified,
			Stage = "Handler",
			NewValue = 20,
		};

		// Assert
		mutation.NewValue.ShouldBe(20);
	}

	#endregion

	#region Complete Object Tests

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var mutation = new ContextMutation
		{
			Field = "UserRole",
			Type = MutationType.Modified,
			OldValue = "User",
			NewValue = "Admin",
			Stage = "Authorization",
		};

		// Assert
		mutation.Field.ShouldBe("UserRole");
		mutation.Type.ShouldBe(MutationType.Modified);
		mutation.OldValue.ShouldBe("User");
		mutation.NewValue.ShouldBe("Admin");
		mutation.Stage.ShouldBe("Authorization");
	}

	[Fact]
	public void SupportAllMutationTypes()
	{
		// Arrange & Act - Test Added
		var addedMutation = new ContextMutation
		{
			Field = "TestField1",
			Type = MutationType.Added,
			Stage = "TestStage",
		};

		// Assert
		addedMutation.Type.ShouldBe(MutationType.Added);

		// Arrange & Act - Test Removed
		var removedMutation = new ContextMutation
		{
			Field = "TestField2",
			Type = MutationType.Removed,
			Stage = "TestStage",
		};

		// Assert
		removedMutation.Type.ShouldBe(MutationType.Removed);

		// Arrange & Act - Test Modified
		var modifiedMutation = new ContextMutation
		{
			Field = "TestField3",
			Type = MutationType.Modified,
			Stage = "TestStage",
		};

		// Assert
		modifiedMutation.Type.ShouldBe(MutationType.Modified);
	}

	[Fact]
	public void SupportAddedMutationWithNullOldValue()
	{
		// Arrange & Act
		var mutation = new ContextMutation
		{
			Field = "NewField",
			Type = MutationType.Added,
			OldValue = null,
			NewValue = "NewValue",
			Stage = "Handler",
		};

		// Assert
		mutation.Type.ShouldBe(MutationType.Added);
		mutation.OldValue.ShouldBeNull();
		mutation.NewValue.ShouldNotBeNull();
	}

	[Fact]
	public void SupportRemovedMutationWithNullNewValue()
	{
		// Arrange & Act
		var mutation = new ContextMutation
		{
			Field = "RemovedField",
			Type = MutationType.Removed,
			OldValue = "OldValue",
			NewValue = null,
			Stage = "Cleanup",
		};

		// Assert
		mutation.Type.ShouldBe(MutationType.Removed);
		mutation.OldValue.ShouldNotBeNull();
		mutation.NewValue.ShouldBeNull();
	}

	[Fact]
	public void SupportModifiedMutationWithBothValues()
	{
		// Arrange & Act
		var mutation = new ContextMutation
		{
			Field = "ModifiedField",
			Type = MutationType.Modified,
			OldValue = 100,
			NewValue = 200,
			Stage = "Processing",
		};

		// Assert
		mutation.Type.ShouldBe(MutationType.Modified);
		mutation.OldValue.ShouldBe(100);
		mutation.NewValue.ShouldBe(200);
	}

	[Fact]
	public void SupportDifferentValueTypes()
	{
		// String values
		var stringMutation = new ContextMutation
		{
			Field = "StringField",
			Type = MutationType.Modified,
			Stage = "Test",
			OldValue = "old",
			NewValue = "new",
		};
		stringMutation.OldValue.ShouldBe("old");
		stringMutation.NewValue.ShouldBe("new");

		// Integer values
		var intMutation = new ContextMutation
		{
			Field = "IntField",
			Type = MutationType.Modified,
			Stage = "Test",
			OldValue = 1,
			NewValue = 2,
		};
		intMutation.OldValue.ShouldBe(1);
		intMutation.NewValue.ShouldBe(2);

		// Boolean values
		var boolMutation = new ContextMutation
		{
			Field = "BoolField",
			Type = MutationType.Modified,
			Stage = "Test",
			OldValue = false,
			NewValue = true,
		};
		boolMutation.OldValue.ShouldBe(false);
		boolMutation.NewValue.ShouldBe(true);
	}

	[Fact]
	public void BeInternal()
	{
		// Assert - ContextMutation should be internal sealed
		typeof(ContextMutation).IsNotPublic.ShouldBeTrue();
		typeof(ContextMutation).IsSealed.ShouldBeTrue();
	}

	#endregion
}
