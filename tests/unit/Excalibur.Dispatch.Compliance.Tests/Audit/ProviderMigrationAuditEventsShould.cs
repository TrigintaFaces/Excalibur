using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.Audit;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ProviderMigrationAuditEventsShould
{
	[Fact]
	public void Create_data_re_encrypted_event()
	{
		var evt = ProviderMigrationAuditEvents.DataReEncrypted(
			"aws-kms", "vault", "AES-256-GCM", "AES-256-CBC",
			"key-old", "key-new", "rec-1", "Customer", "system", "corr-1");

		evt.EventType.ShouldBe(AuditEventType.Security);
		evt.Action.ShouldBe("DataReEncrypted");
		evt.Outcome.ShouldBe(AuditOutcome.Success);
		evt.ResourceId.ShouldBe("rec-1");
		evt.ResourceType.ShouldBe("Customer");
		evt.Metadata!["sourceProvider"].ShouldBe("aws-kms");
		evt.Metadata["targetProvider"].ShouldBe("vault");
		evt.Metadata["sourceKeyId"].ShouldBe("key-old");
		evt.Metadata["targetKeyId"].ShouldBe("key-new");
	}

	[Fact]
	public void Create_provider_migration_completed_event_success()
	{
		var evt = ProviderMigrationAuditEvents.ProviderMigrationCompleted(
			"aws-kms", "vault", 100, 0, 5, TimeSpan.FromMinutes(10), "system");

		evt.Outcome.ShouldBe(AuditOutcome.Success);
		evt.ResourceId.ShouldBe("aws-kms->vault");
		evt.Metadata!["migratedCount"].ShouldBe("100");
		evt.Metadata["totalCount"].ShouldBe("105");
	}

	[Fact]
	public void Create_provider_migration_completed_event_with_failures()
	{
		var evt = ProviderMigrationAuditEvents.ProviderMigrationCompleted(
			"aws-kms", "vault", 80, 20, 0, TimeSpan.FromMinutes(15), "system");

		evt.Outcome.ShouldBe(AuditOutcome.Failure);
		evt.Metadata!["failedCount"].ShouldBe("20");
	}

	[Fact]
	public void Create_decryption_migration_completed_event_success()
	{
		var evt = ProviderMigrationAuditEvents.DecryptionMigrationCompleted(
			"aws-kms", 200, 200, 0, TimeSpan.FromSeconds(30), "system",
			"Switching to plaintext", "corr-2");

		evt.Outcome.ShouldBe(AuditOutcome.Success);
		evt.Action.ShouldBe("DecryptionMigrationCompleted");
		evt.Reason.ShouldBe("Switching to plaintext");
		evt.Metadata!["successRate"].ShouldContain("%");
	}

	[Fact]
	public void Create_decryption_migration_completed_event_with_failures()
	{
		var evt = ProviderMigrationAuditEvents.DecryptionMigrationCompleted(
			"aws-kms", 100, 90, 10, TimeSpan.FromSeconds(60), "system");

		evt.Outcome.ShouldBe(AuditOutcome.Failure);
	}
}
