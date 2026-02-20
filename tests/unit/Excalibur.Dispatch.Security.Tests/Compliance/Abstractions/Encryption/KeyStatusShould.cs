// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Encryption;

/// <summary>
/// Unit tests for <see cref="KeyStatus"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Feature", "Encryption")]
public sealed class KeyStatusShould : UnitTestBase
{
	[Fact]
	public void HaveFiveStatuses()
	{
		// Assert
		var values = Enum.GetValues<KeyStatus>();
		values.Length.ShouldBe(5);
	}

	[Fact]
	public void HaveActiveAsDefault()
	{
		// Assert - Active should be 0 (default for healthy keys)
		((int)KeyStatus.Active).ShouldBe(0);
	}

	[Theory]
	[InlineData(KeyStatus.Active, 0)]
	[InlineData(KeyStatus.DecryptOnly, 1)]
	[InlineData(KeyStatus.PendingDestruction, 2)]
	[InlineData(KeyStatus.Destroyed, 3)]
	[InlineData(KeyStatus.Suspended, 4)]
	public void HaveCorrectUnderlyingValues(KeyStatus status, int expectedValue)
	{
		// Assert
		((int)status).ShouldBe(expectedValue);
	}

	[Theory]
	[InlineData("Active", KeyStatus.Active)]
	[InlineData("DecryptOnly", KeyStatus.DecryptOnly)]
	[InlineData("PendingDestruction", KeyStatus.PendingDestruction)]
	[InlineData("Destroyed", KeyStatus.Destroyed)]
	[InlineData("Suspended", KeyStatus.Suspended)]
	public void ParseFromString(string input, KeyStatus expected)
	{
		// Act
		var result = Enum.Parse<KeyStatus>(input);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void DefaultToActiveWhenNotSpecified()
	{
		// Arrange
		KeyStatus defaultValue = default;

		// Assert
		defaultValue.ShouldBe(KeyStatus.Active);
	}

	[Fact]
	public void SupportKeyLifecycleOrdering()
	{
		// Lifecycle: Active -> DecryptOnly -> Destroyed
		((int)KeyStatus.Active).ShouldBeLessThan((int)KeyStatus.DecryptOnly);
		((int)KeyStatus.DecryptOnly).ShouldBeLessThan((int)KeyStatus.PendingDestruction);
		((int)KeyStatus.PendingDestruction).ShouldBeLessThan((int)KeyStatus.Destroyed);
	}

	[Theory]
	[InlineData(KeyStatus.Active, true)]
	[InlineData(KeyStatus.DecryptOnly, false)]
	[InlineData(KeyStatus.PendingDestruction, false)]
	[InlineData(KeyStatus.Destroyed, false)]
	[InlineData(KeyStatus.Suspended, false)]
	public void IdentifyUsableForEncryption(KeyStatus status, bool canEncrypt)
	{
		// Only Active keys can be used for encryption
		var actualCanEncrypt = status == KeyStatus.Active;
		actualCanEncrypt.ShouldBe(canEncrypt);
	}

	[Theory]
	[InlineData(KeyStatus.Active, true)]
	[InlineData(KeyStatus.DecryptOnly, true)]
	[InlineData(KeyStatus.PendingDestruction, false)]
	[InlineData(KeyStatus.Destroyed, false)]
	[InlineData(KeyStatus.Suspended, false)]
	public void IdentifyUsableForDecryption(KeyStatus status, bool canDecrypt)
	{
		// Active and DecryptOnly keys can be used for decryption
		var actualCanDecrypt = status == KeyStatus.Active || status == KeyStatus.DecryptOnly;
		actualCanDecrypt.ShouldBe(canDecrypt);
	}
}
