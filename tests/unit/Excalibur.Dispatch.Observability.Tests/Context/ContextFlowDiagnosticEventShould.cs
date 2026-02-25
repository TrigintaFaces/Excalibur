// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Unit tests for <see cref="ContextFlowDiagnosticEvent"/> internal class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Context")]
public sealed class ContextFlowDiagnosticEventShould
{
	#region Required Property Tests

	[Fact]
	public void RequireStage()
	{
		// Arrange & Act
		var evt = new ContextFlowDiagnosticEvent
		{
			Stage = "PreHandler",
		};

		// Assert
		evt.Stage.ShouldBe("PreHandler");
	}

	#endregion

	#region Optional Property Tests

	[Fact]
	public void HaveNullMessageIdByDefault()
	{
		// Arrange & Act
		var evt = new ContextFlowDiagnosticEvent
		{
			Stage = "Test",
		};

		// Assert
		evt.MessageId.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingMessageId()
	{
		// Arrange & Act
		var evt = new ContextFlowDiagnosticEvent
		{
			Stage = "Test",
			MessageId = "msg-123",
		};

		// Assert
		evt.MessageId.ShouldBe("msg-123");
	}

	[Fact]
	public void HaveNullCorrelationIdByDefault()
	{
		// Arrange & Act
		var evt = new ContextFlowDiagnosticEvent
		{
			Stage = "Test",
		};

		// Assert
		evt.CorrelationId.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingCorrelationId()
	{
		// Arrange & Act
		var evt = new ContextFlowDiagnosticEvent
		{
			Stage = "Test",
			CorrelationId = "corr-456",
		};

		// Assert
		evt.CorrelationId.ShouldBe("corr-456");
	}

	#endregion

	#region Default Value Tests

	[Fact]
	public void HaveDefaultTimestamp()
	{
		// Arrange & Act
		var evt = new ContextFlowDiagnosticEvent
		{
			Stage = "Test",
		};

		// Assert
		evt.Timestamp.ShouldBe(default(DateTimeOffset));
	}

	[Fact]
	public void HaveDefaultElapsedMilliseconds()
	{
		// Arrange & Act
		var evt = new ContextFlowDiagnosticEvent
		{
			Stage = "Test",
		};

		// Assert
		evt.ElapsedMilliseconds.ShouldBe(0);
	}

	[Fact]
	public void HaveDefaultFieldCountBefore()
	{
		// Arrange & Act
		var evt = new ContextFlowDiagnosticEvent
		{
			Stage = "Test",
		};

		// Assert
		evt.FieldCountBefore.ShouldBe(0);
	}

	[Fact]
	public void HaveDefaultFieldCountAfter()
	{
		// Arrange & Act
		var evt = new ContextFlowDiagnosticEvent
		{
			Stage = "Test",
		};

		// Assert
		evt.FieldCountAfter.ShouldBe(0);
	}

	[Fact]
	public void HaveDefaultSizeBytesBefore()
	{
		// Arrange & Act
		var evt = new ContextFlowDiagnosticEvent
		{
			Stage = "Test",
		};

		// Assert
		evt.SizeBytesBefore.ShouldBe(0);
	}

	[Fact]
	public void HaveDefaultSizeBytesAfter()
	{
		// Arrange & Act
		var evt = new ContextFlowDiagnosticEvent
		{
			Stage = "Test",
		};

		// Assert
		evt.SizeBytesAfter.ShouldBe(0);
	}

	[Fact]
	public void HaveDefaultIntegrityValid()
	{
		// Arrange & Act
		var evt = new ContextFlowDiagnosticEvent
		{
			Stage = "Test",
		};

		// Assert
		evt.IntegrityValid.ShouldBeFalse();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void AllowSettingTimestamp()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var evt = new ContextFlowDiagnosticEvent
		{
			Stage = "Test",
			Timestamp = timestamp,
		};

		// Assert
		evt.Timestamp.ShouldBe(timestamp);
	}

	[Fact]
	public void AllowSettingElapsedMilliseconds()
	{
		// Arrange & Act
		var evt = new ContextFlowDiagnosticEvent
		{
			Stage = "Test",
			ElapsedMilliseconds = 150,
		};

		// Assert
		evt.ElapsedMilliseconds.ShouldBe(150);
	}

	[Fact]
	public void AllowSettingFieldCountBefore()
	{
		// Arrange & Act
		var evt = new ContextFlowDiagnosticEvent
		{
			Stage = "Test",
			FieldCountBefore = 5,
		};

		// Assert
		evt.FieldCountBefore.ShouldBe(5);
	}

	[Fact]
	public void AllowSettingFieldCountAfter()
	{
		// Arrange & Act
		var evt = new ContextFlowDiagnosticEvent
		{
			Stage = "Test",
			FieldCountAfter = 8,
		};

		// Assert
		evt.FieldCountAfter.ShouldBe(8);
	}

	[Fact]
	public void AllowSettingSizeBytesBefore()
	{
		// Arrange & Act
		var evt = new ContextFlowDiagnosticEvent
		{
			Stage = "Test",
			SizeBytesBefore = 1024,
		};

		// Assert
		evt.SizeBytesBefore.ShouldBe(1024);
	}

	[Fact]
	public void AllowSettingSizeBytesAfter()
	{
		// Arrange & Act
		var evt = new ContextFlowDiagnosticEvent
		{
			Stage = "Test",
			SizeBytesAfter = 2048,
		};

		// Assert
		evt.SizeBytesAfter.ShouldBe(2048);
	}

	[Fact]
	public void AllowSettingIntegrityValid()
	{
		// Arrange & Act
		var evt = new ContextFlowDiagnosticEvent
		{
			Stage = "Test",
			IntegrityValid = true,
		};

		// Assert
		evt.IntegrityValid.ShouldBeTrue();
	}

	#endregion

	#region Complete Object Tests

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		var evt = new ContextFlowDiagnosticEvent
		{
			MessageId = "msg-abc",
			CorrelationId = "corr-def",
			Stage = "Handler",
			Timestamp = timestamp,
			ElapsedMilliseconds = 100,
			FieldCountBefore = 3,
			FieldCountAfter = 5,
			SizeBytesBefore = 512,
			SizeBytesAfter = 1024,
			IntegrityValid = true,
		};

		// Assert
		evt.MessageId.ShouldBe("msg-abc");
		evt.CorrelationId.ShouldBe("corr-def");
		evt.Stage.ShouldBe("Handler");
		evt.Timestamp.ShouldBe(timestamp);
		evt.ElapsedMilliseconds.ShouldBe(100);
		evt.FieldCountBefore.ShouldBe(3);
		evt.FieldCountAfter.ShouldBe(5);
		evt.SizeBytesBefore.ShouldBe(512);
		evt.SizeBytesAfter.ShouldBe(1024);
		evt.IntegrityValid.ShouldBeTrue();
	}

	[Fact]
	public void SupportScenarioWhereFieldsWereAdded()
	{
		// Arrange & Act
		var evt = new ContextFlowDiagnosticEvent
		{
			Stage = "Handler",
			FieldCountBefore = 3,
			FieldCountAfter = 6,
			SizeBytesBefore = 512,
			SizeBytesAfter = 1024,
		};

		// Assert - Fields were added
		evt.FieldCountAfter.ShouldBeGreaterThan(evt.FieldCountBefore);
		evt.SizeBytesAfter.ShouldBeGreaterThan(evt.SizeBytesBefore);
	}

	[Fact]
	public void SupportScenarioWhereFieldsWereRemoved()
	{
		// Arrange & Act
		var evt = new ContextFlowDiagnosticEvent
		{
			Stage = "Cleanup",
			FieldCountBefore = 10,
			FieldCountAfter = 5,
			SizeBytesBefore = 2048,
			SizeBytesAfter = 1024,
		};

		// Assert - Fields were removed
		evt.FieldCountAfter.ShouldBeLessThan(evt.FieldCountBefore);
		evt.SizeBytesAfter.ShouldBeLessThan(evt.SizeBytesBefore);
	}

	[Fact]
	public void BeInternal()
	{
		// Assert - ContextFlowDiagnosticEvent should be internal sealed
		typeof(ContextFlowDiagnosticEvent).IsNotPublic.ShouldBeTrue();
		typeof(ContextFlowDiagnosticEvent).IsSealed.ShouldBeTrue();
	}

	#endregion
}
