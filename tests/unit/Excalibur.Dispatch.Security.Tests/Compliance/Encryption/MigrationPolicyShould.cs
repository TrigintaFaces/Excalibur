// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Encryption;

/// <summary>
/// Unit tests for <see cref="MigrationPolicy"/> record.
/// </summary>
/// <remarks>
/// Per AD-257-1, these tests verify the migration policy configuration for encryption migrations.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Compliance")]
public sealed class MigrationPolicyShould
{
	#region Default Value Tests

	[Fact]
	public void HaveNullMaxKeyAgeByDefault()
	{
		// Arrange & Act
		var policy = new MigrationPolicy();

		// Assert
		policy.MaxKeyAge.ShouldBeNull();
	}

	[Fact]
	public void HaveNullMinKeyVersionByDefault()
	{
		// Arrange & Act
		var policy = new MigrationPolicy();

		// Assert
		policy.MinKeyVersion.ShouldBeNull();
	}

	[Fact]
	public void HaveNullTargetAlgorithmByDefault()
	{
		// Arrange & Act
		var policy = new MigrationPolicy();

		// Assert
		policy.TargetAlgorithm.ShouldBeNull();
	}

	[Fact]
	public void HaveNullDeprecatedAlgorithmsByDefault()
	{
		// Arrange & Act
		var policy = new MigrationPolicy();

		// Assert
		policy.DeprecatedAlgorithms.ShouldBeNull();
	}

	[Fact]
	public void HaveNullDeprecatedKeyIdsByDefault()
	{
		// Arrange & Act
		var policy = new MigrationPolicy();

		// Assert
		policy.DeprecatedKeyIds.ShouldBeNull();
	}

	[Fact]
	public void HaveNullEncryptedBeforeByDefault()
	{
		// Arrange & Act
		var policy = new MigrationPolicy();

		// Assert
		policy.EncryptedBefore.ShouldBeNull();
	}

	[Fact]
	public void HaveRequireFipsComplianceFalseByDefault()
	{
		// Arrange & Act
		var policy = new MigrationPolicy();

		// Assert
		policy.RequireFipsCompliance.ShouldBeFalse();
	}

	[Fact]
	public void HaveNullTenantIdsByDefault()
	{
		// Arrange & Act
		var policy = new MigrationPolicy();

		// Assert
		policy.TenantIds.ShouldBeNull();
	}

	#endregion Default Value Tests

	#region Static Factory Tests

	[Fact]
	public void Default_ShouldHave90DayMaxKeyAge()
	{
		// Arrange & Act
		var policy = MigrationPolicy.Default;

		// Assert
		_ = policy.MaxKeyAge.ShouldNotBeNull();
		policy.MaxKeyAge.Value.TotalDays.ShouldBe(90);
	}

	[Fact]
	public void ForAlgorithm_ShouldSetTargetAlgorithm()
	{
		// Arrange & Act
		var policy = MigrationPolicy.ForAlgorithm(EncryptionAlgorithm.Aes256Gcm);

		// Assert
		policy.TargetAlgorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);
	}

	[Fact]
	public void ForDeprecatedKeys_ShouldSetDeprecatedKeyIds()
	{
		// Arrange & Act
		var policy = MigrationPolicy.ForDeprecatedKeys("key-2022", "key-2023");

		// Assert
		_ = policy.DeprecatedKeyIds.ShouldNotBeNull();
		policy.DeprecatedKeyIds.Count.ShouldBe(2);
		policy.DeprecatedKeyIds.ShouldContain("key-2022");
		policy.DeprecatedKeyIds.ShouldContain("key-2023");
	}

	[Fact]
	public void ForDeprecatedKeys_ShouldHandleSingleKey()
	{
		// Arrange & Act
		var policy = MigrationPolicy.ForDeprecatedKeys("old-key");

		// Assert
		_ = policy.DeprecatedKeyIds.ShouldNotBeNull();
		policy.DeprecatedKeyIds.Count.ShouldBe(1);
		policy.DeprecatedKeyIds.ShouldContain("old-key");
	}

	[Fact]
	public void ForDeprecatedKeys_ShouldUseCaseSensitiveComparison()
	{
		// Arrange & Act
		var policy = MigrationPolicy.ForDeprecatedKeys("Key-A", "key-a");

		// Assert - Both should be present (case-sensitive)
		_ = policy.DeprecatedKeyIds.ShouldNotBeNull();
		policy.DeprecatedKeyIds.Count.ShouldBe(2);
	}

	#endregion Static Factory Tests

	#region Property Assignment Tests

	[Theory]
	[InlineData(30)]
	[InlineData(60)]
	[InlineData(90)]
	[InlineData(365)]
	public void AllowMaxKeyAgeConfiguration(int days)
	{
		// Arrange & Act
		var policy = new MigrationPolicy { MaxKeyAge = TimeSpan.FromDays(days) };

		// Assert
		policy.MaxKeyAge.ShouldBe(TimeSpan.FromDays(days));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(5)]
	[InlineData(10)]
	public void AllowMinKeyVersionConfiguration(int version)
	{
		// Arrange & Act
		var policy = new MigrationPolicy { MinKeyVersion = version };

		// Assert
		policy.MinKeyVersion.ShouldBe(version);
	}

	[Theory]
	[InlineData(EncryptionAlgorithm.Aes256Gcm)]
	[InlineData(EncryptionAlgorithm.Aes256CbcHmac)]
	public void AllowTargetAlgorithmConfiguration(EncryptionAlgorithm algorithm)
	{
		// Arrange & Act
		var policy = new MigrationPolicy { TargetAlgorithm = algorithm };

		// Assert
		policy.TargetAlgorithm.ShouldBe(algorithm);
	}

	[Fact]
	public void AllowDeprecatedAlgorithmsConfiguration()
	{
		// Arrange
		var deprecated = new HashSet<EncryptionAlgorithm> { EncryptionAlgorithm.Aes256CbcHmac };

		// Act
		var policy = new MigrationPolicy { DeprecatedAlgorithms = deprecated };

		// Assert
		_ = policy.DeprecatedAlgorithms.ShouldNotBeNull();
		policy.DeprecatedAlgorithms.ShouldContain(EncryptionAlgorithm.Aes256CbcHmac);
	}

	[Fact]
	public void AllowEncryptedBeforeConfiguration()
	{
		// Arrange
		var cutoffDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

		// Act
		var policy = new MigrationPolicy { EncryptedBefore = cutoffDate };

		// Assert
		policy.EncryptedBefore.ShouldBe(cutoffDate);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void AllowRequireFipsComplianceConfiguration(bool require)
	{
		// Arrange & Act
		var policy = new MigrationPolicy { RequireFipsCompliance = require };

		// Assert
		policy.RequireFipsCompliance.ShouldBe(require);
	}

	[Fact]
	public void AllowTenantIdsConfiguration()
	{
		// Arrange
		var tenants = new HashSet<string> { "tenant-a", "tenant-b" };

		// Act
		var policy = new MigrationPolicy { TenantIds = tenants };

		// Assert
		_ = policy.TenantIds.ShouldNotBeNull();
		policy.TenantIds.Count.ShouldBe(2);
		policy.TenantIds.ShouldContain("tenant-a");
		policy.TenantIds.ShouldContain("tenant-b");
	}

	#endregion Property Assignment Tests

	#region Semantic Tests

	[Fact]
	public void BeFullyConfigurable()
	{
		// Arrange
		var deprecated = new HashSet<EncryptionAlgorithm> { EncryptionAlgorithm.Aes256CbcHmac };
		var deprecatedKeys = new HashSet<string> { "old-key" };
		var tenants = new HashSet<string> { "tenant-1" };
		var cutoffDate = DateTimeOffset.UtcNow.AddDays(-30);

		// Act
		var policy = new MigrationPolicy
		{
			MaxKeyAge = TimeSpan.FromDays(90),
			MinKeyVersion = 3,
			TargetAlgorithm = EncryptionAlgorithm.Aes256Gcm,
			DeprecatedAlgorithms = deprecated,
			DeprecatedKeyIds = deprecatedKeys,
			EncryptedBefore = cutoffDate,
			RequireFipsCompliance = true,
			TenantIds = tenants
		};

		// Assert
		policy.MaxKeyAge.ShouldBe(TimeSpan.FromDays(90));
		policy.MinKeyVersion.ShouldBe(3);
		policy.TargetAlgorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);
		policy.DeprecatedAlgorithms.ShouldContain(EncryptionAlgorithm.Aes256CbcHmac);
		policy.DeprecatedKeyIds.ShouldContain("old-key");
		policy.EncryptedBefore.ShouldBe(cutoffDate);
		policy.RequireFipsCompliance.ShouldBeTrue();
		policy.TenantIds.ShouldContain("tenant-1");
	}

	[Fact]
	public void SupportKeyRotationPolicy()
	{
		// Key rotation: migrate data using old keys
		var policy = MigrationPolicy.ForDeprecatedKeys("key-v1", "key-v2");

		_ = policy.DeprecatedKeyIds.ShouldNotBeNull();
		policy.DeprecatedKeyIds.Count.ShouldBe(2);
	}

	[Fact]
	public void SupportAlgorithmUpgradePolicy()
	{
		// Algorithm upgrade: migrate to stronger algorithm
		var policy = new MigrationPolicy
		{
			TargetAlgorithm = EncryptionAlgorithm.Aes256Gcm,
			DeprecatedAlgorithms = new HashSet<EncryptionAlgorithm> { EncryptionAlgorithm.Aes256CbcHmac }
		};

		policy.TargetAlgorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);
		policy.DeprecatedAlgorithms.ShouldContain(EncryptionAlgorithm.Aes256CbcHmac);
	}

	[Fact]
	public void SupportComplianceMigrationPolicy()
	{
		// FIPS compliance: require migration to FIPS-validated implementations
		var policy = new MigrationPolicy
		{
			RequireFipsCompliance = true,
			TargetAlgorithm = EncryptionAlgorithm.Aes256Gcm
		};

		policy.RequireFipsCompliance.ShouldBeTrue();
	}

	[Fact]
	public void SupportTenantScopedMigration()
	{
		// Multi-tenant migration: only migrate specific tenants
		var policy = new MigrationPolicy
		{
			TenantIds = new HashSet<string> { "premium-tenant-1", "premium-tenant-2" }
		};

		_ = policy.TenantIds.ShouldNotBeNull();
		policy.TenantIds.Count.ShouldBe(2);
	}

	#endregion Semantic Tests

	#region Record Equality Tests

	[Fact]
	public void BeEqualWithSameProperties()
	{
		// Arrange
		var policy1 = new MigrationPolicy { MaxKeyAge = TimeSpan.FromDays(90) };
		var policy2 = new MigrationPolicy { MaxKeyAge = TimeSpan.FromDays(90) };

		// Assert
		policy1.ShouldBe(policy2);
	}

	[Fact]
	public void NotBeEqualWithDifferentProperties()
	{
		// Arrange
		var policy1 = new MigrationPolicy { MaxKeyAge = TimeSpan.FromDays(90) };
		var policy2 = new MigrationPolicy { MaxKeyAge = TimeSpan.FromDays(30) };

		// Assert
		policy1.ShouldNotBe(policy2);
	}

	#endregion Record Equality Tests
}
