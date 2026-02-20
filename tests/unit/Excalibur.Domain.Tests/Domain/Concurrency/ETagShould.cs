// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Concurrency;

namespace Excalibur.Tests.Domain.Concurrency;

/// <summary>
/// Unit tests for <see cref="ETag"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ETagShould
{
	#region T419.12: ETag Tests

	[Fact]
	public void Create_HasEmptyDefaultValues()
	{
		// Arrange & Act
		var etag = new ETag();

		// Assert
		etag.IncomingValue.ShouldBe(string.Empty);
		etag.OutgoingValue.ShouldBe(string.Empty);
	}

	[Fact]
	public void IncomingValue_CanBeSetAndRetrieved()
	{
		// Arrange
		var etag = new ETag();

		// Act
		etag.IncomingValue = "incoming-etag-123";

		// Assert
		etag.IncomingValue.ShouldBe("incoming-etag-123");
	}

	[Fact]
	public void OutgoingValue_CanBeSetAndRetrieved()
	{
		// Arrange
		var etag = new ETag();

		// Act
		etag.OutgoingValue = "outgoing-etag-456";

		// Assert
		etag.OutgoingValue.ShouldBe("outgoing-etag-456");
	}

	[Fact]
	public void IncomingValue_CanBeSetToNull()
	{
		// Arrange
		var etag = new ETag { IncomingValue = "initial-value" };

		// Act
		etag.IncomingValue = null;

		// Assert
		etag.IncomingValue.ShouldBeNull();
	}

	[Fact]
	public void OutgoingValue_CanBeSetToNull()
	{
		// Arrange
		var etag = new ETag { OutgoingValue = "initial-value" };

		// Act
		etag.OutgoingValue = null;

		// Assert
		etag.OutgoingValue.ShouldBeNull();
	}

	[Fact]
	public void ImplementsIETagInterface()
	{
		// Arrange & Act
		var etag = new ETag();

		// Assert
		_ = etag.ShouldBeAssignableTo<IETag>();
	}

	[Fact]
	public void Comparison_CanCompareIncomingAndOutgoing()
	{
		// Arrange
		var etag = new ETag
		{
			IncomingValue = "version-1",
			OutgoingValue = "version-2",
		};

		// Act & Assert
		(etag.IncomingValue == etag.OutgoingValue).ShouldBeFalse();
	}

	[Fact]
	public void ValuesMatch_WhenBothAreTheSame()
	{
		// Arrange
		const string version = "version-abc";
		var etag = new ETag
		{
			IncomingValue = version,
			OutgoingValue = version,
		};

		// Act & Assert
		etag.IncomingValue.ShouldBe(etag.OutgoingValue);
	}

	#endregion T419.12: ETag Tests
}
