// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Tests.Outbox;

/// <summary>
/// Unit tests for <see cref="PublishingError"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PublishingErrorShould
{
	[Fact]
	public void Constructor_SetsProperties()
	{
		// Act
		var error = new PublishingError("msg-1", "Connection refused");

		// Assert
		error.MessageId.ShouldBe("msg-1");
		error.Error.ShouldBe("Connection refused");
		error.Exception.ShouldBeNull();
		error.Timestamp.ShouldBeGreaterThan(DateTimeOffset.MinValue);
	}

	[Fact]
	public void Constructor_WithException_SetsException()
	{
		// Arrange
		var ex = new InvalidOperationException("Broken");

		// Act
		var error = new PublishingError("msg-1", "Failed", ex);

		// Assert
		error.Exception.ShouldBe(ex);
	}

	[Fact]
	public void Constructor_ThrowsOnNullMessageId()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new PublishingError(null!, "error"));
	}

	[Fact]
	public void Constructor_ThrowsOnNullError()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new PublishingError("msg-1", null!));
	}

	[Fact]
	public void ToString_ContainsMessageIdAndError()
	{
		// Arrange
		var error = new PublishingError("msg-42", "Timeout");

		// Act
		var str = error.ToString();

		// Assert
		str.ShouldContain("msg-42");
		str.ShouldContain("Timeout");
	}
}
