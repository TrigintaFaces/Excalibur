// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Exceptions;

namespace Excalibur.Dispatch.Tests.Messaging.Exceptions;

/// <summary>
///     Tests for severity determination in <see cref="DispatchException" />.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ErrorCategorySeverityMappingShould
{
	[Theory]
	[InlineData("CFG001", ErrorSeverity.Critical)]
	[InlineData("SEC001", ErrorSeverity.Critical)]
	[InlineData("SYS001", ErrorSeverity.Error)]
	[InlineData("DAT001", ErrorSeverity.Error)]
	[InlineData("SER001", ErrorSeverity.Error)]
	[InlineData("RES001", ErrorSeverity.Error)]
	[InlineData("MSG001", ErrorSeverity.Warning)]
	[InlineData("VAL001", ErrorSeverity.Warning)]
	[InlineData("TIM001", ErrorSeverity.Warning)]
	[InlineData("NET001", ErrorSeverity.Warning)]
	[InlineData("UNK001", ErrorSeverity.Information)]
	public void MapErrorCodeToCorrectSeverity(string errorCode, ErrorSeverity expectedSeverity)
	{
		var ex = new DispatchException(errorCode, "test");
		ex.Severity.ShouldBe(expectedSeverity);
	}
}
