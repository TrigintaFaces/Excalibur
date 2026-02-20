// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore.Inbox;

namespace Excalibur.Data.Tests.Firestore;

/// <summary>
/// Unit tests for <see cref="FirestoreInboxOptions"/>.
/// </summary>
/// <remarks>
/// Sprint 515 (S515.2): Firestore unit tests.
/// Tests verify inbox options defaults and validation.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Firestore")]
[Trait("Feature", "Configuration")]
public sealed class FirestoreInboxOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void HaveNullProjectIdByDefault()
	{
		// Arrange & Act
		var options = new FirestoreInboxOptions();

		// Assert
		options.ProjectId.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultCollectionName()
	{
		// Arrange & Act
		var options = new FirestoreInboxOptions();

		// Assert
		options.CollectionName.ShouldBe("inbox_messages");
	}

	[Fact]
	public void HaveNullCredentialsPathByDefault()
	{
		// Arrange & Act
		var options = new FirestoreInboxOptions();

		// Assert
		options.CredentialsPath.ShouldBeNull();
	}

	[Fact]
	public void HaveNullCredentialsJsonByDefault()
	{
		// Arrange & Act
		var options = new FirestoreInboxOptions();

		// Assert
		options.CredentialsJson.ShouldBeNull();
	}

	[Fact]
	public void HaveNullEmulatorHostByDefault()
	{
		// Arrange & Act
		var options = new FirestoreInboxOptions();

		// Assert
		options.EmulatorHost.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultTtlSeconds()
	{
		// Arrange & Act
		var options = new FirestoreInboxOptions();

		// Assert - 7 days in seconds
		options.DefaultTtlSeconds.ShouldBe(604800);
	}

	[Fact]
	public void HaveDefaultTimeoutInSeconds()
	{
		// Arrange & Act
		var options = new FirestoreInboxOptions();

		// Assert
		options.TimeoutInSeconds.ShouldBe(30);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void AllowSettingProjectId()
	{
		// Arrange
		var options = new FirestoreInboxOptions();

		// Act
		options.ProjectId = "my-project";

		// Assert
		options.ProjectId.ShouldBe("my-project");
	}

	[Fact]
	public void AllowSettingCollectionName()
	{
		// Arrange
		var options = new FirestoreInboxOptions();

		// Act
		options.CollectionName = "custom_inbox";

		// Assert
		options.CollectionName.ShouldBe("custom_inbox");
	}

	[Fact]
	public void AllowSettingDefaultTtlSeconds()
	{
		// Arrange
		var options = new FirestoreInboxOptions();

		// Act
		options.DefaultTtlSeconds = 0;

		// Assert
		options.DefaultTtlSeconds.ShouldBe(0);
	}

	[Fact]
	public void AllowSettingTimeoutInSeconds()
	{
		// Arrange
		var options = new FirestoreInboxOptions();

		// Act
		options.TimeoutInSeconds = 60;

		// Assert
		options.TimeoutInSeconds.ShouldBe(60);
	}

	#endregion

	#region Validate Tests

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenBothProjectIdAndEmulatorHostAreNull()
	{
		// Arrange
		var options = new FirestoreInboxOptions();

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("ProjectId");
		exception.Message.ShouldContain("EmulatorHost");
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenCollectionNameIsNull()
	{
		// Arrange
		var options = new FirestoreInboxOptions
		{
			ProjectId = "my-project",
			CollectionName = null!
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("CollectionName");
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenCollectionNameIsEmpty()
	{
		// Arrange
		var options = new FirestoreInboxOptions
		{
			ProjectId = "my-project",
			CollectionName = ""
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenCollectionNameIsWhitespace()
	{
		// Arrange
		var options = new FirestoreInboxOptions
		{
			ProjectId = "my-project",
			CollectionName = "   "
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_DoesNotThrow_WhenProjectIdIsSet()
	{
		// Arrange
		var options = new FirestoreInboxOptions
		{
			ProjectId = "my-project"
		};

		// Act & Assert
		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void Validate_DoesNotThrow_WhenEmulatorHostIsSet()
	{
		// Arrange
		var options = new FirestoreInboxOptions
		{
			EmulatorHost = "localhost:8080"
		};

		// Act & Assert
		Should.NotThrow(() => options.Validate());
	}

	#endregion

	#region Type Tests

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(FirestoreInboxOptions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void BePublic()
	{
		// Assert
		typeof(FirestoreInboxOptions).IsPublic.ShouldBeTrue();
	}

	#endregion
}
