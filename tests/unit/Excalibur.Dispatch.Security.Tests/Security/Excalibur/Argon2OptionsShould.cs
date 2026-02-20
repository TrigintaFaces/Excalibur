// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Security;

namespace Excalibur.Dispatch.Security.Tests.Excalibur;

/// <summary>
/// Unit tests for <see cref="Argon2Options"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "PasswordHashing")]
public sealed class Argon2OptionsShould
{
	[Fact]
	public void HaveSectionName()
	{
		// Assert
		Argon2Options.SectionName.ShouldBe("Argon2");
	}

	[Fact]
	public void HaveDefaultMemorySizeOf65536KB()
	{
		// Act
		var options = new Argon2Options();

		// Assert - OWASP recommends 64 MB
		options.MemorySize.ShouldBe(65536);
	}

	[Fact]
	public void HaveDefaultIterationsOfFour()
	{
		// Act
		var options = new Argon2Options();

		// Assert - OWASP recommendation
		options.Iterations.ShouldBe(4);
	}

	[Fact]
	public void HaveDefaultParallelismOfFour()
	{
		// Act
		var options = new Argon2Options();

		// Assert - OWASP recommendation
		options.Parallelism.ShouldBe(4);
	}

	[Fact]
	public void HaveDefaultHashLengthOf32Bytes()
	{
		// Act
		var options = new Argon2Options();

		// Assert - 256 bits
		options.HashLength.ShouldBe(32);
	}

	[Fact]
	public void HaveDefaultSaltLengthOf16Bytes()
	{
		// Act
		var options = new Argon2Options();

		// Assert - OWASP recommendation of 128 bits
		options.SaltLength.ShouldBe(16);
	}

	[Fact]
	public void HaveDefaultVersionOfOne()
	{
		// Act
		var options = new Argon2Options();

		// Assert
		options.Version.ShouldBe(1);
	}

	[Fact]
	public void AllowCustomMemorySize()
	{
		// Act
		var options = new Argon2Options { MemorySize = 131072 }; // 128 MB

		// Assert
		options.MemorySize.ShouldBe(131072);
	}

	[Fact]
	public void AllowCustomIterations()
	{
		// Act
		var options = new Argon2Options { Iterations = 6 };

		// Assert
		options.Iterations.ShouldBe(6);
	}

	[Fact]
	public void AllowCustomParallelism()
	{
		// Act
		var options = new Argon2Options { Parallelism = 8 };

		// Assert
		options.Parallelism.ShouldBe(8);
	}

	[Fact]
	public void AllowCustomHashLength()
	{
		// Act
		var options = new Argon2Options { HashLength = 64 }; // 512 bits

		// Assert
		options.HashLength.ShouldBe(64);
	}

	[Fact]
	public void AllowCustomSaltLength()
	{
		// Act
		var options = new Argon2Options { SaltLength = 32 }; // 256 bits

		// Assert
		options.SaltLength.ShouldBe(32);
	}

	[Fact]
	public void AllowCustomVersion()
	{
		// Act
		var options = new Argon2Options { Version = 2 };

		// Assert
		options.Version.ShouldBe(2);
	}

	[Fact]
	public void CreateFullyConfiguredOptions()
	{
		// Act
		var options = new Argon2Options
		{
			MemorySize = 131072,
			Iterations = 6,
			Parallelism = 8,
			HashLength = 64,
			SaltLength = 32,
			Version = 3
		};

		// Assert
		options.MemorySize.ShouldBe(131072);
		options.Iterations.ShouldBe(6);
		options.Parallelism.ShouldBe(8);
		options.HashLength.ShouldBe(64);
		options.SaltLength.ShouldBe(32);
		options.Version.ShouldBe(3);
	}
}
