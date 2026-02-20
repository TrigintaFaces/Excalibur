// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore.Outbox;

namespace Excalibur.Data.Tests.Firestore;

/// <summary>
/// Unit tests for <see cref="FirestoreOutboxOptions"/>.
/// </summary>
/// <remarks>
/// Sprint 515 (S515.2): Firestore unit tests.
/// Tests verify outbox options defaults and validation.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Firestore")]
[Trait("Feature", "Configuration")]
public sealed class FirestoreOutboxOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void HaveNullProjectIdByDefault()
	{
		// Arrange & Act
		var options = new FirestoreOutboxOptions();

		// Assert
		options.ProjectId.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultCollectionName()
	{
		// Arrange & Act
		var options = new FirestoreOutboxOptions();

		// Assert
		options.CollectionName.ShouldBe("outbox_messages");
	}

	[Fact]
	public void HaveNullCredentialsPathByDefault()
	{
		// Arrange & Act
		var options = new FirestoreOutboxOptions();

		// Assert
		options.CredentialsPath.ShouldBeNull();
	}

	[Fact]
	public void HaveNullCredentialsJsonByDefault()
	{
		// Arrange & Act
		var options = new FirestoreOutboxOptions();

		// Assert
		options.CredentialsJson.ShouldBeNull();
	}

	[Fact]
	public void HaveNullEmulatorHostByDefault()
	{
		// Arrange & Act
		var options = new FirestoreOutboxOptions();

		// Assert
		options.EmulatorHost.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultSentMessageTtlSeconds()
	{
		// Arrange & Act
		var options = new FirestoreOutboxOptions();

		// Assert - 7 days in seconds
		options.SentMessageTtlSeconds.ShouldBe(604800);
	}

	[Fact]
	public void HaveDefaultTimeoutInSeconds()
	{
		// Arrange & Act
		var options = new FirestoreOutboxOptions();

		// Assert
		options.TimeoutInSeconds.ShouldBe(30);
	}

	[Fact]
	public void HaveDefaultMaxBatchSize()
	{
		// Arrange & Act
		var options = new FirestoreOutboxOptions();

		// Assert - Firestore limit is 500
		options.MaxBatchSize.ShouldBe(500);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void AllowSettingProjectId()
	{
		// Arrange
		var options = new FirestoreOutboxOptions();

		// Act
		options.ProjectId = "my-project";

		// Assert
		options.ProjectId.ShouldBe("my-project");
	}

	[Fact]
	public void AllowSettingCollectionName()
	{
		// Arrange
		var options = new FirestoreOutboxOptions();

		// Act
		options.CollectionName = "custom_outbox";

		// Assert
		options.CollectionName.ShouldBe("custom_outbox");
	}

	[Fact]
	public void AllowSettingSentMessageTtlSeconds()
	{
		// Arrange
		var options = new FirestoreOutboxOptions();

		// Act
		options.SentMessageTtlSeconds = 0;

		// Assert
		options.SentMessageTtlSeconds.ShouldBe(0);
	}

	[Fact]
	public void AllowSettingMaxBatchSize()
	{
		// Arrange
		var options = new FirestoreOutboxOptions();

		// Act
		options.MaxBatchSize = 100;

		// Assert
		options.MaxBatchSize.ShouldBe(100);
	}

	#endregion

	#region Validate Tests

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenBothProjectIdAndEmulatorHostAreNull()
	{
		// Arrange
		var options = new FirestoreOutboxOptions();

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("ProjectId");
		exception.Message.ShouldContain("EmulatorHost");
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenCollectionNameIsNull()
	{
		// Arrange
		var options = new FirestoreOutboxOptions
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
		var options = new FirestoreOutboxOptions
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
		var options = new FirestoreOutboxOptions
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
		var options = new FirestoreOutboxOptions
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
		var options = new FirestoreOutboxOptions
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
		typeof(FirestoreOutboxOptions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void BePublic()
	{
		// Assert
		typeof(FirestoreOutboxOptions).IsPublic.ShouldBeTrue();
	}

	#endregion
}
