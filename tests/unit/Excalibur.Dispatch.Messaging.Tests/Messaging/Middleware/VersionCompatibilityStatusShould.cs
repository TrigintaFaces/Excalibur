// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Middleware;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for <see cref="VersionCompatibilityStatus"/>.
/// </summary>
/// <remarks>
/// Tests the version compatibility status enumeration values.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
[Trait("Priority", "0")]
public sealed class VersionCompatibilityStatusShould
{
	#region Enum Value Tests

	[Fact]
	public void Compatible_HasValue0()
	{
		// Assert
		((int)VersionCompatibilityStatus.Compatible).ShouldBe(0);
	}

	[Fact]
	public void Deprecated_HasValue1()
	{
		// Assert
		((int)VersionCompatibilityStatus.Deprecated).ShouldBe(1);
	}

	[Fact]
	public void Incompatible_HasValue2()
	{
		// Assert
		((int)VersionCompatibilityStatus.Incompatible).ShouldBe(2);
	}

	[Fact]
	public void Unknown_HasValue3()
	{
		// Assert
		((int)VersionCompatibilityStatus.Unknown).ShouldBe(3);
	}

	#endregion

	#region Enum Completeness Tests

	[Fact]
	public void HasExpectedNumberOfValues()
	{
		// Arrange
		var values = Enum.GetValues<VersionCompatibilityStatus>();

		// Assert
		values.Length.ShouldBe(4);
	}

	[Theory]
	[InlineData(VersionCompatibilityStatus.Compatible, "Compatible")]
	[InlineData(VersionCompatibilityStatus.Deprecated, "Deprecated")]
	[InlineData(VersionCompatibilityStatus.Incompatible, "Incompatible")]
	[InlineData(VersionCompatibilityStatus.Unknown, "Unknown")]
	public void ToString_ReturnsExpectedName(VersionCompatibilityStatus status, string expectedName)
	{
		// Act
		var result = status.ToString();

		// Assert
		result.ShouldBe(expectedName);
	}

	#endregion

	#region Parse Tests

	[Theory]
	[InlineData("Compatible", VersionCompatibilityStatus.Compatible)]
	[InlineData("Deprecated", VersionCompatibilityStatus.Deprecated)]
	[InlineData("Incompatible", VersionCompatibilityStatus.Incompatible)]
	[InlineData("Unknown", VersionCompatibilityStatus.Unknown)]
	public void Parse_WithValidString_ReturnsExpectedStatus(string input, VersionCompatibilityStatus expected)
	{
		// Act
		var result = Enum.Parse<VersionCompatibilityStatus>(input);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void Parse_WithInvalidString_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => Enum.Parse<VersionCompatibilityStatus>("PartiallyCompatible"));
	}

	[Fact]
	public void TryParse_WithInvalidString_ReturnsFalse()
	{
		// Act
		var result = Enum.TryParse<VersionCompatibilityStatus>("PartiallyCompatible", out _);

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region IsDefined Tests

	[Theory]
	[InlineData(0, true)]
	[InlineData(1, true)]
	[InlineData(2, true)]
	[InlineData(3, true)]
	[InlineData(4, false)]
	[InlineData(-1, false)]
	public void IsDefined_WithIntValue_ReturnsExpected(int value, bool expected)
	{
		// Act
		var result = Enum.IsDefined(typeof(VersionCompatibilityStatus), value);

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region Typical Usage Scenarios

	[Fact]
	public void CanBeUsedInSwitchExpression()
	{
		// Arrange
		var statuses = Enum.GetValues<VersionCompatibilityStatus>();

		// Act & Assert
		foreach (var status in statuses)
		{
			var description = status switch
			{
				VersionCompatibilityStatus.Compatible => "Version is compatible",
				VersionCompatibilityStatus.Deprecated => "Version is deprecated but supported",
				VersionCompatibilityStatus.Incompatible => "Version is incompatible",
				VersionCompatibilityStatus.Unknown => "Version status is unknown",
				_ => "Unhandled status",
			};

			description.ShouldNotBe("Unhandled status");
		}
	}

	[Theory]
	[InlineData(VersionCompatibilityStatus.Compatible, true)]
	[InlineData(VersionCompatibilityStatus.Deprecated, true)]
	[InlineData(VersionCompatibilityStatus.Incompatible, false)]
	[InlineData(VersionCompatibilityStatus.Unknown, false)]
	public void CanDetermineIfMessageProcessable(VersionCompatibilityStatus status, bool expectedProcessable)
	{
		// Act
		var isProcessable = status == VersionCompatibilityStatus.Compatible ||
		                    status == VersionCompatibilityStatus.Deprecated;

		// Assert
		isProcessable.ShouldBe(expectedProcessable);
	}

	[Theory]
	[InlineData(VersionCompatibilityStatus.Compatible, false)]
	[InlineData(VersionCompatibilityStatus.Deprecated, true)]
	[InlineData(VersionCompatibilityStatus.Incompatible, false)]
	[InlineData(VersionCompatibilityStatus.Unknown, false)]
	public void CanDetermineIfDeprecationWarningNeeded(VersionCompatibilityStatus status, bool warningNeeded)
	{
		// Act
		var needsWarning = status == VersionCompatibilityStatus.Deprecated;

		// Assert
		needsWarning.ShouldBe(warningNeeded);
	}

	[Theory]
	[InlineData(VersionCompatibilityStatus.Compatible, false)]
	[InlineData(VersionCompatibilityStatus.Deprecated, false)]
	[InlineData(VersionCompatibilityStatus.Incompatible, true)]
	[InlineData(VersionCompatibilityStatus.Unknown, false)]
	public void CanDetermineIfRejectionRequired(VersionCompatibilityStatus status, bool rejectionRequired)
	{
		// Act
		var shouldReject = status == VersionCompatibilityStatus.Incompatible;

		// Assert
		shouldReject.ShouldBe(rejectionRequired);
	}

	[Fact]
	public void CanFilterByStatus()
	{
		// Arrange
		var messages = new List<(string Id, VersionCompatibilityStatus Status)>
		{
			("msg1", VersionCompatibilityStatus.Compatible),
			("msg2", VersionCompatibilityStatus.Deprecated),
			("msg3", VersionCompatibilityStatus.Incompatible),
			("msg4", VersionCompatibilityStatus.Unknown),
			("msg5", VersionCompatibilityStatus.Compatible),
		};

		// Act
		var compatibleMessages = messages.Where(m =>
			m.Status == VersionCompatibilityStatus.Compatible ||
			m.Status == VersionCompatibilityStatus.Deprecated).ToList();

		// Assert
		compatibleMessages.Count.ShouldBe(3);
		compatibleMessages.ShouldContain(m => m.Id == "msg1");
		compatibleMessages.ShouldContain(m => m.Id == "msg2");
		compatibleMessages.ShouldContain(m => m.Id == "msg5");
	}

	[Fact]
	public void CanGroupByStatus()
	{
		// Arrange
		var statuses = new[]
		{
			VersionCompatibilityStatus.Compatible,
			VersionCompatibilityStatus.Compatible,
			VersionCompatibilityStatus.Deprecated,
			VersionCompatibilityStatus.Incompatible,
			VersionCompatibilityStatus.Unknown,
		};

		// Act
		var grouped = statuses.GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count());

		// Assert
		grouped[VersionCompatibilityStatus.Compatible].ShouldBe(2);
		grouped[VersionCompatibilityStatus.Deprecated].ShouldBe(1);
		grouped[VersionCompatibilityStatus.Incompatible].ShouldBe(1);
		grouped[VersionCompatibilityStatus.Unknown].ShouldBe(1);
	}

	#endregion
}
