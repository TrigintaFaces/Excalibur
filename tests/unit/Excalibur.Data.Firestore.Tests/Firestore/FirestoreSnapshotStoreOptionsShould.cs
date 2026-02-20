// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore.Snapshots;

using Excalibur.Data.Firestore;

namespace Excalibur.Data.Tests.Firestore.Snapshots;

/// <summary>
/// Unit tests for <see cref="FirestoreSnapshotStoreOptions"/> configuration and validation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class FirestoreSnapshotStoreOptionsShould : UnitTestBase
{
	#region Default Values Tests

	[Fact]
	public void HaveNullProjectId()
	{
		// Arrange & Act
		var options = new FirestoreSnapshotStoreOptions();

		// Assert
		options.ProjectId.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultCollectionName()
	{
		// Arrange & Act
		var options = new FirestoreSnapshotStoreOptions();

		// Assert
		options.CollectionName.ShouldBe("snapshots");
	}

	[Fact]
	public void HaveNullCredentialsPath()
	{
		// Arrange & Act
		var options = new FirestoreSnapshotStoreOptions();

		// Assert
		options.CredentialsPath.ShouldBeNull();
	}

	[Fact]
	public void HaveNullCredentialsJson()
	{
		// Arrange & Act
		var options = new FirestoreSnapshotStoreOptions();

		// Assert
		options.CredentialsJson.ShouldBeNull();
	}

	[Fact]
	public void HaveNullEmulatorHost()
	{
		// Arrange & Act
		var options = new FirestoreSnapshotStoreOptions();

		// Assert
		options.EmulatorHost.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultTtlOfZero()
	{
		// Arrange & Act
		var options = new FirestoreSnapshotStoreOptions();

		// Assert
		options.DefaultTtlSeconds.ShouldBe(0);
	}

	[Fact]
	public void HaveDefaultTimeoutOf30Seconds()
	{
		// Arrange & Act
		var options = new FirestoreSnapshotStoreOptions();

		// Assert
		options.TimeoutInSeconds.ShouldBe(30);
	}

	[Fact]
	public void HaveDefaultMaxBatchSizeOf500()
	{
		// Arrange & Act
		var options = new FirestoreSnapshotStoreOptions();

		// Assert
		options.MaxBatchSize.ShouldBe(500);
	}

	#endregion Default Values Tests

	#region Property Setters Tests

	[Fact]
	public void AllowCustomProjectId()
	{
		// Arrange & Act
		var options = new FirestoreSnapshotStoreOptions
		{
			ProjectId = "my-gcp-project"
		};

		// Assert
		options.ProjectId.ShouldBe("my-gcp-project");
	}

	[Fact]
	public void AllowCustomCollectionName()
	{
		// Arrange & Act
		var options = new FirestoreSnapshotStoreOptions
		{
			CollectionName = "custom_snapshots"
		};

		// Assert
		options.CollectionName.ShouldBe("custom_snapshots");
	}

	[Fact]
	public void AllowCustomCredentialsPath()
	{
		// Arrange & Act
		var options = new FirestoreSnapshotStoreOptions
		{
			CredentialsPath = "/path/to/credentials.json"
		};

		// Assert
		options.CredentialsPath.ShouldBe("/path/to/credentials.json");
	}

	[Fact]
	public void AllowCustomCredentialsJson()
	{
		// Arrange & Act
		var options = new FirestoreSnapshotStoreOptions
		{
			CredentialsJson = "{\"type\": \"service_account\"}"
		};

		// Assert
		options.CredentialsJson.ShouldBe("{\"type\": \"service_account\"}");
	}

	[Fact]
	public void AllowCustomEmulatorHost()
	{
		// Arrange & Act
		var options = new FirestoreSnapshotStoreOptions
		{
			EmulatorHost = "localhost:8080"
		};

		// Assert
		options.EmulatorHost.ShouldBe("localhost:8080");
	}

	[Fact]
	public void AllowCustomTtl()
	{
		// Arrange & Act
		var options = new FirestoreSnapshotStoreOptions
		{
			DefaultTtlSeconds = 3600
		};

		// Assert
		options.DefaultTtlSeconds.ShouldBe(3600);
	}

	[Fact]
	public void AllowCustomTimeout()
	{
		// Arrange & Act
		var options = new FirestoreSnapshotStoreOptions
		{
			TimeoutInSeconds = 60
		};

		// Assert
		options.TimeoutInSeconds.ShouldBe(60);
	}

	[Fact]
	public void AllowCustomMaxBatchSize()
	{
		// Arrange & Act
		var options = new FirestoreSnapshotStoreOptions
		{
			MaxBatchSize = 250
		};

		// Assert
		options.MaxBatchSize.ShouldBe(250);
	}

	#endregion Property Setters Tests

	#region Validation Tests

	[Fact]
	public void Validate_WithProjectId_DoesNotThrow()
	{
		// Arrange
		var options = new FirestoreSnapshotStoreOptions
		{
			ProjectId = "my-project"
		};

		// Act & Assert - Should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_WithEmulatorHost_DoesNotThrow()
	{
		// Arrange
		var options = new FirestoreSnapshotStoreOptions
		{
			EmulatorHost = "localhost:8080"
		};

		// Act & Assert - Should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_WithBothProjectIdAndEmulatorHost_DoesNotThrow()
	{
		// Arrange
		var options = new FirestoreSnapshotStoreOptions
		{
			ProjectId = "my-project",
			EmulatorHost = "localhost:8080"
		};

		// Act & Assert - Should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_WithoutProjectIdOrEmulatorHost_ThrowsInvalidOperationException()
	{
		// Arrange
		var options = new FirestoreSnapshotStoreOptions();

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("ProjectId");
		exception.Message.ShouldContain("EmulatorHost");
	}

	[Fact]
	public void Validate_WithNullCollectionName_ThrowsInvalidOperationException()
	{
		// Arrange
		var options = new FirestoreSnapshotStoreOptions
		{
			ProjectId = "my-project",
			CollectionName = null!
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("CollectionName");
	}

	[Fact]
	public void Validate_WithEmptyCollectionName_ThrowsInvalidOperationException()
	{
		// Arrange
		var options = new FirestoreSnapshotStoreOptions
		{
			ProjectId = "my-project",
			CollectionName = string.Empty
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("CollectionName");
	}

	[Fact]
	public void Validate_WithWhitespaceCollectionName_ThrowsInvalidOperationException()
	{
		// Arrange
		var options = new FirestoreSnapshotStoreOptions
		{
			ProjectId = "my-project",
			CollectionName = "   "
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("CollectionName");
	}

	[Fact]
	public void Validate_WithWhitespaceProjectId_ThrowsInvalidOperationException()
	{
		// Arrange
		var options = new FirestoreSnapshotStoreOptions
		{
			ProjectId = "   "
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("ProjectId");
	}

	[Fact]
	public void Validate_WithWhitespaceEmulatorHost_ThrowsInvalidOperationException()
	{
		// Arrange
		var options = new FirestoreSnapshotStoreOptions
		{
			EmulatorHost = "   "
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("EmulatorHost");
	}

	#endregion Validation Tests
}
