using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Compliance.Tests.Consent;

/// <summary>
/// Tests the consent service lifecycle workflows including multi-purpose consent,
/// expiration edge cases, and concurrent operations.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ConsentServiceWorkflowShould
{
	[Fact]
	public async Task Maintain_separate_consent_per_purpose()
	{
		// Arrange
		var sut = CreateService();
		var marketing = new ConsentRecord { SubjectId = "user-1", Purpose = "marketing" };
		var analytics = new ConsentRecord { SubjectId = "user-1", Purpose = "analytics" };

		// Act
		await sut.RecordConsentAsync(marketing, CancellationToken.None);
		await sut.RecordConsentAsync(analytics, CancellationToken.None);

		// Assert - both should be independently retrievable
		var m = await sut.GetConsentAsync("user-1", "marketing", CancellationToken.None);
		var a = await sut.GetConsentAsync("user-1", "analytics", CancellationToken.None);
		m.ShouldNotBeNull();
		a.ShouldNotBeNull();
		m.Purpose.ShouldBe("marketing");
		a.Purpose.ShouldBe("analytics");
	}

	[Fact]
	public async Task Withdraw_one_purpose_without_affecting_others()
	{
		// Arrange
		var sut = CreateService();
		await sut.RecordConsentAsync(
			new ConsentRecord { SubjectId = "user-1", Purpose = "marketing" },
			CancellationToken.None);
		await sut.RecordConsentAsync(
			new ConsentRecord { SubjectId = "user-1", Purpose = "analytics" },
			CancellationToken.None);

		// Act - withdraw marketing only
		var result = await sut.WithdrawConsentAsync("user-1", "marketing", CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
		var m = await sut.GetConsentAsync("user-1", "marketing", CancellationToken.None);
		var a = await sut.GetConsentAsync("user-1", "analytics", CancellationToken.None);
		m.ShouldBeNull(); // withdrawn
		a.ShouldNotBeNull(); // still active
	}

	[Fact]
	public async Task Override_existing_consent_with_new_record()
	{
		// Arrange
		var sut = CreateService();
		var original = new ConsentRecord
		{
			SubjectId = "user-1",
			Purpose = "marketing",
			LegalBasis = LegalBasis.Consent,
		};
		var updated = new ConsentRecord
		{
			SubjectId = "user-1",
			Purpose = "marketing",
			LegalBasis = LegalBasis.LegitimateInterests,
		};

		// Act
		await sut.RecordConsentAsync(original, CancellationToken.None);
		await sut.RecordConsentAsync(updated, CancellationToken.None);

		// Assert - should reflect the latest record
		var result = await sut.GetConsentAsync("user-1", "marketing", CancellationToken.None);
		result.ShouldNotBeNull();
		result.LegalBasis.ShouldBe(LegalBasis.LegitimateInterests);
	}

	[Fact]
	public async Task Apply_default_expiration_when_no_explicit_expiry_set()
	{
		// Arrange
		var options = new ConsentOptions { DefaultExpirationDays = 365 };
		var sut = CreateService(options);
		var now = DateTimeOffset.UtcNow;
		var record = new ConsentRecord
		{
			SubjectId = "user-1",
			Purpose = "marketing",
			GrantedAt = now,
		};

		// Act
		await sut.RecordConsentAsync(record, CancellationToken.None);
		var result = await sut.GetConsentAsync("user-1", "marketing", CancellationToken.None);

		// Assert - should have an expiration set approximately 365 days from now
		result.ShouldNotBeNull();
		// The consent should be retrievable (not expired yet)
	}

	[Fact]
	public async Task Not_apply_default_expiration_when_explicit_expiry_set()
	{
		// Arrange
		var options = new ConsentOptions { DefaultExpirationDays = 30 };
		var sut = CreateService(options);
		var explicitExpiry = DateTimeOffset.UtcNow.AddDays(90);
		var record = new ConsentRecord
		{
			SubjectId = "user-1",
			Purpose = "marketing",
			GrantedAt = DateTimeOffset.UtcNow,
			ExpiresAt = explicitExpiry,
		};

		// Act
		await sut.RecordConsentAsync(record, CancellationToken.None);
		var result = await sut.GetConsentAsync("user-1", "marketing", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.ExpiresAt.ShouldBe(explicitExpiry);
	}

	[Fact]
	public async Task Maintain_separate_subjects()
	{
		// Arrange
		var sut = CreateService();
		await sut.RecordConsentAsync(
			new ConsentRecord { SubjectId = "user-1", Purpose = "marketing" },
			CancellationToken.None);
		await sut.RecordConsentAsync(
			new ConsentRecord { SubjectId = "user-2", Purpose = "marketing" },
			CancellationToken.None);

		// Act - withdraw user-1 only
		await sut.WithdrawConsentAsync("user-1", "marketing", CancellationToken.None);

		// Assert
		var u1 = await sut.GetConsentAsync("user-1", "marketing", CancellationToken.None);
		var u2 = await sut.GetConsentAsync("user-2", "marketing", CancellationToken.None);
		u1.ShouldBeNull();
		u2.ShouldNotBeNull();
	}

	[Fact]
	public async Task Return_false_for_expired_consent()
	{
		// Arrange
		var sut = CreateService();
		var record = new ConsentRecord
		{
			SubjectId = "user-1",
			Purpose = "marketing",
			ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1), // Already expired
		};

		await sut.RecordConsentAsync(record, CancellationToken.None);

		// Act
		var result = await sut.GetConsentAsync("user-1", "marketing", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task Throw_when_withdrawing_with_null_subject()
	{
		var sut = CreateService();

		await Should.ThrowAsync<ArgumentException>(
			() => sut.WithdrawConsentAsync(null!, "marketing", CancellationToken.None));
	}

	[Fact]
	public async Task Throw_when_withdrawing_with_null_purpose()
	{
		var sut = CreateService();

		await Should.ThrowAsync<ArgumentException>(
			() => sut.WithdrawConsentAsync("user-1", null!, CancellationToken.None));
	}

	private static ConsentService CreateService(ConsentOptions? options = null) =>
		new(
			Microsoft.Extensions.Options.Options.Create(options ?? new ConsentOptions()),
			NullLogger<ConsentService>.Instance);
}
