// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Firestore;

namespace Excalibur.EventSourcing.Tests.Firestore;

/// <summary>
/// Unit tests for <see cref="FirestoreEventStoreOptions"/> configuration and validation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class FirestoreEventStoreOptionsShould : UnitTestBase
{
	#region Default Values Tests

	[Fact]
	public void HaveNullProjectId()
	{
		// Arrange & Act
		var options = new FirestoreEventStoreOptions();

		// Assert
		options.ProjectId.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultEventsCollectionName()
	{
		// Arrange & Act
		var options = new FirestoreEventStoreOptions();

		// Assert
		options.EventsCollectionName.ShouldBe("events");
	}

	[Fact]
	public void HaveNullCredentialsPath()
	{
		// Arrange & Act
		var options = new FirestoreEventStoreOptions();

		// Assert
		options.CredentialsPath.ShouldBeNull();
	}

	[Fact]
	public void HaveNullCredentialsJson()
	{
		// Arrange & Act
		var options = new FirestoreEventStoreOptions();

		// Assert
		options.CredentialsJson.ShouldBeNull();
	}

	[Fact]
	public void HaveNullEmulatorHost()
	{
		// Arrange & Act
		var options = new FirestoreEventStoreOptions();

		// Assert
		options.EmulatorHost.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultUseBatchedWritesTrue()
	{
		// Arrange & Act
		var options = new FirestoreEventStoreOptions();

		// Assert
		options.UseBatchedWrites.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultMaxBatchSizeOf500()
	{
		// Arrange & Act
		var options = new FirestoreEventStoreOptions();

		// Assert
		options.MaxBatchSize.ShouldBe(500);
	}

	[Fact]
	public void HaveDefaultCreateCollectionIfNotExistsTrue()
	{
		// Arrange & Act
		var options = new FirestoreEventStoreOptions();

		// Assert
		options.CreateCollectionIfNotExists.ShouldBeTrue();
	}

	#endregion Default Values Tests

	#region Property Setters Tests

	[Fact]
	public void AllowCustomProjectId()
	{
		// Arrange & Act
		var options = new FirestoreEventStoreOptions
		{
			ProjectId = "my-gcp-project"
		};

		// Assert
		options.ProjectId.ShouldBe("my-gcp-project");
	}

	[Fact]
	public void AllowCustomEventsCollectionName()
	{
		// Arrange & Act
		var options = new FirestoreEventStoreOptions
		{
			EventsCollectionName = "custom_events"
		};

		// Assert
		options.EventsCollectionName.ShouldBe("custom_events");
	}

	[Fact]
	public void AllowCustomCredentialsPath()
	{
		// Arrange & Act
		var options = new FirestoreEventStoreOptions
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
		var options = new FirestoreEventStoreOptions
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
		var options = new FirestoreEventStoreOptions
		{
			EmulatorHost = "localhost:8080"
		};

		// Assert
		options.EmulatorHost.ShouldBe("localhost:8080");
	}

	[Fact]
	public void AllowCustomUseBatchedWrites()
	{
		// Arrange & Act
		var options = new FirestoreEventStoreOptions
		{
			UseBatchedWrites = false
		};

		// Assert
		options.UseBatchedWrites.ShouldBeFalse();
	}

	[Fact]
	public void AllowCustomMaxBatchSize()
	{
		// Arrange & Act
		var options = new FirestoreEventStoreOptions
		{
			MaxBatchSize = 250
		};

		// Assert
		options.MaxBatchSize.ShouldBe(250);
	}

	[Fact]
	public void AllowCustomCreateCollectionIfNotExists()
	{
		// Arrange & Act
		var options = new FirestoreEventStoreOptions
		{
			CreateCollectionIfNotExists = false
		};

		// Assert
		options.CreateCollectionIfNotExists.ShouldBeFalse();
	}

	#endregion Property Setters Tests

	#region Validation Tests

	[Fact]
	public void Validate_WithProjectId_DoesNotThrow()
	{
		// Arrange
		var options = new FirestoreEventStoreOptions
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
		var options = new FirestoreEventStoreOptions
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
		var options = new FirestoreEventStoreOptions
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
		var options = new FirestoreEventStoreOptions();

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("ProjectId");
	}

	[Fact]
	public void Validate_WithWhitespaceProjectId_ThrowsInvalidOperationException()
	{
		// Arrange
		var options = new FirestoreEventStoreOptions
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
		var options = new FirestoreEventStoreOptions
		{
			EmulatorHost = "   "
		};

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("ProjectId");
	}

	[Fact]
	public void Validate_WithEmptyProjectId_ThrowsInvalidOperationException()
	{
		// Arrange
		var options = new FirestoreEventStoreOptions
		{
			ProjectId = string.Empty
		};

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	[Fact]
	public void Validate_WithEmptyEmulatorHost_ThrowsInvalidOperationException()
	{
		// Arrange
		var options = new FirestoreEventStoreOptions
		{
			EmulatorHost = string.Empty
		};

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() => options.Validate());
	}

	#endregion Validation Tests
}
