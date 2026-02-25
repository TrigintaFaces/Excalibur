// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Encryption;

/// <summary>
/// Unit tests for <see cref="EncryptionMode"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Feature", "Encryption")]
public sealed class EncryptionModeShould : UnitTestBase
{
	[Fact]
	public void HaveFiveModes()
	{
		// Assert
		var values = Enum.GetValues<EncryptionMode>();
		values.Length.ShouldBe(5);
	}

	[Fact]
	public void HaveEncryptAndDecryptAsDefault()
	{
		// Assert - EncryptAndDecrypt should be 0 (default)
		((int)EncryptionMode.EncryptAndDecrypt).ShouldBe(0);
	}

	[Theory]
	[InlineData(EncryptionMode.EncryptAndDecrypt, 0)]
	[InlineData(EncryptionMode.EncryptNewDecryptAll, 1)]
	[InlineData(EncryptionMode.DecryptOnlyWritePlaintext, 2)]
	[InlineData(EncryptionMode.DecryptOnlyReadOnly, 3)]
	[InlineData(EncryptionMode.Disabled, 4)]
	public void HaveCorrectUnderlyingValues(EncryptionMode mode, int expectedValue)
	{
		// Assert
		((int)mode).ShouldBe(expectedValue);
	}

	[Theory]
	[InlineData("EncryptAndDecrypt", EncryptionMode.EncryptAndDecrypt)]
	[InlineData("EncryptNewDecryptAll", EncryptionMode.EncryptNewDecryptAll)]
	[InlineData("DecryptOnlyWritePlaintext", EncryptionMode.DecryptOnlyWritePlaintext)]
	[InlineData("DecryptOnlyReadOnly", EncryptionMode.DecryptOnlyReadOnly)]
	[InlineData("Disabled", EncryptionMode.Disabled)]
	public void ParseFromString(string input, EncryptionMode expected)
	{
		// Act
		var result = Enum.Parse<EncryptionMode>(input);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void DefaultToEncryptAndDecryptWhenNotSpecified()
	{
		// Arrange
		EncryptionMode defaultValue = default;

		// Assert
		defaultValue.ShouldBe(EncryptionMode.EncryptAndDecrypt);
	}

	[Fact]
	public void SupportMigrationPathOrdering()
	{
		// Migration path: EncryptAndDecrypt → EncryptNewDecryptAll → DecryptOnlyWritePlaintext → Disabled
		// This ordering should be numerically sequential
		((int)EncryptionMode.EncryptAndDecrypt).ShouldBeLessThan((int)EncryptionMode.EncryptNewDecryptAll);
		((int)EncryptionMode.EncryptNewDecryptAll).ShouldBeLessThan((int)EncryptionMode.DecryptOnlyWritePlaintext);
		((int)EncryptionMode.DecryptOnlyWritePlaintext).ShouldBeLessThan((int)EncryptionMode.DecryptOnlyReadOnly);
		((int)EncryptionMode.DecryptOnlyReadOnly).ShouldBeLessThan((int)EncryptionMode.Disabled);
	}

	[Fact]
	public void BeUsableInSwitchStatements()
	{
		var modes = Enum.GetValues<EncryptionMode>();

		foreach (var mode in modes)
		{
			var canEncrypt = mode switch
			{
				EncryptionMode.EncryptAndDecrypt => true,
				EncryptionMode.EncryptNewDecryptAll => true,
				EncryptionMode.DecryptOnlyWritePlaintext => false,
				EncryptionMode.DecryptOnlyReadOnly => false,
				EncryptionMode.Disabled => false,
				_ => throw new ArgumentOutOfRangeException(nameof(mode))
			};

			// Verify all modes are handled
			Enum.IsDefined(mode).ShouldBeTrue();
		}
	}
}
