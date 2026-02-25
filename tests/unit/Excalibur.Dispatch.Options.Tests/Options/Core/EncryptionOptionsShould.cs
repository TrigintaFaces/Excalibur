// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

/// <summary>
/// Unit tests for <see cref="EncryptionOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class EncryptionOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Enabled_IsFalse()
	{
		// Arrange & Act
		var options = new EncryptionOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void Default_Algorithm_IsAes256Gcm()
	{
		// Arrange & Act
		var options = new EncryptionOptions();

		// Assert
		options.Algorithm.ShouldBe(EncryptionAlgorithm.Aes256Gcm);
	}

	[Fact]
	public void Default_Key_IsNull()
	{
		// Arrange & Act
		var options = new EncryptionOptions();

		// Assert
		options.Key.ShouldBeNull();
	}

	[Fact]
	public void Default_KeyDerivation_IsNull()
	{
		// Arrange & Act
		var options = new EncryptionOptions();

		// Assert
		options.KeyDerivation.ShouldBeNull();
	}

	[Fact]
	public void Default_EnableKeyRotation_IsFalse()
	{
		// Arrange & Act
		var options = new EncryptionOptions();

		// Assert
		options.EnableKeyRotation.ShouldBeFalse();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Enabled_CanBeSet()
	{
		// Arrange
		var options = new EncryptionOptions();

		// Act
		options.Enabled = true;

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void Algorithm_CanBeSet()
	{
		// Arrange
		var options = new EncryptionOptions();

		// Act
		options.Algorithm = EncryptionAlgorithm.Aes128Gcm;

		// Assert
		options.Algorithm.ShouldBe(EncryptionAlgorithm.Aes128Gcm);
	}

	[Fact]
	public void Key_CanBeSet()
	{
		// Arrange
		var options = new EncryptionOptions();
		var key = new byte[] { 1, 2, 3, 4, 5 };

		// Act
		options.Key = key;

		// Assert
		options.Key.ShouldBe(key);
	}

	[Fact]
	public void EnableKeyRotation_CanBeSet()
	{
		// Arrange
		var options = new EncryptionOptions();

		// Act
		options.EnableKeyRotation = true;

		// Assert
		options.EnableKeyRotation.ShouldBeTrue();
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Arrange
		var key = new byte[] { 1, 2, 3, 4 };

		// Act
		var options = new EncryptionOptions
		{
			Enabled = true,
			Algorithm = EncryptionAlgorithm.Aes128Gcm,
			Key = key,
			EnableKeyRotation = true,
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.Algorithm.ShouldBe(EncryptionAlgorithm.Aes128Gcm);
		options.Key.ShouldBe(key);
		options.EnableKeyRotation.ShouldBeTrue();
	}

	#endregion
}
