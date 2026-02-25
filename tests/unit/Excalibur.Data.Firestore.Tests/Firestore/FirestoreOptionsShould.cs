// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore;

namespace Excalibur.Data.Tests.Firestore;

/// <summary>
/// Unit tests for <see cref="FirestoreOptions"/>.
/// </summary>
/// <remarks>
/// Sprint 515 (S515.2): Firestore unit tests.
/// Tests verify options defaults and validation.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Firestore")]
[Trait("Feature", "Configuration")]
public sealed class FirestoreOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void HaveDefaultName()
	{
		// Arrange & Act
		var options = new FirestoreOptions();

		// Assert
		options.Name.ShouldBe("Firestore");
	}

	[Fact]
	public void HaveNullProjectIdByDefault()
	{
		// Arrange & Act
		var options = new FirestoreOptions();

		// Assert
		options.ProjectId.ShouldBeNull();
	}

	[Fact]
	public void HaveNullDefaultCollectionByDefault()
	{
		// Arrange & Act
		var options = new FirestoreOptions();

		// Assert
		options.DefaultCollection.ShouldBeNull();
	}

	[Fact]
	public void HaveNullCredentialsPathByDefault()
	{
		// Arrange & Act
		var options = new FirestoreOptions();

		// Assert
		options.CredentialsPath.ShouldBeNull();
	}

	[Fact]
	public void HaveNullCredentialsJsonByDefault()
	{
		// Arrange & Act
		var options = new FirestoreOptions();

		// Assert
		options.CredentialsJson.ShouldBeNull();
	}

	[Fact]
	public void HaveNullEmulatorHostByDefault()
	{
		// Arrange & Act
		var options = new FirestoreOptions();

		// Assert
		options.EmulatorHost.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultTimeoutInSeconds()
	{
		// Arrange & Act
		var options = new FirestoreOptions();

		// Assert
		options.TimeoutInSeconds.ShouldBe(30);
	}

	[Fact]
	public void HaveDefaultMaxRetryAttempts()
	{
		// Arrange & Act
		var options = new FirestoreOptions();

		// Assert
		options.MaxRetryAttempts.ShouldBe(3);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void AllowSettingName()
	{
		// Arrange
		var options = new FirestoreOptions();

		// Act
		options.Name = "CustomFirestore";

		// Assert
		options.Name.ShouldBe("CustomFirestore");
	}

	[Fact]
	public void AllowSettingProjectId()
	{
		// Arrange
		var options = new FirestoreOptions();

		// Act
		options.ProjectId = "my-project";

		// Assert
		options.ProjectId.ShouldBe("my-project");
	}

	[Fact]
	public void AllowSettingDefaultCollection()
	{
		// Arrange
		var options = new FirestoreOptions();

		// Act
		options.DefaultCollection = "documents";

		// Assert
		options.DefaultCollection.ShouldBe("documents");
	}

	[Fact]
	public void AllowSettingCredentialsPath()
	{
		// Arrange
		var options = new FirestoreOptions();

		// Act
		options.CredentialsPath = "/path/to/credentials.json";

		// Assert
		options.CredentialsPath.ShouldBe("/path/to/credentials.json");
	}

	[Fact]
	public void AllowSettingCredentialsJson()
	{
		// Arrange
		var options = new FirestoreOptions();

		// Act
		options.CredentialsJson = "{\"type\":\"service_account\"}";

		// Assert
		options.CredentialsJson.ShouldBe("{\"type\":\"service_account\"}");
	}

	[Fact]
	public void AllowSettingEmulatorHost()
	{
		// Arrange
		var options = new FirestoreOptions();

		// Act
		options.EmulatorHost = "localhost:8080";

		// Assert
		options.EmulatorHost.ShouldBe("localhost:8080");
	}

	[Fact]
	public void AllowSettingTimeoutInSeconds()
	{
		// Arrange
		var options = new FirestoreOptions();

		// Act
		options.TimeoutInSeconds = 60;

		// Assert
		options.TimeoutInSeconds.ShouldBe(60);
	}

	[Fact]
	public void AllowSettingMaxRetryAttempts()
	{
		// Arrange
		var options = new FirestoreOptions();

		// Act
		options.MaxRetryAttempts = 5;

		// Assert
		options.MaxRetryAttempts.ShouldBe(5);
	}

	#endregion

	#region Validate Tests

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenBothProjectIdAndEmulatorHostAreNull()
	{
		// Arrange
		var options = new FirestoreOptions();

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("ProjectId");
		exception.Message.ShouldContain("EmulatorHost");
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenBothProjectIdAndEmulatorHostAreEmpty()
	{
		// Arrange
		var options = new FirestoreOptions
		{
			ProjectId = "",
			EmulatorHost = ""
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenBothProjectIdAndEmulatorHostAreWhitespace()
	{
		// Arrange
		var options = new FirestoreOptions
		{
			ProjectId = "   ",
			EmulatorHost = "   "
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_DoesNotThrow_WhenProjectIdIsSet()
	{
		// Arrange
		var options = new FirestoreOptions
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
		var options = new FirestoreOptions
		{
			EmulatorHost = "localhost:8080"
		};

		// Act & Assert
		Should.NotThrow(() => options.Validate());
	}

	[Fact]
	public void Validate_DoesNotThrow_WhenBothProjectIdAndEmulatorHostAreSet()
	{
		// Arrange
		var options = new FirestoreOptions
		{
			ProjectId = "my-project",
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
		typeof(FirestoreOptions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void BePublic()
	{
		// Assert
		typeof(FirestoreOptions).IsPublic.ShouldBeTrue();
	}

	#endregion
}
