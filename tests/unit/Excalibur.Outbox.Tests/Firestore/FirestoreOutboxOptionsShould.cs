// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox.Tests.Firestore;

/// <summary>
/// Unit tests for <see cref="FirestoreOutboxOptions" />.
/// </summary>
[Trait("Category", "Unit")]
public sealed class FirestoreOutboxOptionsShould : UnitTestBase
{
	[Fact]
	public void DefaultValues_AreCorrect()
	{
		// Act
		var options = new FirestoreOutboxOptions();

		// Assert
		options.ProjectId.ShouldBeNull();
		options.CredentialsPath.ShouldBeNull();
		options.CredentialsJson.ShouldBeNull();
		options.EmulatorHost.ShouldBeNull();
		options.CollectionName.ShouldBe("outbox");
		options.DefaultTimeToLiveSeconds.ShouldBe(604800); // 7 days
		options.MaxBatchSize.ShouldBe(500);
		options.CreateCollectionIfNotExists.ShouldBeTrue();
		options.MaxRetryAttempts.ShouldBe(3);
	}

	[Fact]
	public void ProjectId_CanBeSet()
	{
		// Arrange
		var options = new FirestoreOutboxOptions();
		const string projectId = "my-gcp-project";

		// Act
		options.ProjectId = projectId;

		// Assert
		options.ProjectId.ShouldBe(projectId);
	}

	[Fact]
	public void CredentialsPath_CanBeSet()
	{
		// Arrange
		var options = new FirestoreOutboxOptions();
		const string credentialsPath = "/path/to/credentials.json";

		// Act
		options.CredentialsPath = credentialsPath;

		// Assert
		options.CredentialsPath.ShouldBe(credentialsPath);
	}

	[Fact]
	public void CredentialsJson_CanBeSet()
	{
		// Arrange
		var options = new FirestoreOutboxOptions();
		const string credentialsJson = "{\"type\":\"service_account\"}";

		// Act
		options.CredentialsJson = credentialsJson;

		// Assert
		options.CredentialsJson.ShouldBe(credentialsJson);
	}

	[Fact]
	public void EmulatorHost_CanBeSet()
	{
		// Arrange
		var options = new FirestoreOutboxOptions();
		const string emulatorHost = "localhost:8080";

		// Act
		options.EmulatorHost = emulatorHost;

		// Assert
		options.EmulatorHost.ShouldBe(emulatorHost);
	}

	[Fact]
	public void CollectionName_CanBeSet()
	{
		// Arrange
		var options = new FirestoreOutboxOptions();
		const string collectionName = "custom-outbox";

		// Act
		options.CollectionName = collectionName;

		// Assert
		options.CollectionName.ShouldBe(collectionName);
	}

	[Fact]
	public void DefaultTimeToLiveSeconds_CanBeSet()
	{
		// Arrange
		var options = new FirestoreOutboxOptions();
		const int ttl = 0; // Disable TTL

		// Act
		options.DefaultTimeToLiveSeconds = ttl;

		// Assert
		options.DefaultTimeToLiveSeconds.ShouldBe(ttl);
	}

	[Fact]
	public void MaxBatchSize_CanBeSet()
	{
		// Arrange
		var options = new FirestoreOutboxOptions();
		const int batchSize = 100;

		// Act
		options.MaxBatchSize = batchSize;

		// Assert
		options.MaxBatchSize.ShouldBe(batchSize);
	}

	[Fact]
	public void MaxRetryAttempts_CanBeSet()
	{
		// Arrange
		var options = new FirestoreOutboxOptions();
		const int retries = 5;

		// Act
		options.MaxRetryAttempts = retries;

		// Assert
		options.MaxRetryAttempts.ShouldBe(retries);
	}

	[Fact]
	public void Validate_Succeeds_WhenProjectIdProvided()
	{
		// Arrange
		var options = new FirestoreOutboxOptions
		{
			ProjectId = "my-gcp-project",
		};

		// Act & Assert - Should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_Succeeds_WhenEmulatorHostProvided()
	{
		// Arrange
		var options = new FirestoreOutboxOptions
		{
			EmulatorHost = "localhost:8080",
		};

		// Act & Assert - Should not throw
		options.Validate();
	}

	[Fact]
	public void Validate_ThrowsInvalidOperationException_WhenNoConfigProvided()
	{
		// Arrange
		var options = new FirestoreOutboxOptions();

		// Act & Assert
		var exception = Should.Throw<InvalidOperationException>(() => options.Validate());
		exception.Message.ShouldContain("ProjectId");
		exception.Message.ShouldContain("EmulatorHost");
	}
}
