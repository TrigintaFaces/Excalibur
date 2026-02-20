// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Validation;
using Excalibur.Data.SqlServer.Persistence;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.SqlServer;

/// <summary>
/// Regression tests for S541.2 (bd-nekex): SQL injection prevention in savepoint name interpolation.
/// Validates that the savepoint name whitelist regex rejects malicious input and accepts valid names.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SqlServerSavepointValidationShould : UnitTestBase
{
	private readonly SqlServerTransactionScope _sut;

	public SqlServerSavepointValidationShould()
	{
		_sut = new SqlServerTransactionScope(
			IsolationLevel.ReadCommitted,
			TimeSpan.FromSeconds(30),
			NullLogger<SqlServerTransactionScope>.Instance);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_sut.Dispose();
		}

		base.Dispose(disposing);
	}

	#region Valid Savepoint Names

	[Theory]
	[InlineData("sp1")]
	[InlineData("savepoint_one")]
	[InlineData("SP_UPPER_CASE")]
	[InlineData("MixedCase123")]
	[InlineData("a")]
	[InlineData("_leading_underscore")]
	[InlineData("trailing_underscore_")]
	[InlineData("sp_123_456")]
	public async Task AcceptValidSavepointNames(string savepointName)
	{
		// Act — should not throw ArgumentException for valid names.
		// CreateSavepointAsync is a no-op when no connections are enlisted (returns immediately).
		// The key assertion: no ArgumentException = validation passed.
		var exception = await Record.ExceptionAsync(async () =>
			await _sut.CreateSavepointAsync(savepointName, CancellationToken.None));

		// Should not throw ArgumentException (the validation must have passed)
		exception.ShouldNotBeOfType<ArgumentException>();
	}

	#endregion

	#region SQL Injection Strings Rejected

	[Theory]
	[InlineData("'; DROP TABLE Users; --", "SQL injection via semicolon")]
	[InlineData("sp1; DELETE FROM dbo.Events", "SQL injection via DELETE")]
	[InlineData("sp1' OR '1'='1", "SQL injection via OR clause")]
	[InlineData("sp1\"; EXEC xp_cmdshell 'dir'", "SQL injection via EXEC")]
	[InlineData("sp1--comment", "SQL injection via comment")]
	[InlineData("sp1/*comment*/", "SQL injection via block comment")]
	[InlineData("sp name with spaces", "Spaces in name")]
	[InlineData("sp.dot.name", "Dots in name")]
	[InlineData("sp-dash-name", "Dashes in name")]
	[InlineData("sp@at", "At sign in name")]
	[InlineData("sp#hash", "Hash in name")]
	[InlineData("sp$dollar", "Dollar sign in name")]
	[InlineData("sp%percent", "Percent in name")]
	[InlineData("sp\ttab", "Tab character in name")]
	[InlineData("sp\nnewline", "Newline in name")]
	public async Task RejectMaliciousSavepointNames(string savepointName, string description)
	{
		// Act & Assert — should throw ArgumentException for invalid names
		var ex = await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.CreateSavepointAsync(savepointName, CancellationToken.None));

		ex.Message.ShouldContain("invalid characters");
		_ = description; // Used for test output clarity
	}

	#endregion

	#region Empty/Null Rejected

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public async Task RejectNullOrEmptySavepointNames(string? savepointName)
	{
		// Act & Assert — should throw ArgumentException for null/empty/whitespace names
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.CreateSavepointAsync(savepointName, CancellationToken.None));
	}

	#endregion

	#region Validation Applied to All Entry Points

	[Fact]
	public async Task RejectInvalidNameInCreateSavepointAsync()
	{
		// Act & Assert
		var ex = await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.CreateSavepointAsync("sp'; DROP TABLE--", CancellationToken.None));

		ex.Message.ShouldContain("invalid characters");
	}

	[Fact]
	public async Task RejectInvalidNameInRollbackToSavepointAsync()
	{
		// Act & Assert
		var ex = await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.RollbackToSavepointAsync("sp'; DROP TABLE--", CancellationToken.None));

		ex.Message.ShouldContain("invalid characters");
	}

	[Fact]
	public async Task RejectInvalidNameInReleaseSavepointAsync()
	{
		// Act & Assert
		var ex = await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.ReleaseSavepointAsync("sp'; DROP TABLE--", CancellationToken.None));

		ex.Message.ShouldContain("invalid characters");
	}

	#endregion

	#region Centralized Validator Verification

	[Fact]
	public void UseCentralizedSqlIdentifierValidator()
	{
		// Verify the centralized SqlIdentifierValidator exists and works (bd-4y6t3)
		SqlIdentifierValidator.IsValid("valid_name").ShouldBeTrue();
		SqlIdentifierValidator.IsValid("invalid;name").ShouldBeFalse();
	}

	[Fact]
	public void CentralizedValidatorThrowsForInvalidIdentifier()
	{
		// Verify ThrowIfInvalid throws ArgumentException for invalid identifiers
		var ex = Should.Throw<ArgumentException>(
			() => SqlIdentifierValidator.ThrowIfInvalid("invalid;name", "testParam"));
		ex.Message.ShouldContain("invalid characters");
		ex.ParamName.ShouldBe("testParam");
	}

	#endregion
}
