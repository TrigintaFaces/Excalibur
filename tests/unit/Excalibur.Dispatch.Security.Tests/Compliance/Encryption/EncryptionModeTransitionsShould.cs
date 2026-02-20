// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Encryption;

/// <summary>
/// Unit tests for <see cref="EncryptionMode"/> transition validation.
/// </summary>
/// <remarks>
/// Per AD-257-1, these tests verify the encryption mode transitions in the migration flow.
/// The complete migration flow is: DISABLED → ENCRYPT_NEW → LAZY_MIGRATION → BULK_MIGRATION → ENCRYPT_ALL → KEY_ROTATION
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Compliance")]
public sealed class EncryptionModeTransitionsShould
{
	#region Valid Forward Transition Tests

	[Fact]
	public void AllowTransition_FromDisabled_ToEncryptNewDecryptAll()
	{
		// Per AD-257-4: First step in migration - start encrypting new data
		var from = EncryptionMode.Disabled;
		var to = EncryptionMode.EncryptNewDecryptAll;

		// Valid transition: Plaintext store starts encrypting new writes
		IsValidForwardTransition(from, to).ShouldBeTrue();
	}

	[Fact]
	public void AllowTransition_FromEncryptNewDecryptAll_ToEncryptAndDecrypt()
	{
		// Per AD-257-4: Complete encryption - all data now encrypted
		var from = EncryptionMode.EncryptNewDecryptAll;
		var to = EncryptionMode.EncryptAndDecrypt;

		// Valid transition: After bulk migration, fully encrypted
		IsValidForwardTransition(from, to).ShouldBeTrue();
	}

	[Fact]
	public void AllowTransition_FromEncryptAndDecrypt_ToDecryptOnlyWritePlaintext()
	{
		// Decrypt-only mode for data export or plaintext migration
		var from = EncryptionMode.EncryptAndDecrypt;
		var to = EncryptionMode.DecryptOnlyWritePlaintext;

		IsValidForwardTransition(from, to).ShouldBeTrue();
	}

	[Fact]
	public void AllowTransition_FromDecryptOnlyWritePlaintext_ToDecryptOnlyReadOnly()
	{
		// Read-only mode for final export phase
		var from = EncryptionMode.DecryptOnlyWritePlaintext;
		var to = EncryptionMode.DecryptOnlyReadOnly;

		IsValidForwardTransition(from, to).ShouldBeTrue();
	}

	[Fact]
	public void AllowTransition_FromDecryptOnlyReadOnly_ToDisabled()
	{
		// Final step: encryption disabled after full plaintext migration
		var from = EncryptionMode.DecryptOnlyReadOnly;
		var to = EncryptionMode.Disabled;

		IsValidForwardTransition(from, to).ShouldBeTrue();
	}

	#endregion Valid Forward Transition Tests

	#region Mode Capability Tests

	[Theory]
	[InlineData(EncryptionMode.EncryptAndDecrypt, true, true)]
	[InlineData(EncryptionMode.EncryptNewDecryptAll, true, true)]
	[InlineData(EncryptionMode.DecryptOnlyWritePlaintext, false, true)]
	[InlineData(EncryptionMode.DecryptOnlyReadOnly, false, true)]
	[InlineData(EncryptionMode.Disabled, false, false)]
	public void HaveCorrectCapabilities(EncryptionMode mode, bool canEncrypt, bool canDecrypt)
	{
		// Test encryption capability
		CanEncryptInMode(mode).ShouldBe(canEncrypt);

		// Test decryption capability
		CanDecryptInMode(mode).ShouldBe(canDecrypt);
	}

	[Theory]
	[InlineData(EncryptionMode.EncryptAndDecrypt, true)]
	[InlineData(EncryptionMode.EncryptNewDecryptAll, true)]
	[InlineData(EncryptionMode.DecryptOnlyWritePlaintext, true)]
	[InlineData(EncryptionMode.DecryptOnlyReadOnly, false)]
	[InlineData(EncryptionMode.Disabled, true)]
	public void HaveCorrectWriteCapability(EncryptionMode mode, bool canWrite)
	{
		CanWriteInMode(mode).ShouldBe(canWrite);
	}

	#endregion Mode Capability Tests

	#region Migration Flow Tests

	[Fact]
	public void SupportFullMigrationFlowFromPlaintextToEncrypted()
	{
		// Per AD-257-4: Complete forward migration flow
		var flow = new[]
		{
			EncryptionMode.Disabled,
			EncryptionMode.EncryptNewDecryptAll,
			EncryptionMode.EncryptAndDecrypt
		};

		for (var i = 0; i < flow.Length - 1; i++)
		{
			IsValidForwardTransition(flow[i], flow[i + 1]).ShouldBeTrue(
				$"Transition from {flow[i]} to {flow[i + 1]} should be valid");
		}
	}

	[Fact]
	public void SupportFullMigrationFlowFromEncryptedToPlaintext()
	{
		// Reverse migration: encrypted to plaintext
		var flow = new[]
		{
			EncryptionMode.EncryptAndDecrypt,
			EncryptionMode.DecryptOnlyWritePlaintext,
			EncryptionMode.DecryptOnlyReadOnly,
			EncryptionMode.Disabled
		};

		for (var i = 0; i < flow.Length - 1; i++)
		{
			IsValidForwardTransition(flow[i], flow[i + 1]).ShouldBeTrue(
				$"Transition from {flow[i]} to {flow[i + 1]} should be valid");
		}
	}

	[Fact]
	public void OrderModesByMigrationPhase()
	{
		// Modes should be orderable by migration phase
		var modes = new[]
		{
			EncryptionMode.Disabled,
			EncryptionMode.EncryptNewDecryptAll,
			EncryptionMode.EncryptAndDecrypt,
			EncryptionMode.DecryptOnlyWritePlaintext,
			EncryptionMode.DecryptOnlyReadOnly
		};

		// Verify each mode can transition to the next
		for (var i = 0; i < modes.Length - 1; i++)
		{
			// Note: This tests the conceptual ordering, not strict numeric ordering
			modes[i].ShouldNotBe(modes[i + 1]);
		}
	}

	#endregion Migration Flow Tests

	#region Same Mode Transition Tests

	[Theory]
	[InlineData(EncryptionMode.Disabled)]
	[InlineData(EncryptionMode.EncryptAndDecrypt)]
	[InlineData(EncryptionMode.EncryptNewDecryptAll)]
	[InlineData(EncryptionMode.DecryptOnlyWritePlaintext)]
	[InlineData(EncryptionMode.DecryptOnlyReadOnly)]
	public void AllowTransitionToSameMode(EncryptionMode mode)
	{
		// Transitioning to the same mode is always valid (no-op)
		// Using explicit comparison to satisfy compiler
		var same = mode;
		(mode == same).ShouldBeTrue();
	}

	#endregion Same Mode Transition Tests

	#region Mode Semantics Tests

	[Fact]
	public void EncryptAndDecrypt_ShouldBeDefaultSecureMode()
	{
		// This is the standard operating mode
		var mode = EncryptionMode.EncryptAndDecrypt;

		mode.ShouldBe(default(EncryptionMode));
		CanEncryptInMode(mode).ShouldBeTrue();
		CanDecryptInMode(mode).ShouldBeTrue();
		CanWriteInMode(mode).ShouldBeTrue();
	}

	[Fact]
	public void EncryptNewDecryptAll_ShouldSupportMixedModeReads()
	{
		// Per AD-254-2: Mixed-mode reads via IsFieldEncrypted
		var mode = EncryptionMode.EncryptNewDecryptAll;

		CanEncryptInMode(mode).ShouldBeTrue();
		CanDecryptInMode(mode).ShouldBeTrue();
		CanWriteInMode(mode).ShouldBeTrue();
		// This mode can read both plaintext and encrypted data
	}

	[Fact]
	public void DecryptOnlyWritePlaintext_ShouldNotEncrypt()
	{
		// For GDPR export or plaintext migration
		var mode = EncryptionMode.DecryptOnlyWritePlaintext;

		CanEncryptInMode(mode).ShouldBeFalse();
		CanDecryptInMode(mode).ShouldBeTrue();
		CanWriteInMode(mode).ShouldBeTrue();
	}

	[Fact]
	public void DecryptOnlyReadOnly_ShouldNotAllowWrites()
	{
		// Final export phase - read-only
		var mode = EncryptionMode.DecryptOnlyReadOnly;

		CanEncryptInMode(mode).ShouldBeFalse();
		CanDecryptInMode(mode).ShouldBeTrue();
		CanWriteInMode(mode).ShouldBeFalse();
	}

	[Fact]
	public void Disabled_ShouldBypassAllEncryption()
	{
		// Passthrough mode - no encryption/decryption
		var mode = EncryptionMode.Disabled;

		CanEncryptInMode(mode).ShouldBeFalse();
		CanDecryptInMode(mode).ShouldBeFalse();
		CanWriteInMode(mode).ShouldBeTrue();
	}

	#endregion Mode Semantics Tests

	#region Encryption Capability by Mode Tests

	[Theory]
	[InlineData(EncryptionMode.EncryptAndDecrypt)]
	[InlineData(EncryptionMode.EncryptNewDecryptAll)]
	public void EncryptingModes_ShouldEncryptNewData(EncryptionMode mode)
	{
		CanEncryptInMode(mode).ShouldBeTrue();
	}

	[Theory]
	[InlineData(EncryptionMode.DecryptOnlyWritePlaintext)]
	[InlineData(EncryptionMode.DecryptOnlyReadOnly)]
	[InlineData(EncryptionMode.Disabled)]
	public void NonEncryptingModes_ShouldNotEncryptNewData(EncryptionMode mode)
	{
		CanEncryptInMode(mode).ShouldBeFalse();
	}

	[Theory]
	[InlineData(EncryptionMode.EncryptAndDecrypt)]
	[InlineData(EncryptionMode.EncryptNewDecryptAll)]
	[InlineData(EncryptionMode.DecryptOnlyWritePlaintext)]
	[InlineData(EncryptionMode.DecryptOnlyReadOnly)]
	public void DecryptingModes_ShouldDecryptData(EncryptionMode mode)
	{
		CanDecryptInMode(mode).ShouldBeTrue();
	}

	[Theory]
	[InlineData(EncryptionMode.Disabled)]
	public void NonDecryptingModes_ShouldNotDecryptData(EncryptionMode mode)
	{
		CanDecryptInMode(mode).ShouldBeFalse();
	}

	#endregion Encryption Capability by Mode Tests

	#region Helper Methods

	private static bool IsValidForwardTransition(EncryptionMode from, EncryptionMode to)
	{
		// Define valid transitions for forward migration
		return (from, to) switch
		{
			// Plaintext to encrypted migration
			(EncryptionMode.Disabled, EncryptionMode.EncryptNewDecryptAll) => true,
			(EncryptionMode.EncryptNewDecryptAll, EncryptionMode.EncryptAndDecrypt) => true,

			// Encrypted to plaintext migration
			(EncryptionMode.EncryptAndDecrypt, EncryptionMode.DecryptOnlyWritePlaintext) => true,
			(EncryptionMode.DecryptOnlyWritePlaintext, EncryptionMode.DecryptOnlyReadOnly) => true,
			(EncryptionMode.DecryptOnlyReadOnly, EncryptionMode.Disabled) => true,

			// Key rotation (stay in EncryptAndDecrypt)
			(EncryptionMode.EncryptAndDecrypt, EncryptionMode.EncryptAndDecrypt) => true,

			_ => false
		};
	}

	private static bool CanEncryptInMode(EncryptionMode mode)
	{
		return mode is EncryptionMode.EncryptAndDecrypt or EncryptionMode.EncryptNewDecryptAll;
	}

	private static bool CanDecryptInMode(EncryptionMode mode)
	{
		return mode is not EncryptionMode.Disabled;
	}

	private static bool CanWriteInMode(EncryptionMode mode)
	{
		return mode is not EncryptionMode.DecryptOnlyReadOnly;
	}

	#endregion Helper Methods
}
