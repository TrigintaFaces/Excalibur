// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Options;
using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Abstractions.Tests.Options;

/// <summary>
/// Unit tests for <see cref="SerializationOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SerializationOptionsShould
{
	[Fact]
	public void DefaultValues_AreCorrect()
	{
		// Act
		var options = new SerializationOptions();

		// Assert
		options.UseCompression.ShouldBeFalse();
		options.CompressionAlgorithm.ShouldBe(CompressionAlgorithm.None);
		options.IncludeTypeInfo.ShouldBeFalse();
		options.MaxMessageSize.ShouldBe(256 * 1024); // 256 KB
	}

	[Fact]
	public void UseCompression_CanBeSet()
	{
		// Arrange
		var options = new SerializationOptions();

		// Act
		options.UseCompression = true;

		// Assert
		options.UseCompression.ShouldBeTrue();
	}

	[Fact]
	public void CompressionAlgorithm_CanBeSet()
	{
		// Arrange
		var options = new SerializationOptions();

		// Act
		options.CompressionAlgorithm = CompressionAlgorithm.Gzip;

		// Assert
		options.CompressionAlgorithm.ShouldBe(CompressionAlgorithm.Gzip);
	}

	[Fact]
	public void MaxMessageSize_CanBeSet()
	{
		// Arrange
		var options = new SerializationOptions();

		// Act
		options.MaxMessageSize = 1024 * 1024; // 1 MB

		// Assert
		options.MaxMessageSize.ShouldBe(1024 * 1024);
	}
}
