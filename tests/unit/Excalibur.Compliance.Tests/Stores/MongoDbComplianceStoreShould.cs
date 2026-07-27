// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

using MsOptions = Microsoft.Extensions.Options.Options;

using Excalibur.Compliance.Stores.MongoDb;

using Excalibur.Compliance;namespace Excalibur.Compliance.Tests.Stores;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class MongoDbComplianceStoreShould
{
	private readonly ILogger<MongoDbComplianceStore> _logger = NullLogger<MongoDbComplianceStore>.Instance;

	[Fact]
	public void ThrowWhenOptionsIsNullInIOptionsConstructor()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new MongoDbComplianceStore(
				(IOptions<MongoDbComplianceOptions>)null!,
				null,
				_logger));
	}

	[Fact]
	public void ThrowWhenLoggerIsNullInIOptionsConstructor()
	{
		// Arrange
		var options = MsOptions.Create(new MongoDbComplianceOptions());

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new MongoDbComplianceStore(options, null, null!));
	}

	[Fact]
	public void ThrowWhenClientIsNullInClientConstructor()
	{
		// Arrange
		var options = MsOptions.Create(new MongoDbComplianceOptions());

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new MongoDbComplianceStore(
				(IMongoClient)null!,
				options,
				null,
				_logger));
	}

	[Fact]
	public void ThrowWhenOptionsIsNullInClientConstructor()
	{
		// Arrange
		var client = A.Fake<IMongoClient>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new MongoDbComplianceStore(
				client,
				(IOptions<MongoDbComplianceOptions>)null!,
				null,
				_logger));
	}

	[Fact]
	public void ThrowWhenLoggerIsNullInClientConstructor()
	{
		// Arrange
		var client = A.Fake<IMongoClient>();
		var options = MsOptions.Create(new MongoDbComplianceOptions());

		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new MongoDbComplianceStore(client, options, null, null!));
	}

	[Fact]
	public void ConstructSuccessfullyWithValidOptions()
	{
		// Arrange
		var options = MsOptions.Create(new MongoDbComplianceOptions());

		// Act
		var store = new MongoDbComplianceStore(options, null, _logger);

		// Assert
		store.ShouldNotBeNull();
	}

	[Fact]
	public void ConstructSuccessfullyWithClient()
	{
		// Arrange
		var client = A.Fake<IMongoClient>();
		var db = A.Fake<IMongoDatabase>();
		A.CallTo(() => client.GetDatabase(A<string>._, A<MongoDatabaseSettings>._)).Returns(db);
		A.CallTo(() => db.GetCollection<MongoDbComplianceStore.ConsentDocument>(
			A<string>._, A<MongoCollectionSettings>._))
			.Returns(A.Fake<IMongoCollection<MongoDbComplianceStore.ConsentDocument>>());
		var options = MsOptions.Create(new MongoDbComplianceOptions());

		// Act
		var store = new MongoDbComplianceStore(client, options, null, _logger);

		// Assert
		store.ShouldNotBeNull();
	}

	[Fact]
	public async Task ThrowWhenStoreConsentRecordIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new MongoDbComplianceOptions());
		var store = new MongoDbComplianceStore(options, null, _logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => store.StoreConsentAsync(null!, CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public async Task ThrowWhenGetConsentSubjectIdIsNullOrWhitespace(string? subjectId)
	{
		// Arrange
		var options = MsOptions.Create(new MongoDbComplianceOptions());
		var store = new MongoDbComplianceStore(options, null, _logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => store.GetConsentAsync(subjectId!, "purpose", CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public async Task ThrowWhenGetConsentPurposeIsNullOrWhitespace(string? purpose)
	{
		// Arrange
		var options = MsOptions.Create(new MongoDbComplianceOptions());
		var store = new MongoDbComplianceStore(options, null, _logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => store.GetConsentAsync("subject-1", purpose!, CancellationToken.None));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public async Task ThrowWhenStoreErasureLogSubjectIdIsNullOrWhitespace(string? subjectId)
	{
		// Arrange
		var options = MsOptions.Create(new MongoDbComplianceOptions());
		var store = new MongoDbComplianceStore(options, null, _logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => store.StoreErasureLogAsync(subjectId!, "details", DateTimeOffset.UtcNow, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenStoreSubjectAccessRequestResultIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new MongoDbComplianceOptions());
		var store = new MongoDbComplianceStore(options, null, _logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => store.StoreSubjectAccessRequestAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenEnsureInitializedWithNoConnectionString()
	{
		// Arrange -- options with no connection string, no client injected
		var options = MsOptions.Create(new MongoDbComplianceOptions { ConnectionString = null });
		var store = new MongoDbComplianceStore(options, null, _logger);
		var record = new ConsentRecord
		{
			SubjectId = "subject-1",
			Purpose = "analytics"
		};

		// Act & Assert -- EnsureInitializedAsync called from StoreConsentAsync
		await Should.ThrowAsync<InvalidOperationException>(
			() => store.StoreConsentAsync(record, CancellationToken.None));
	}

	[Fact]
	public void ConsentDocumentCreateIdShouldCombineTenantSubjectAndPurpose()
	{
		// Act
		var id = MongoDbComplianceStore.ConsentDocument.CreateId("tenant-a", "user-123", "marketing");

		// Assert
		id.ShouldBe("8:tenant-a:8:user-123:marketing");
	}

	[Fact]
	public void ConsentDocumentCreateIdShouldSeparateTenantsSharingASubjectAndPurpose()
	{
		// Act
		var tenantA = MongoDbComplianceStore.ConsentDocument.CreateId("tenant-a", "user-123", "marketing");
		var tenantB = MongoDbComplianceStore.ConsentDocument.CreateId("tenant-b", "user-123", "marketing");

		// Assert - the key is the upsert conflict target, so equal keys here would mean tenant B's write
		// silently overwrites tenant A's consent record for the same data subject.
		tenantA.ShouldNotBe(tenantB);
	}

	[Fact]
	public void ConsentDocumentCreateIdShouldNotCollideWhenATenantIdContainsTheDelimiter()
	{
		// Act - a plain "tenant:subject:purpose" join would render both of these as "a:b:c:marketing".
		var delimiterInTenant = MongoDbComplianceStore.ConsentDocument.CreateId("a:b", "c", "marketing");
		var delimiterInSubject = MongoDbComplianceStore.ConsentDocument.CreateId("a", "b:c", "marketing");

		// Assert
		delimiterInTenant.ShouldNotBe(delimiterInSubject);
	}

	[Fact]
	public void ConsentDocumentFromRecordShouldMapAllProperties()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;
		var record = new ConsentRecord
		{
			SubjectId = "user-456",
			Purpose = "analytics",
			GrantedAt = now,
			ExpiresAt = now.AddYears(1),
			LegalBasis = LegalBasis.Consent,
			IsWithdrawn = false,
			WithdrawnAt = null
		};

		// Act
		var doc = MongoDbComplianceStore.ConsentDocument.FromRecord(record, "tenant-a");

		// Assert
		doc.Id.ShouldBe("8:tenant-a:8:user-456:analytics");
		doc.TenantId.ShouldBe("tenant-a");
		doc.SubjectId.ShouldBe("user-456");
		doc.Purpose.ShouldBe("analytics");
		doc.GrantedAt.ShouldBe(now);
		doc.ExpiresAt.ShouldBe(now.AddYears(1));
		doc.LegalBasis.ShouldBe((int)LegalBasis.Consent);
		doc.IsWithdrawn.ShouldBeFalse();
		doc.WithdrawnAt.ShouldBeNull();
	}

	[Fact]
	public void ConsentDocumentToConsentRecordShouldMapAllProperties()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;
		var withdrawnAt = now.AddDays(30);
		var doc = new MongoDbComplianceStore.ConsentDocument
		{
			Id = "user-789:marketing",
			SubjectId = "user-789",
			Purpose = "marketing",
			GrantedAt = now,
			ExpiresAt = now.AddYears(2),
			LegalBasis = (int)LegalBasis.LegalObligation,
			IsWithdrawn = true,
			WithdrawnAt = withdrawnAt
		};

		// Act
		var record = doc.ToConsentRecord();

		// Assert
		record.SubjectId.ShouldBe("user-789");
		record.Purpose.ShouldBe("marketing");
		record.GrantedAt.ShouldBe(now);
		record.ExpiresAt.ShouldBe(now.AddYears(2));
		record.LegalBasis.ShouldBe(LegalBasis.LegalObligation);
		record.IsWithdrawn.ShouldBeTrue();
		record.WithdrawnAt.ShouldBe(withdrawnAt);
	}

	[Fact]
	public void ConsentDocumentRoundtripShouldPreserveAllData()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;
		var original = new ConsentRecord
		{
			SubjectId = "roundtrip-user",
			Purpose = "profiling",
			GrantedAt = now,
			ExpiresAt = now.AddMonths(6),
			LegalBasis = LegalBasis.Contract,
			IsWithdrawn = false,
			WithdrawnAt = null
		};

		// Act
		var doc = MongoDbComplianceStore.ConsentDocument.FromRecord(original, "tenant-a");
		var restored = doc.ToConsentRecord();

		// Assert
		restored.SubjectId.ShouldBe(original.SubjectId);
		restored.Purpose.ShouldBe(original.Purpose);
		restored.GrantedAt.ShouldBe(original.GrantedAt);
		restored.ExpiresAt.ShouldBe(original.ExpiresAt);
		restored.LegalBasis.ShouldBe(original.LegalBasis);
		restored.IsWithdrawn.ShouldBe(original.IsWithdrawn);
		restored.WithdrawnAt.ShouldBe(original.WithdrawnAt);
	}

	[Fact]
	public void SubjectAccessDocumentFromResultShouldMapAllProperties()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;
		var result = new SubjectAccessResult
		{
			RequestId = "SAR-001",
			Status = SubjectAccessRequestStatus.Fulfilled,
			Deadline = now.AddDays(30),
			FulfilledAt = now
		};

		// Act
		var doc = MongoDbComplianceStore.SubjectAccessDocument.FromResult(result, "tenant-a");

		// Assert
		doc.Id.ShouldBe("8:tenant-a:SAR-001");
		doc.TenantId.ShouldBe("tenant-a");
		doc.Status.ShouldBe((int)SubjectAccessRequestStatus.Fulfilled);
		doc.Deadline.ShouldBe(now.AddDays(30));
		doc.FulfilledAt.ShouldBe(now);
	}

	[Fact]
	public void SubjectAccessDocumentHandlesNullOptionalFields()
	{
		// Arrange
		var result = new SubjectAccessResult
		{
			RequestId = "SAR-002",
			Status = SubjectAccessRequestStatus.Pending,
			Deadline = null,
			FulfilledAt = null
		};

		// Act
		var doc = MongoDbComplianceStore.SubjectAccessDocument.FromResult(result, "tenant-a");

		// Assert
		doc.Id.ShouldBe("8:tenant-a:SAR-002");
		doc.TenantId.ShouldBe("tenant-a");
		doc.Status.ShouldBe((int)SubjectAccessRequestStatus.Pending);
		doc.Deadline.ShouldBeNull();
		doc.FulfilledAt.ShouldBeNull();
	}
}
