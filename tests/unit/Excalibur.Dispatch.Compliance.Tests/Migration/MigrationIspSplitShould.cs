// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.Migration;

/// <summary>
/// ISP gate compliance and behavioral tests for the Sprint 612 migration service
/// interface split (A.3): IMigrationInfo, IMigrationService, EncryptionVersion,
/// and supporting result types.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Feature", "Migration")]
public sealed class MigrationIspSplitShould : UnitTestBase
{
	#region ISP Gate Compliance

	[Fact]
	public void IMigrationInfo_HaveExactlyOneProperty()
	{
		var props = typeof(IMigrationInfo)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

		props.Length.ShouldBe(1);
		props[0].Name.ShouldBe("CurrentVersion");
		props[0].PropertyType.ShouldBe(typeof(EncryptionVersion));
	}

	[Fact]
	public void IMigrationInfo_HaveNoMethods()
	{
		var methods = typeof(IMigrationInfo)
			.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
			.Where(m => !m.IsSpecialName)
			.ToArray();

		methods.Length.ShouldBe(0);
	}

	[Fact]
	public void IMigrationService_HaveAtMostFiveMethods()
	{
		var methods = typeof(IMigrationService)
			.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
			.Where(m => !m.IsSpecialName)
			.ToArray();

		methods.Length.ShouldBeLessThanOrEqualTo(5,
			$"IMigrationService has {methods.Length} methods: {string.Join(", ", methods.Select(m => m.Name))}");
	}

	[Fact]
	public void IMigrationService_NotInheritIMigrationInfo()
	{
		// ISP: IMigrationInfo is separate -- not inherited
		typeof(IMigrationService).GetInterfaces().ShouldNotContain(typeof(IMigrationInfo));
	}

	#endregion

	#region EncryptionVersion Struct

	[Fact]
	public void EncryptionVersion_Version10_BeV1Dot0()
	{
		EncryptionVersion.Version10.Major.ShouldBe(1);
		EncryptionVersion.Version10.Minor.ShouldBe(0);
	}

	[Fact]
	public void EncryptionVersion_Version11_BeV1Dot1()
	{
		EncryptionVersion.Version11.Major.ShouldBe(1);
		EncryptionVersion.Version11.Minor.ShouldBe(1);
	}

	[Fact]
	public void EncryptionVersion_Unknown_BeV0Dot0()
	{
		EncryptionVersion.Unknown.Major.ShouldBe(0);
		EncryptionVersion.Unknown.Minor.ShouldBe(0);
	}

	[Fact]
	public void EncryptionVersion_ToString_FormatWithPrefix()
	{
		EncryptionVersion.Version10.ToString().ShouldBe("v1.0");
		EncryptionVersion.Version11.ToString().ShouldBe("v1.1");
		EncryptionVersion.Unknown.ToString().ShouldBe("v0.0");
	}

	[Theory]
	[InlineData("v1.0", 1, 0)]
	[InlineData("V1.0", 1, 0)]
	[InlineData("1.0", 1, 0)]
	[InlineData("v1.1", 1, 1)]
	[InlineData("v2.3", 2, 3)]
	public void EncryptionVersion_Parse_ValidVersionStrings(string input, int major, int minor)
	{
		var version = EncryptionVersion.Parse(input);

		version.Major.ShouldBe(major);
		version.Minor.ShouldBe(minor);
	}

	[Theory]
	[InlineData("")]
	[InlineData(null)]
	public void EncryptionVersion_Parse_ReturnUnknownForEmptyOrNull(string? input)
	{
		var version = EncryptionVersion.Parse(input!);

		version.ShouldBe(EncryptionVersion.Unknown);
	}

	[Theory]
	[InlineData("abc")]
	[InlineData("v")]
	[InlineData("1")]
	public void EncryptionVersion_Parse_ReturnUnknownForInvalidFormat(string input)
	{
		var version = EncryptionVersion.Parse(input);

		version.ShouldBe(EncryptionVersion.Unknown);
	}

	[Fact]
	public void EncryptionVersion_CompareTo_OrderByMajorThenMinor()
	{
		var v10 = new EncryptionVersion(1, 0);
		var v11 = new EncryptionVersion(1, 1);
		var v20 = new EncryptionVersion(2, 0);

		v10.CompareTo(v11).ShouldBeLessThan(0);
		v11.CompareTo(v10).ShouldBeGreaterThan(0);
		v10.CompareTo(v10).ShouldBe(0);
		v11.CompareTo(v20).ShouldBeLessThan(0);
	}

	[Fact]
	public void EncryptionVersion_ComparisonOperators_WorkCorrectly()
	{
		var v10 = EncryptionVersion.Version10;
		var v10Copy = new EncryptionVersion(1, 0);
		var v11 = EncryptionVersion.Version11;
		var v11Copy = new EncryptionVersion(1, 1);

		(v10 < v11).ShouldBeTrue();
		(v11 > v10).ShouldBeTrue();
		(v10 <= v11).ShouldBeTrue();
		(v10 <= v10Copy).ShouldBeTrue();
		(v11 >= v10).ShouldBeTrue();
		(v11 >= v11Copy).ShouldBeTrue();

		(v10 > v11).ShouldBeFalse();
		(v11 < v10).ShouldBeFalse();
	}

	[Fact]
	public void EncryptionVersion_Equality_WorkForRecordStruct()
	{
		var a = new EncryptionVersion(1, 0);
		var b = new EncryptionVersion(1, 0);
		var c = new EncryptionVersion(1, 1);

		(a == b).ShouldBeTrue();
		(a != c).ShouldBeTrue();
		a.Equals(b).ShouldBeTrue();
		a.GetHashCode().ShouldBe(b.GetHashCode());
	}

	#endregion

	#region VersionMigrationResult Factory Methods

	[Fact]
	public void VersionMigrationResult_Succeeded_StoreAllFields()
	{
		var original = new byte[] { 1, 2 };
		var migrated = new byte[] { 3, 4 };
		var duration = TimeSpan.FromMilliseconds(42);

		var result = VersionMigrationResult.Succeeded(
			original, migrated, EncryptionVersion.Version10, EncryptionVersion.Version11, duration);

		result.Success.ShouldBeTrue();
		result.OriginalCiphertext.ShouldBe(original);
		result.MigratedCiphertext.ShouldBe(migrated);
		result.SourceVersion.ShouldBe(EncryptionVersion.Version10);
		result.TargetVersion.ShouldBe(EncryptionVersion.Version11);
		result.ErrorMessage.ShouldBeNull();
		result.Duration.ShouldBe(duration);
	}

	[Fact]
	public void VersionMigrationResult_Failed_StoreErrorAndNullMigrated()
	{
		var original = new byte[] { 1, 2 };
		var duration = TimeSpan.FromMilliseconds(10);

		var result = VersionMigrationResult.Failed(
			original, EncryptionVersion.Version10, EncryptionVersion.Version11, "Key not found", duration);

		result.Success.ShouldBeFalse();
		result.MigratedCiphertext.ShouldBeNull();
		result.ErrorMessage.ShouldBe("Key not found");
		result.OriginalCiphertext.ShouldBe(original);
	}

	[Fact]
	public void VersionMigrationResult_NotRequired_ReturnOriginalAsMigrated()
	{
		var original = new byte[] { 1, 2, 3 };

		var result = VersionMigrationResult.NotRequired(original, EncryptionVersion.Version11);

		result.Success.ShouldBeTrue();
		result.MigratedCiphertext.ShouldBeSameAs(original);
		result.SourceVersion.ShouldBe(result.TargetVersion);
		result.Duration.ShouldBe(TimeSpan.Zero);
		result.ErrorMessage.ShouldBeNull();
	}

	#endregion

	#region MigrationItem Record

	[Fact]
	public void MigrationItem_StoreIdAndCiphertext()
	{
		var data = new byte[] { 10, 20, 30 };
		var item = new MigrationItem("item-1", data);

		item.Id.ShouldBe("item-1");
		item.Ciphertext.ShouldBe(data);
	}

	#endregion

	#region VersionBatchMigrationResult Record

	[Fact]
	public void VersionBatchMigrationResult_StoreBatchStatistics()
	{
		var results = new List<(string Id, VersionMigrationResult Result)>
		{
			("item-1", VersionMigrationResult.NotRequired([], EncryptionVersion.Version11)),
			("item-2", VersionMigrationResult.Failed([], EncryptionVersion.Version10, EncryptionVersion.Version11, "err", TimeSpan.Zero))
		};
		var duration = TimeSpan.FromSeconds(2);

		var batch = new VersionBatchMigrationResult(
			TotalItems: 3,
			SuccessCount: 1,
			FailureCount: 1,
			SkippedCount: 1,
			Results: results,
			TotalDuration: duration);

		batch.TotalItems.ShouldBe(3);
		batch.SuccessCount.ShouldBe(1);
		batch.FailureCount.ShouldBe(1);
		batch.SkippedCount.ShouldBe(1);
		batch.Results.Count.ShouldBe(2);
		batch.TotalDuration.ShouldBe(duration);
	}

	#endregion

	#region VersionMigrationProgress Record

	[Fact]
	public void VersionMigrationProgress_CalculateCompletionPercentage()
	{
		var distribution = new Dictionary<EncryptionVersion, long>
		{
			[EncryptionVersion.Version10] = 25,
			[EncryptionVersion.Version11] = 75
		};

		var progress = new VersionMigrationProgress(
			TotalItemsDetected: 100,
			ItemsMigrated: 75,
			FailureCount: 5,
			VersionDistribution: distribution,
			LastUpdated: DateTimeOffset.UtcNow);

		progress.CompletionPercentage.ShouldBe(75.0);
	}

	[Fact]
	public void VersionMigrationProgress_Return100Percent_WhenNoItemsDetected()
	{
		var progress = new VersionMigrationProgress(
			TotalItemsDetected: 0,
			ItemsMigrated: 0,
			FailureCount: 0,
			VersionDistribution: new Dictionary<EncryptionVersion, long>(),
			LastUpdated: DateTimeOffset.UtcNow);

		progress.CompletionPercentage.ShouldBe(100.0);
	}

	#endregion
}
