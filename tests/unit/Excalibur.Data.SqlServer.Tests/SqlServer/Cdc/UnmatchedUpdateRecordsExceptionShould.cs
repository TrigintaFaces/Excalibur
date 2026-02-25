// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Cdc;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class UnmatchedUpdateRecordsExceptionShould
{
	[Fact]
	public void ThrowWhenLsnIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new UnmatchedUpdateRecordsException(null!, statusCode: null));
	}

	[Fact]
	public void StoreLsnProperty()
	{
		byte[] lsn = [0x01, 0x02, 0x03];

		var exception = new UnmatchedUpdateRecordsException(lsn, statusCode: null);

		exception.Lsn.ShouldBeSameAs(lsn);
	}

	[Fact]
	public void FormatDefaultMessage()
	{
		byte[] lsn = [0xAB, 0xCD];

		var exception = new UnmatchedUpdateRecordsException(lsn, statusCode: null);

		exception.Message.ShouldContain("Unmatched");
		exception.Message.ShouldContain("UpdateBefore");
		exception.Message.ShouldContain("UpdateAfter");
	}

	[Fact]
	public void UseDefaultStatusCodeOf500()
	{
		byte[] lsn = [0x01];

		var exception = new UnmatchedUpdateRecordsException(lsn, statusCode: null);

		exception.StatusCode.ShouldBe(500);
	}

	[Fact]
	public void AcceptCustomStatusCode()
	{
		byte[] lsn = [0x01];

		var exception = new UnmatchedUpdateRecordsException(lsn, statusCode: 503);

		exception.StatusCode.ShouldBe(503);
	}

	[Fact]
	public void AcceptCustomMessage()
	{
		byte[] lsn = [0x01];

		var exception = new UnmatchedUpdateRecordsException(lsn, message: "Custom message");

		exception.Message.ShouldBe("Custom message");
	}

	[Fact]
	public void AcceptInnerException()
	{
		byte[] lsn = [0x01];
		var inner = new InvalidOperationException("inner");

		var exception = new UnmatchedUpdateRecordsException(lsn, innerException: inner);

		exception.InnerException.ShouldBeSameAs(inner);
	}

	[Fact]
	public void SupportDefaultConstructor()
	{
		var exception = new UnmatchedUpdateRecordsException();

		exception.ShouldNotBeNull();
	}

	[Fact]
	public void SupportMessageConstructor()
	{
		var exception = new UnmatchedUpdateRecordsException("test message");

		exception.Message.ShouldBe("test message");
	}

	[Fact]
	public void SupportMessageAndInnerExceptionConstructor()
	{
		var inner = new InvalidOperationException();
		var exception = new UnmatchedUpdateRecordsException("test", inner);

		exception.InnerException.ShouldBeSameAs(inner);
	}

	[Fact]
	public void SupportStatusCodeMessageInnerExceptionConstructor()
	{
		var inner = new InvalidOperationException();
		var exception = new UnmatchedUpdateRecordsException(404, "not found", inner);

		exception.StatusCode.ShouldBe(404);
		exception.Message.ShouldBe("not found");
		exception.InnerException.ShouldBeSameAs(inner);
	}
}
