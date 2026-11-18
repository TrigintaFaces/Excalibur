// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.Erasure;
using Excalibur.Dispatch;

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// The tenant a per-subject key is owned by comes from the HOST, never from the erasure request.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is a safety arm and not a validation nicety.</b> The key handle protecting a subject's
/// personal fields is derived from an owner term. The write path derives it from the ambient tenant
/// context. If the erasure derives it from a value the caller put on the request instead, the two
/// disagree, and the erasure destroys a handle nothing was ever minted under: the key provider answers
/// "not found", which the erasure treats as an idempotent no-op, no error is recorded, and the request
/// reaches <c>Completed</c> with a signed certificate over personal data that still decrypts. Nothing
/// throws and nothing is logged, so a regulated consumer holds an attestation of an erasure that did not
/// happen.
/// </para>
/// <para>
/// <b>Refused, not quietly corrected.</b> On a single-tenant host there is exactly one legitimate owner —
/// the resolved ambient identity — so a caller-supplied value can only agree with it (and add nothing) or
/// disagree with it (and silently select the wrong key). Substituting the ambient term without a word
/// would close the safety hole while discarding caller intent in silence, which is the same dishonesty one
/// layer down: the caller asked for something the framework did not do and was not told. So the request is
/// rejected by name.
/// </para>
/// <para>
/// <b>One resolution site, three providers.</b> The in-memory, PostgreSQL and SQL Server erasure stores
/// previously each computed this owner inline, with the same expression character for character — a
/// contract implemented three times, and the error replicated three times with it. They now share one
/// resolver, which is what these arms bind. <b>That is deliberately the whole coverage claim: these arms
/// exercise the shared resolver through the in-memory store, and the other two providers are protected
/// because they call the same function — not because an arm here ran against a database.</b> A provider
/// that stopped calling it would not be caught by this file.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "Regression")]
public sealed class AnErasureOwnerComesFromTheHostNotTheRequestShould
{
	// A single-tenant host: RequireTenant defaults to false, and the ambient context resolves the reserved
	// untenanted term. This is the shape the defect was reachable in, with no unusual configuration.
	private static InMemoryErasureStore SingleTenantStore() =>
		new(
			TestDataSubjectHasher.Instance,
			UntenantedContext.Instance,
			Options.Create(new TenantContextOptions()), TimeProvider.System);

	private static ErasureRequest Request(Guid requestId, string? tenantId) =>
		new()
		{
			RequestId = requestId,
			DataSubjectId = "subject-42",
			IdType = DataSubjectIdType.UserId,
			Scope = ErasureScope.User,
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			RequestedBy = "admin",
			RequestedAt = DateTimeOffset.UtcNow,
			TenantId = tenantId,
		};

	[Fact]
	public async Task RefuseARequestNamingATenantTheHostDoesNotHave()
	{
		var store = SingleTenantStore();
		var requestId = Guid.NewGuid();

		// A tenant this host has no notion of. Before the fix this was accepted and became the key owner,
		// so the erasure went looking for a handle the write never created.
		var request = Request(requestId, "a-tenant-this-host-does-not-have");

		_ = await Should.ThrowAsync<ErasureOperationException>(
			() => store.SaveRequestAsync(request, DateTimeOffset.UtcNow, TestContext.Current.CancellationToken));

		// AND NOTHING WAS FILED. A refusal that still persisted the row would leave the background
		// processor -- which reads the scheduled set unscoped -- free to execute it under the wrong owner,
		// which is the whole defect surviving behind an exception.
		var status = await store
			.GetStatusAsync(requestId, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);

		status.ShouldBeNull("a refused request must not be persisted at all");
	}

	[Fact]
	public async Task AcceptARequestThatNamesNoTenantAndOwnItByTheHostsTerm()
	{
		var store = SingleTenantStore();
		var requestId = Guid.NewGuid();

		// The documented single-tenant shape: the property is described as being for multi-tenant
		// scenarios, so a single-tenant caller leaves it unset.
		await store
			.SaveRequestAsync(Request(requestId, tenantId: null), DateTimeOffset.UtcNow, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);

		var status = await store
			.GetStatusAsync(requestId, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);

		// LIVENESS. Without this arm the safety arm above is satisfied by a resolver that refuses
		// everything, which would make every erasure unfileable.
		status.ShouldNotBeNull("a request naming no tenant is the ordinary single-tenant case");
		status.TenantId.ShouldBe(
			UntenantedContext.Instance.TenantId,
			"the owner must be the host's resolved term, which is what the write path encrypted under");
	}

	[Fact]
	public async Task AcceptARequestThatNamesTheHostsOwnTenant()
	{
		var store = SingleTenantStore();
		var requestId = Guid.NewGuid();

		// Naming the host's own term agrees with the write path, so it is redundant rather than wrong.
		// This arm is what makes the refusal a MISMATCH check rather than a ban on supplying the field --
		// a resolver that threw whenever the property was set would pass the safety arm and break callers
		// who fill it in correctly.
		await store
			.SaveRequestAsync(
				Request(requestId, UntenantedContext.Instance.TenantId),
				DateTimeOffset.UtcNow,
				TestContext.Current.CancellationToken)
			.ConfigureAwait(false);

		var status = await store
			.GetStatusAsync(requestId, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);

		status.ShouldNotBeNull("a request naming the host's own tenant agrees with the write path");
		status.TenantId.ShouldBe(UntenantedContext.Instance.TenantId);
	}
}
