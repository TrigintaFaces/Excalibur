// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Validation;

namespace Excalibur.Dispatch.Abstractions.Tests.Validation;

/// <summary>
/// Unit tests for <see cref="MessageValidationContext"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageValidationContextShould
{
	[Fact]
	public void Constructor_SetsMessageIdAndMessageType()
	{
		// Act
		var context = new MessageValidationContext("msg-123", typeof(string));

		// Assert
		context.MessageId.ShouldBe("msg-123");
		context.MessageType.ShouldBe(typeof(string));
	}

	[Fact]
	public void Constructor_ThrowsOnNullMessageId()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new MessageValidationContext(null!, typeof(string)));
	}

	[Fact]
	public void Constructor_ThrowsOnNullMessageType()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new MessageValidationContext("msg-1", null!));
	}

	[Fact]
	public void CorrelationId_DefaultsToNull()
	{
		// Arrange
		var context = new MessageValidationContext("msg-1", typeof(string));

		// Assert
		context.CorrelationId.ShouldBeNull();
	}

	[Fact]
	public void CorrelationId_CanBeSet()
	{
		// Arrange
		var context = new MessageValidationContext("msg-1", typeof(string));

		// Act
		context.CorrelationId = "corr-abc";

		// Assert
		context.CorrelationId.ShouldBe("corr-abc");
	}

	[Fact]
	public void TenantId_DefaultsToNull()
	{
		// Arrange
		var context = new MessageValidationContext("msg-1", typeof(string));

		// Assert
		context.TenantId.ShouldBeNull();
	}

	[Fact]
	public void TenantId_CanBeSet()
	{
		// Arrange
		var context = new MessageValidationContext("msg-1", typeof(string));

		// Act
		context.TenantId = "tenant-xyz";

		// Assert
		context.TenantId.ShouldBe("tenant-xyz");
	}

	[Fact]
	public void Properties_InitializesAsEmptyDictionary()
	{
		// Arrange
		var context = new MessageValidationContext("msg-1", typeof(string));

		// Assert
		context.Properties.ShouldNotBeNull();
		context.Properties.ShouldBeEmpty();
	}

	[Fact]
	public void Properties_CanStoreValues()
	{
		// Arrange
		var context = new MessageValidationContext("msg-1", typeof(string));

		// Act
		context.Properties["key1"] = "value1";

		// Assert
		context.Properties["key1"].ShouldBe("value1");
	}

	[Fact]
	public void Timestamp_HasDefaultValue()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;
		var context = new MessageValidationContext("msg-1", typeof(string));
		var after = DateTimeOffset.UtcNow;

		// Assert
		context.Timestamp.ShouldBeGreaterThanOrEqualTo(before);
		context.Timestamp.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void Timestamp_CanBeOverridden()
	{
		// Arrange
		var context = new MessageValidationContext("msg-1", typeof(string));
		var customTime = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);

		// Act
		context.Timestamp = customTime;

		// Assert
		context.Timestamp.ShouldBe(customTime);
	}
}
