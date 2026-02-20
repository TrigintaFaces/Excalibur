// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Classification;

/// <summary>
/// Unit tests for <see cref="DataClassification"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Feature", "Classification")]
public sealed class DataClassificationShould : UnitTestBase
{
	[Fact]
	public void HaveFourClassificationLevels()
	{
		// Assert
		var values = Enum.GetValues<DataClassification>();
		values.Length.ShouldBe(4);
	}

	[Fact]
	public void HavePublicAsLowestLevel()
	{
		// Assert
		((int)DataClassification.Public).ShouldBe(0);
	}

	[Fact]
	public void HaveRestrictedAsHighestLevel()
	{
		// Assert
		((int)DataClassification.Restricted).ShouldBe(3);
	}

	[Fact]
	public void HaveCorrectOrderingFromLeastToMostSensitive()
	{
		// Assert
		((int)DataClassification.Public).ShouldBeLessThan((int)DataClassification.Internal);
		((int)DataClassification.Internal).ShouldBeLessThan((int)DataClassification.Confidential);
		((int)DataClassification.Confidential).ShouldBeLessThan((int)DataClassification.Restricted);
	}

	[Theory]
	[InlineData(DataClassification.Public, 0)]
	[InlineData(DataClassification.Internal, 1)]
	[InlineData(DataClassification.Confidential, 2)]
	[InlineData(DataClassification.Restricted, 3)]
	public void HaveCorrectUnderlyingValues(DataClassification classification, int expectedValue)
	{
		// Assert
		((int)classification).ShouldBe(expectedValue);
	}

	[Fact]
	public void SupportComparisonOperations()
	{
		// Assert - Can compare sensitivity levels
		(DataClassification.Public < DataClassification.Internal).ShouldBeTrue();
		(DataClassification.Restricted > DataClassification.Confidential).ShouldBeTrue();
		(DataClassification.Public <= DataClassification.Internal).ShouldBeTrue();
		(DataClassification.Confidential >= DataClassification.Internal).ShouldBeTrue();
	}

	[Theory]
	[InlineData("Public", DataClassification.Public)]
	[InlineData("Internal", DataClassification.Internal)]
	[InlineData("Confidential", DataClassification.Confidential)]
	[InlineData("Restricted", DataClassification.Restricted)]
	public void ParseFromString(string input, DataClassification expected)
	{
		// Act
		var result = Enum.Parse<DataClassification>(input);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData(DataClassification.Public, "Public")]
	[InlineData(DataClassification.Internal, "Internal")]
	[InlineData(DataClassification.Confidential, "Confidential")]
	[InlineData(DataClassification.Restricted, "Restricted")]
	public void ConvertToString(DataClassification classification, string expected)
	{
		// Act
		var result = classification.ToString();

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void BeUsableInEncryptionDecisions()
	{
		// This test demonstrates the typical usage pattern for classification-based encryption decisions
		var classifications = new[]
		{
			(Classification: DataClassification.Public, RequiresEncryption: false),
			(Classification: DataClassification.Internal, RequiresEncryption: false),
			(Classification: DataClassification.Confidential, RequiresEncryption: true),
			(Classification: DataClassification.Restricted, RequiresEncryption: true)
		};

		foreach (var (classification, requiresEncryption) in classifications)
		{
			// Rule: Confidential and above require encryption
			var shouldEncrypt = classification >= DataClassification.Confidential;
			shouldEncrypt.ShouldBe(requiresEncryption, $"Classification {classification} encryption requirement mismatch");
		}
	}

	[Fact]
	public void BeUsableInAuditLevelDecisions()
	{
		// This test demonstrates using classification for audit logging levels
		var classifications = new[]
		{
			(Classification: DataClassification.Public, AuditLevel: "Minimal"),
			(Classification: DataClassification.Internal, AuditLevel: "Standard"),
			(Classification: DataClassification.Confidential, AuditLevel: "Enhanced"),
			(Classification: DataClassification.Restricted, AuditLevel: "Comprehensive")
		};

		// All classifications should map to a defined audit level
		foreach (var (classification, auditLevel) in classifications)
		{
			auditLevel.ShouldNotBeNullOrWhiteSpace($"Classification {classification} should have audit level");
		}
	}
}
