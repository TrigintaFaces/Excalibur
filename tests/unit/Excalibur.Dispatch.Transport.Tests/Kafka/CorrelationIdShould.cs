// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Transport.Tests.Kafka;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class CorrelationIdShould
{
	private static readonly Guid TestGuid = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");

	[Fact]
	public void StoreValue()
	{
		// Arrange & Act
		var id = new CorrelationId(TestGuid);

		// Assert
		id.Value.ShouldBe(TestGuid);
	}

	[Fact]
	public void ReturnValueFromToString()
	{
		// Arrange
		var id = new CorrelationId(TestGuid);

		// Act
		var result = id.ToString();

		// Assert
		result.ShouldBe(TestGuid.ToString());
	}

	[Fact]
	public void ParseFromString()
	{
		// Arrange
		var guidString = TestGuid.ToString();

		// Act
		var id = new CorrelationId(guidString);

		// Assert
		id.Value.ShouldBe(TestGuid);
	}

	[Fact]
	public void GenerateDefaultGuid()
	{
		// Arrange & Act
		var id = new CorrelationId();

		// Assert
		id.Value.ShouldNotBe(Guid.Empty);
	}
}
