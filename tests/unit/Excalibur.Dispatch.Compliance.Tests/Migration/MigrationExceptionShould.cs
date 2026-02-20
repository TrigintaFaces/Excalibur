// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.Migration;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class MigrationExceptionShould
{
	[Fact]
	public void CreateWithMessage()
	{
		// Act
		var ex = new MigrationException("test error");

		// Assert
		ex.Message.ShouldBe("test error");
		ex.InnerException.ShouldBeNull();
	}

	[Fact]
	public void CreateWithMessageAndInnerException()
	{
		// Arrange
		var inner = new InvalidOperationException("inner");

		// Act
		var ex = new MigrationException("outer", inner);

		// Assert
		ex.Message.ShouldBe("outer");
		ex.InnerException.ShouldBe(inner);
	}
}
