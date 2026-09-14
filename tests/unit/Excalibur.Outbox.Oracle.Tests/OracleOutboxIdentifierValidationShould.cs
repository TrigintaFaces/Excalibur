// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Outbox.Outbox;
using Excalibur.Outbox.Partitioning;

using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Outbox.Oracle.Tests;

/// <summary>
/// Locks every config-derived SQL identifier on the Oracle outbox store options to the allowlist.
/// </summary>
/// <remarks>
/// <para>
/// The table names on this options type are interpolated into statement text by the <c>Qualified*TableName</c>
/// projections, so each is a SQL-injection surface that no parameter can cover — an identifier cannot be
/// parameterized. Oracle is the sharpest of the three relational providers here: its projections emit the
/// identifier <b>unquoted</b>, so unlike the bracket-quoted SQL Server and double-quoted Postgres forms there
/// is no quoting to breach in the first place, and the allowlist is the whole of the guard.
/// </para>
/// <para>
/// <b>Why the exhaustiveness arm exists, and why it is the load-bearing one.</b> The outbox and dead-letter
/// names were validated while the fence control table — added later, and interpolated by exactly the same
/// mechanism — was not. Nothing detected that, because "every interpolated identifier is validated" was a
/// convention re-applied by hand at each property rather than a property of the type. The arm below enumerates
/// the identifiers off the options type itself, so a table name added tomorrow is covered on the day it is
/// added rather than on the day someone remembers. It asserts a non-zero enumeration first, so it cannot pass
/// by finding nothing to check.
/// </para>
/// <para>
/// LIVENESS: <see cref="AcceptOptionsWhenEveryIdentifierIsValid"/> proves the validator does not simply reject
/// everything — without it, a validator that failed unconditionally would satisfy every arm above.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class OracleOutboxIdentifierValidationShould
{
	// Invalid under the identifier allowlist, and — because Oracle interpolates the identifier unquoted —
	// directly statement-terminating in the emitted text.
	private const string InjectionPayload = "X; DROP TABLE USERS--";

	private static OracleOutboxStoreOptionsValidator CreateValidator() =>
		new(
			Options.Create(new OutboxProcessingOptions { PollingInterval = TimeSpan.FromSeconds(5) }),
			Options.Create(new OutboxPartitionOptions()));

	private static OracleOutboxStoreOptions ValidOptions() => new()
	{
		OutboxTableName = "VALID_OUTBOX",
		DeadLetterTableName = "VALID_DEAD_LETTERS",
		ReservationTimeout = 300,
	};

	/// <summary>
	/// SAFETY: the fence control table name is on the allowlist path like its siblings. RED before the fix —
	/// this was the one identifier in the fencing seam nobody validated.
	/// </summary>
	[Fact]
	public void FailWhenFenceTableNameIsNotAnAllowlistedIdentifier()
	{
		var options = ValidOptions();
		options.FenceTableName = InjectionPayload;

		var result = CreateValidator().Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("Fence table name");
	}

	/// <summary>
	/// STRUCTURAL: every table-name identifier on the options type is refused, discovered from the type rather
	/// than listed here — so an identifier added later is covered without this test being edited.
	/// </summary>
	[Fact]
	public void RefuseEveryTableNameIdentifierOnTheOptionsType()
	{
		var identifiers = TableNameProperties();

		identifiers.Length.ShouldBeGreaterThan(
			0,
			"the enumeration found no identifiers to check, so a green here would prove nothing about any of them");

		var validator = CreateValidator();

		foreach (var property in identifiers)
		{
			var options = ValidOptions();
			property.SetValue(options, InjectionPayload);

			validator.Validate(null, options).Failed.ShouldBeTrue(
				$"{property.Name} is interpolated into statement text but is not validated against the identifier "
				+ "allowlist. An identifier cannot be parameterized, so the allowlist is the only guard it has.");
		}
	}

	/// <summary>
	/// LIVENESS: a wholly valid configuration still passes — so the arms above are not satisfied by a validator
	/// that refuses every input.
	/// </summary>
	[Fact]
	public void AcceptOptionsWhenEveryIdentifierIsValid()
	{
		var options = ValidOptions();
		options.FenceTableName = "OUTBOX_FENCE_2";

		CreateValidator().Validate(null, options).Succeeded.ShouldBeTrue();
	}

	private static PropertyInfo[] TableNameProperties() =>
		[.. typeof(OracleOutboxStoreOptions)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(static p =>
				p.PropertyType == typeof(string)
				&& p.CanWrite
				&& p.Name.EndsWith("TableName", StringComparison.Ordinal))];
}
