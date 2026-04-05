// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Context;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Platform")]
public sealed class ContextFlowDiagnosticEventShould
{
	[Fact]
	public void SetAndGetAllProperties()
	{
		// Arrange & Act
		var now = DateTimeOffset.UtcNow;
		var sut = new ContextFlowDiagnosticEvent
		{
			MessageId = "msg-001",
			CorrelationId = "corr-001",
			Stage = "Validation",
			Timestamp = now,
			ElapsedMilliseconds = 42,
			FieldCountBefore = 5,
			FieldCountAfter = 7,
			SizeBytesBefore = 200,
			SizeBytesAfter = 300,
			IntegrityValid = true,
		};

		// Assert
		sut.MessageId.ShouldBe("msg-001");
		sut.CorrelationId.ShouldBe("corr-001");
		sut.Stage.ShouldBe("Validation");
		sut.Timestamp.ShouldBe(now);
		sut.ElapsedMilliseconds.ShouldBe(42);
		sut.FieldCountBefore.ShouldBe(5);
		sut.FieldCountAfter.ShouldBe(7);
		sut.SizeBytesBefore.ShouldBe(200);
		sut.SizeBytesAfter.ShouldBe(300);
		sut.IntegrityValid.ShouldBeTrue();
	}

	[Fact]
	public void HaveNullableOptionalProperties()
	{
		// Arrange & Act - only required properties set
		var sut = new ContextFlowDiagnosticEvent { Stage = "Validation" };

		// Assert
		sut.MessageId.ShouldBeNull();
		sut.CorrelationId.ShouldBeNull();
		sut.ElapsedMilliseconds.ShouldBe(0);
		sut.IntegrityValid.ShouldBeFalse();
	}
}
