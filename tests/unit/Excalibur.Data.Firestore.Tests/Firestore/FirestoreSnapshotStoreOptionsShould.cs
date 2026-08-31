// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore.Snapshots;

using Excalibur.Data.Firestore;

namespace Excalibur.Data.Tests.Firestore.Snapshots;

/// <summary>
/// Unit tests for <see cref="FirestoreSnapshotStoreOptions"/> configuration and validation.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
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

	/// <summary>
	/// Pins the shipped contended-write retry budget. The store's contention behaviour is now
	/// configurable, and these two defaults are what a consumer gets when they configure nothing --
	/// so they are the values that describe the store as shipped, not an arbitrary starting point.
	/// A change here changes what an uncontended-to-contended write costs every existing consumer.
	/// </summary>
	/// <remarks>
	/// 16 is a spin guard, not a writer budget. The store's contended write takes no lock, so a writer is
	/// re-attempted only because another writer's write landed and strictly raised the stored version --
	/// which means it needs at most one extra attempt per concurrent writer holding a lower version, and
	/// reaching the bound is a fault rather than an expected outcome. Measured against ten concurrent
	/// savers on a real emulator, over twenty runs, the deepest any writer reached was attempt 5. The
	/// earlier default of 40 was sized for a write that waited on a document lock, which this one does
	/// not. Raising this pins a longer wait before contention is reported, not a more correct store.
	/// </remarks>
	[Fact]
	public void HaveDefaultMaxContendedWriteAttemptsOf16()
	{
		// Arrange & Act
		var options = new FirestoreSnapshotStoreOptions();

		// Assert
		options.MaxContendedWriteAttempts.ShouldBe(16);
	}

	[Fact]
	public void HaveDefaultContendedWriteBackoffOf25Milliseconds()
	{
		// Arrange & Act
		var options = new FirestoreSnapshotStoreOptions();

		// Assert
		options.ContendedWriteBackoffMilliseconds.ShouldBe(25);
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
