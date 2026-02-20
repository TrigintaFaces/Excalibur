// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Context;

/// <summary>
/// Unit tests for <see cref="ContextChange"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class ContextChangeShould : UnitTestBase
{
	[Fact]
	public void CreateWithAllRequiredProperties()
	{
		// Arrange & Act
		var change = new ContextChange
		{
			FieldName = "CorrelationId",
			ChangeType = ContextChangeType.Modified,
			FromValue = "old-correlation",
			ToValue = "new-correlation",
			Stage = "Handler",
			Timestamp = DateTimeOffset.UtcNow
		};

		// Assert
		change.FieldName.ShouldBe("CorrelationId");
		change.ChangeType.ShouldBe(ContextChangeType.Modified);
		change.FromValue.ShouldBe("old-correlation");
		change.ToValue.ShouldBe("new-correlation");
		change.Stage.ShouldBe("Handler");
	}

	[Fact]
	public void AllowNullFromValueForAddedChanges()
	{
		// Arrange & Act
		var change = new ContextChange
		{
			FieldName = "NewField",
			ChangeType = ContextChangeType.Added,
			FromValue = null,
			ToValue = "new-value",
			Stage = "Middleware",
			Timestamp = DateTimeOffset.UtcNow
		};

		// Assert
		change.FromValue.ShouldBeNull();
		change.ToValue.ShouldNotBeNull();
	}

	[Fact]
	public void AllowNullToValueForRemovedChanges()
	{
		// Arrange & Act
		var change = new ContextChange
		{
			FieldName = "OldField",
			ChangeType = ContextChangeType.Removed,
			FromValue = "old-value",
			ToValue = null,
			Stage = "Pipeline",
			Timestamp = DateTimeOffset.UtcNow
		};

		// Assert
		change.FromValue.ShouldNotBeNull();
		change.ToValue.ShouldBeNull();
	}

	[Fact]
	public void RecordAccurateTimestamp()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var change = new ContextChange
		{
			FieldName = "TestField",
			ChangeType = ContextChangeType.Modified,
			FromValue = "old",
			ToValue = "new",
			Stage = "Test",
			Timestamp = DateTimeOffset.UtcNow
		};

		var after = DateTimeOffset.UtcNow;

		// Assert
		change.Timestamp.ShouldBeGreaterThanOrEqualTo(before);
		change.Timestamp.ShouldBeLessThanOrEqualTo(after);
	}
}
