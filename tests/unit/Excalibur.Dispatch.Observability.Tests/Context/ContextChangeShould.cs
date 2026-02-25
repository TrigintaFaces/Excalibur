// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Unit tests for <see cref="ContextChange"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Context")]
public sealed class ContextChangeShould
{
	#region Required Property Tests

	[Fact]
	public void RequireFieldName()
	{
		// Arrange & Act
		var change = new ContextChange
		{
			FieldName = "CorrelationId",
			Stage = "PreHandler",
		};

		// Assert
		change.FieldName.ShouldBe("CorrelationId");
	}

	[Fact]
	public void RequireStage()
	{
		// Arrange & Act
		var change = new ContextChange
		{
			FieldName = "UserId",
			Stage = "PostHandler",
		};

		// Assert
		change.Stage.ShouldBe("PostHandler");
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void HaveDefaultChangeType()
	{
		// Arrange & Act
		var change = new ContextChange
		{
			FieldName = "Field1",
			Stage = "Stage1",
		};

		// Assert
		change.ChangeType.ShouldBe(ContextChangeType.Added);
	}

	[Fact]
	public void HaveNullFromValueByDefault()
	{
		// Arrange & Act
		var change = new ContextChange
		{
			FieldName = "Field2",
			Stage = "Stage2",
		};

		// Assert
		change.FromValue.ShouldBeNull();
	}

	[Fact]
	public void HaveNullToValueByDefault()
	{
		// Arrange & Act
		var change = new ContextChange
		{
			FieldName = "Field3",
			Stage = "Stage3",
		};

		// Assert
		change.ToValue.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultTimestamp()
	{
		// Arrange & Act
		var change = new ContextChange
		{
			FieldName = "Field4",
			Stage = "Stage4",
		};

		// Assert
		change.Timestamp.ShouldBe(default(DateTimeOffset));
	}

	#endregion

	#region Property Setter Tests

	[Theory]
	[InlineData(ContextChangeType.Added)]
	[InlineData(ContextChangeType.Removed)]
	[InlineData(ContextChangeType.Modified)]
	public void AllowSettingChangeType(ContextChangeType changeType)
	{
		// Arrange & Act
		var change = new ContextChange
		{
			FieldName = "TestField",
			Stage = "TestStage",
			ChangeType = changeType,
		};

		// Assert
		change.ChangeType.ShouldBe(changeType);
	}

	[Fact]
	public void AllowSettingFromValue()
	{
		// Arrange & Act
		var change = new ContextChange
		{
			FieldName = "Counter",
			Stage = "Middleware",
			FromValue = 10,
		};

		// Assert
		change.FromValue.ShouldBe(10);
	}

	[Fact]
	public void AllowSettingToValue()
	{
		// Arrange & Act
		var change = new ContextChange
		{
			FieldName = "Counter",
			Stage = "Middleware",
			ToValue = 20,
		};

		// Assert
		change.ToValue.ShouldBe(20);
	}

	[Fact]
	public void AllowSettingTimestamp()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var change = new ContextChange
		{
			FieldName = "Field5",
			Stage = "Stage5",
			Timestamp = timestamp,
		};

		// Assert
		change.Timestamp.ShouldBe(timestamp);
	}

	#endregion

	#region Complete Object Tests

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var change = new ContextChange
		{
			FieldName = "UserRole",
			ChangeType = ContextChangeType.Modified,
			FromValue = "User",
			ToValue = "Admin",
			Stage = "Authorization",
			Timestamp = timestamp,
		};

		// Assert
		change.FieldName.ShouldBe("UserRole");
		change.ChangeType.ShouldBe(ContextChangeType.Modified);
		change.FromValue.ShouldBe("User");
		change.ToValue.ShouldBe("Admin");
		change.Stage.ShouldBe("Authorization");
		change.Timestamp.ShouldBe(timestamp);
	}

	[Fact]
	public void SupportAddedChangeWithNullFromValue()
	{
		// Arrange & Act
		var change = new ContextChange
		{
			FieldName = "NewField",
			ChangeType = ContextChangeType.Added,
			FromValue = null,
			ToValue = "NewValue",
			Stage = "Handler",
		};

		// Assert
		change.ChangeType.ShouldBe(ContextChangeType.Added);
		change.FromValue.ShouldBeNull();
		change.ToValue.ShouldNotBeNull();
	}

	[Fact]
	public void SupportRemovedChangeWithNullToValue()
	{
		// Arrange & Act
		var change = new ContextChange
		{
			FieldName = "RemovedField",
			ChangeType = ContextChangeType.Removed,
			FromValue = "OldValue",
			ToValue = null,
			Stage = "Cleanup",
		};

		// Assert
		change.ChangeType.ShouldBe(ContextChangeType.Removed);
		change.FromValue.ShouldNotBeNull();
		change.ToValue.ShouldBeNull();
	}

	[Fact]
	public void SupportModifiedChangeWithBothValues()
	{
		// Arrange & Act
		var change = new ContextChange
		{
			FieldName = "ModifiedField",
			ChangeType = ContextChangeType.Modified,
			FromValue = 100,
			ToValue = 200,
			Stage = "Processing",
		};

		// Assert
		change.ChangeType.ShouldBe(ContextChangeType.Modified);
		change.FromValue.ShouldBe(100);
		change.ToValue.ShouldBe(200);
	}

	[Fact]
	public void SupportDifferentValueTypes()
	{
		// Arrange & Act - String
		var stringChange = new ContextChange
		{
			FieldName = "StringField",
			Stage = "Test",
			FromValue = "old",
			ToValue = "new",
		};

		// Assert
		stringChange.FromValue.ShouldBe("old");
		stringChange.ToValue.ShouldBe("new");

		// Arrange & Act - Integer
		var intChange = new ContextChange
		{
			FieldName = "IntField",
			Stage = "Test",
			FromValue = 1,
			ToValue = 2,
		};

		// Assert
		intChange.FromValue.ShouldBe(1);
		intChange.ToValue.ShouldBe(2);

		// Arrange & Act - Boolean
		var boolChange = new ContextChange
		{
			FieldName = "BoolField",
			Stage = "Test",
			FromValue = false,
			ToValue = true,
		};

		// Assert
		boolChange.FromValue.ShouldBe(false);
		boolChange.ToValue.ShouldBe(true);
	}

	#endregion
}
