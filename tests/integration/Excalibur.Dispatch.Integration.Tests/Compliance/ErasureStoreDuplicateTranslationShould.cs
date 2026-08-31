// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Compliance;
using Excalibur.Compliance.Erasure;

using Microsoft.Data.SqlClient;

using Npgsql;

using Shouldly;

using Tests.Shared.Fixtures;

using PgStore = Excalibur.Compliance.Postgres.Erasure.PostgresErasureStore;
using PgStoreOptions = Excalibur.Compliance.Postgres.Erasure.PostgresErasureStoreOptions;
using SqlStore = Excalibur.Compliance.SqlServer.Erasure.SqlServerErasureStore;
using SqlStoreOptions = Excalibur.Compliance.SqlServer.Erasure.SqlServerErasureStoreOptions;

#pragma warning disable CA1812 // Instantiated by the xUnit test runner.

namespace Excalibur.Dispatch.Integration.Tests.Compliance;

/// <summary>
/// Shared inputs for the duplicate-translation narrowness arms.
/// </summary>
/// <remarks>
/// The conformance kit asserts that a duplicate insert surfaces as
/// <see cref="DuplicateErasureRequestException"/> rather than a raw provider exception. A blanket
/// <c>catch</c> satisfies that assertion perfectly and is
/// the obvious way to write the fix, so the kit alone cannot distinguish a correct translation from one
/// that reports every failure as a duplicate. That is the worse outcome of the two: a caller told the row
/// already exists, when in fact the connection dropped or a value did not fit, will treat a write that
/// never happened as already done and never retry it. The exception type is half the defence and the
/// filter is the other half: a narrow filter that threw the base <see cref="InvalidOperationException"/>
/// would still be read as "already on file" by a caller who also has to handle an unprovisioned schema,
/// a disposed store and an unresolved tenant through that same type. The arms below induce a provider error that is
/// <b>not</b> a uniqueness violation and require the provider's own exception type to come through
/// untouched — they fail against a blanket catch and pass against a filter scoped to duplicate keys.
/// </remarks>
internal static class ErasureStoreDuplicateTranslationFixtures
{
	/// <summary>The column is bounded at 256; this exceeds it and nothing else about the row is invalid.</summary>
	internal static string OverlongRequestedBy => new('x', 300);

	internal static ErasureRequest CreateRequest(string requestedBy) => new()
	{
		RequestId = Guid.NewGuid(),
		DataSubjectId = "subject-" + Guid.NewGuid().ToString("N"),
		IdType = DataSubjectIdType.UserId,
		LegalBasis = ErasureLegalBasis.DataSubjectRequest,
		RequestedBy = requestedBy,
	};
}

/// <summary>
/// Postgres arm: an error other than SQLSTATE 23505 must not be reported as a duplicate.
/// </summary>
[Collection(ContainerCollections.Postgres)]
[Trait("Component", TestComponents.Compliance)]
[Trait("Infrastructure", TestInfrastructure.Postgres)]
[Trait(TraitNames.Category, TestCategories.Integration)]
public sealed class PostgresErasureDuplicateTranslationShould
{
	private readonly PostgresFixture _fixture;
	private readonly string _suffix = Guid.NewGuid().ToString("N")[..8];

	public PostgresErasureDuplicateTranslationShould(PostgresFixture fixture) => _fixture = fixture;

	[Fact]
	public async Task SurfaceANonUniquenessFailureAsTheProviderException()
	{
		var store = CreateStore();

		var thrown = await Should.ThrowAsync<PostgresException>(
			() => store.SaveRequestAsync(
				ErasureStoreDuplicateTranslationFixtures.CreateRequest(
					ErasureStoreDuplicateTranslationFixtures.OverlongRequestedBy),
				DateTimeOffset.UtcNow.AddDays(7),
				TestContext.Current.CancellationToken),
			"a value that does not fit its column is not a duplicate. Translating it to " +
			"DuplicateErasureRequestException would tell the caller the request already exists, so they " +
			"would treat a write that never happened as already done. The filter must match SQLSTATE " +
			"23505 and nothing else.");

		thrown.SqlState.ShouldNotBe(
			PostgresErrorCodes.UniqueViolation,
			"the induced error must genuinely be something other than a uniqueness violation, or this " +
			"arm proves nothing about the filter's narrowness.");
	}

	[Fact]
	public async Task StillTranslateAGenuineDuplicate()
	{
		// The liveness pair: narrowing the filter must not stop it firing on the case it exists for.
		var store = CreateStore();
		var request = ErasureStoreDuplicateTranslationFixtures.CreateRequest("tester");
		var scheduled = DateTimeOffset.UtcNow.AddDays(7);

		await store.SaveRequestAsync(request, scheduled, TestContext.Current.CancellationToken);

		var thrown = await Should.ThrowAsync<DuplicateErasureRequestException>(
			() => store.SaveRequestAsync(request, scheduled, TestContext.Current.CancellationToken),
			"a genuine duplicate must still be translated, and to the specific type: a filter narrowed " +
			"until it never matches would pass the arm above while restoring the raw provider leak the " +
			"contract forbids, and the base type cannot be told from an unprovisioned schema.");

		thrown.RequestId.ShouldBe(
			request.RequestId,
			"the exception must name the request that was re-filed so a caller can act on it.");

		_ = thrown.InnerException.ShouldBeOfType<PostgresException>(
			"the provider exception must be preserved as the inner exception rather than discarded, so " +
			"the underlying cause remains diagnosable.");
	}

	private PgStore CreateStore()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Docker/Postgres must be available - these error codes are produced by the engine and this " +
			"lock must never be skipped.");

		return new PgStore(
			Microsoft.Extensions.Options.Options.Create(new PgStoreOptions
			{
				ConnectionString = _fixture.ConnectionString,
				SchemaName = "compliance",
				RequestsTableName = $"erasure_requests_narrow_{_suffix}",
				CertificatesTableName = $"erasure_certificates_narrow_{_suffix}",
				CommandTimeoutSeconds = 30,
				AutoCreateSchema = true,
			}),
			ConformanceDataSubjectHasher.Instance,
			EnabledTestLogger.Create<PgStore>(),
			UntenantedContext.Instance,
			tenantContextOptions: Microsoft.Extensions.Options.Options.Create(new Excalibur.Dispatch.TenantContextOptions()));
	}
}

/// <summary>
/// SQL Server arm: an error other than 2627/2601 must not be reported as a duplicate.
/// </summary>
[Collection(ContainerCollections.SqlServer)]
[Trait("Component", TestComponents.Compliance)]
[Trait("Infrastructure", TestInfrastructure.SqlServer)]
[Trait(TraitNames.Category, TestCategories.Integration)]
public sealed class SqlServerErasureDuplicateTranslationShould
{
	private static readonly int[] DuplicateKeyNumbers = [2627, 2601];

	private readonly SqlServerFixture _fixture;
	private readonly string _suffix = Guid.NewGuid().ToString("N")[..8];

	public SqlServerErasureDuplicateTranslationShould(SqlServerFixture fixture) => _fixture = fixture;

	[Fact]
	public async Task SurfaceANonUniquenessFailureAsTheProviderException()
	{
		var store = CreateStore();

		var thrown = await Should.ThrowAsync<SqlException>(
			() => store.SaveRequestAsync(
				ErasureStoreDuplicateTranslationFixtures.CreateRequest(
					ErasureStoreDuplicateTranslationFixtures.OverlongRequestedBy),
				DateTimeOffset.UtcNow.AddDays(7),
				TestContext.Current.CancellationToken),
			"a value that does not fit its column is not a duplicate. Translating it to " +
			"DuplicateErasureRequestException would tell the caller the request already exists, so they " +
			"would treat a write that never happened as already done. The filter must match 2627/2601 only.");

		thrown.Number.ShouldNotBeOneOf(
			DuplicateKeyNumbers,
			"the induced error must genuinely be something other than a duplicate-key violation, or " +
			"this arm proves nothing about the filter's narrowness.");
	}

	[Fact]
	public async Task StillTranslateAGenuineDuplicate()
	{
		var store = CreateStore();
		var request = ErasureStoreDuplicateTranslationFixtures.CreateRequest("tester");
		var scheduled = DateTimeOffset.UtcNow.AddDays(7);

		await store.SaveRequestAsync(request, scheduled, TestContext.Current.CancellationToken);

		var thrown = await Should.ThrowAsync<DuplicateErasureRequestException>(
			() => store.SaveRequestAsync(request, scheduled, TestContext.Current.CancellationToken),
			"a genuine duplicate must still be translated, and to the specific type: a filter narrowed " +
			"until it never matches would pass the arm above while restoring the raw provider leak the " +
			"contract forbids, and the base type cannot be told from an unprovisioned schema.");

		thrown.RequestId.ShouldBe(
			request.RequestId,
			"the exception must name the request that was re-filed so a caller can act on it.");

		_ = thrown.InnerException.ShouldBeOfType<SqlException>(
			"the provider exception must be preserved as the inner exception rather than discarded, so " +
			"the underlying cause remains diagnosable.");
	}

	private SqlStore CreateStore()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Docker/SQL Server must be available - these error numbers are produced by the engine and " +
			"this lock must never be skipped.");

		return new SqlStore(
			Microsoft.Extensions.Options.Options.Create(new SqlStoreOptions
			{
				ConnectionString = _fixture.ConnectionString,
				SchemaName = "compliance",
				RequestsTableName = $"ErasureRequestsNarrow{_suffix}",
				CertificatesTableName = $"ErasureCertificatesNarrow{_suffix}",
				CommandTimeoutSeconds = 30,
				AutoCreateSchema = true,
			}),
			ConformanceDataSubjectHasher.Instance,
			EnabledTestLogger.Create<SqlStore>(),
			UntenantedContext.Instance,
			tenantContextOptions: Microsoft.Extensions.Options.Options.Create(new Excalibur.Dispatch.TenantContextOptions()));
	}
}
