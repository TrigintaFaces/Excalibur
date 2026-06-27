// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;
using Excalibur.Saga.Postgres;

namespace Excalibur.Data.Tests.Postgres.Requests.Saga;

/// <summary>
/// Author≠impl SECURITY regression lock for bead <c>r5r7fe</c> nit 5 (sprint 855): the Postgres saga
/// request types MUST reject a malicious/malformed config-sourced qualified table name <b>at the public
/// constructor surface</b> — before the <c>"schema"."table"</c> identifier is interpolated into SQL —
/// closing the defense-in-depth gap (parity with SqlServer's saga requests).
/// </summary>
/// <remarks>
/// <para>
/// Authored independently of the fix (<c>issue-remediation-protocol</c>), at the <b>public request-ctor
/// surface</b> (no <c>InternalsVisibleTo</c>) per the integrator's ruling — this also proves the
/// <c>SagaSqlValidator</c> guard is actually <i>wired into</i> <see cref="LoadSagaRequest{T}"/> /
/// <see cref="SaveSagaRequest{T}"/>, not merely present. The qualified name is composed by
/// <c>PostgresSagaOptions.QualifiedTableName =&gt; $"\"{Schema}\".\"{TableName}\""</c>; a malicious
/// <c>Schema</c>/<c>TableName</c> breaks the safe shape and must be rejected.
/// </para>
/// <para>
/// <b>Non-vacuity (RED on the pre-fix surface):</b> on pre-fix HEAD the request constructors do NOT call
/// <c>SagaSqlValidator.ThrowIfInvalidQualifiedName</c>, so a malicious qualified name flows straight into
/// the interpolated SQL and the ctor does <i>not</i> throw — the reject cases fail (RED). Verified the
/// rejection fires at the validator (not a pre-existing <c>PostgresSagaOptions</c> guard): the option
/// setters and the <c>QualifiedTableName</c> getter perform no identifier validation, and
/// <c>PostgresSagaOptions.Validate()</c> / the <c>IValidateOptions</c> startup validator are not invoked
/// by direct construction.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Postgres")]
public sealed class PostgresSagaRequestSqlInjectionGuardShould
{
	private static readonly DispatchJsonSerializer Serializer = new();

	private static PostgresSagaOptions OptionsWith(string schema, string tableName) => new()
	{
		ConnectionString = "Host=localhost;Database=test;",
		Schema = schema,
		TableName = tableName,
		CommandTimeoutSeconds = 30,
	};

	[Theory]
	// SQL-injection payloads in the schema / table identifier
	[InlineData("dispatch\";DROP TABLE sagas;--", "sagas")]
	[InlineData("dispatch", "sagas\"; DROP TABLE sagas;--")]
	[InlineData("dispatch\".\"sagas\"; DROP --", "sagas")]
	// Malformed identifiers (whitespace, empty, bracket-form) that break the safe "schema"."table" shape
	[InlineData("dis patch", "sagas")]
	[InlineData("dispatch", "")]
	[InlineData("", "sagas")]
	[InlineData("[dispatch]", "[sagas]")]
	public void RejectMaliciousQualifiedNameAtBothRequestConstructors(string schema, string tableName)
	{
		var options = OptionsWith(schema, tableName);

		_ = Should.Throw<ArgumentException>(() =>
			new LoadSagaRequest<TestSagaState>(Guid.NewGuid(), options, Serializer, CancellationToken.None));

		_ = Should.Throw<ArgumentException>(() =>
			new SaveSagaRequest<TestSagaState>(CreateTestState(), options, Serializer, CancellationToken.None));
	}

	[Theory]
	[InlineData("dispatch", "sagas")]
	[InlineData("my_schema", "saga_state")]
	public void AcceptWellFormedQualifiedNameAtBothRequestConstructors(string schema, string tableName)
	{
		var options = OptionsWith(schema, tableName);

		Should.NotThrow(() =>
			new LoadSagaRequest<TestSagaState>(Guid.NewGuid(), options, Serializer, CancellationToken.None));

		Should.NotThrow(() =>
			new SaveSagaRequest<TestSagaState>(CreateTestState(), options, Serializer, CancellationToken.None));
	}

	private static TestSagaState CreateTestState() => new()
	{
		SagaId = Guid.NewGuid(),
		Completed = false,
		OrderId = "order-123",
	};

	private sealed class TestSagaState : SagaState
	{
		public string OrderId { get; set; } = string.Empty;
	}
}
