// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class CorrelationIdShould
{
	[Fact]
	public void StoreValue()
	{
		// Arrange & Act
		var id = new CorrelationId("abc-123");

		// Assert
		id.Value.ShouldBe("abc-123");
	}

	[Fact]
	public void ReturnValueFromToString()
	{
		// Arrange
		var id = new CorrelationId("corr-456");

		// Act
		var result = id.ToString();

		// Assert
		result.ShouldBe("corr-456");
	}
}
