// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.AuditLogging.Encryption;
using Excalibur.Compliance;
using Excalibur.Dispatch;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.AuditLogging.Tests;

/// <summary>
/// Binds the delegation of the two decorating audit stores: every read must be answered by the wrapped
/// store, never resolved by the decorator itself.
/// </summary>
/// <remarks>
/// <para>
/// WHY THIS IS THE RIGHT ASSERTION, and it is not the obvious one. A census found these two decorators
/// have no conformance coverage, and the obvious repair -- "add tenant-scoping arms for them" -- would
/// assert the wrong property. Both stores hold no rows today: every read forwards, so tenant scoping is
/// correct by construction and an arm asserting it would pass without touching the thing that could break.
/// </para>
/// <para>
/// What is NOT established is that anything PREVENTS a decorator from acquiring storage of its own -- a
/// cache, a local index, a short-circuit for a hot identifier. On the day one does, it answers a read the
/// inner store never saw, and every tenant predicate the inner store applies is bypassed. That is the
/// regression these arms exist to catch, and it is invisible to a scoping arm.
/// </para>
/// <para>
/// So the safety half withholds every row at the inner store and requires the decorator to return nothing.
/// A decorator that resolved anything itself would produce a row from a store that has none. The liveness
/// half is not optional: a decorator that returned null unconditionally would satisfy the safety half
/// perfectly while forwarding nothing at all.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AuditStoreDecoratorDelegationShould
{
	private const string KnownEventId = "evt-delegation-1";

	[Fact]
	public async Task Rbac_returnNothing_whenTheInnerStoreHoldsNothing()
	{
		var inner = new RecordingAuditStore();
		var sut = CreateRbac(inner);

		var result = await sut.GetByIdAsync(KnownEventId, CancellationToken.None).ConfigureAwait(false);

		result.ShouldBeNull(
			"RbacAuditStore resolved an audit event the wrapped store does not hold, so it answered from "
			+ "storage of its own -- every tenant predicate the inner store applies is bypassed on that path");
		inner.GetByIdCalls.ShouldContain(KnownEventId, "the read must reach the wrapped store at all");
	}

	[Fact]
	public async Task Rbac_forwardTheInnerStoresAnswer_whenItHoldsTheEvent()
	{
		var held = CreateEvent(KnownEventId);
		var inner = new RecordingAuditStore { Held = held };
		var sut = CreateRbac(inner);

		var result = await sut.GetByIdAsync(KnownEventId, CancellationToken.None).ConfigureAwait(false);

		result.ShouldNotBeNull(
			"RbacAuditStore returned nothing for an event the wrapped store holds -- refusing every read is "
			+ "not delegation, and it would satisfy the safety arm above while forwarding nothing");
		result.EventId.ShouldBe(KnownEventId);
	}

	[Fact]
	public async Task Encrypting_returnNothing_whenTheInnerStoreHoldsNothing()
	{
		var inner = new RecordingAuditStore();
		var sut = CreateEncrypting(inner);

		var result = await sut.GetByIdAsync(KnownEventId, CancellationToken.None).ConfigureAwait(false);

		result.ShouldBeNull(
			"EncryptingAuditEventStore resolved an audit event the wrapped store does not hold, so it "
			+ "answered from storage of its own -- the inner store's tenant predicate is bypassed");
		inner.GetByIdCalls.ShouldContain(KnownEventId, "the read must reach the wrapped store at all");
	}

	[Fact]
	public async Task Encrypting_forwardTheInnerStoresAnswer_whenItHoldsTheEvent()
	{
		var held = CreateEvent(KnownEventId);
		var inner = new RecordingAuditStore { Held = held };
		var sut = CreateEncrypting(inner);

		var result = await sut.GetByIdAsync(KnownEventId, CancellationToken.None).ConfigureAwait(false);

		result.ShouldNotBeNull(
			"EncryptingAuditEventStore returned nothing for an event the wrapped store holds -- refusing "
			+ "every read is not delegation");
		result.EventId.ShouldBe(KnownEventId);
	}

	/// <remarks>
	/// The caller is granted <see cref="AuditLogRole.ComplianceOfficer"/> deliberately, and both halves of
	/// that choice matter. An unfaked role provider returns <see cref="AuditLogRole.None"/> and the
	/// decorator throws before it ever reaches the wrapped store. A <see cref="AuditLogRole.SecurityAnalyst"/>
	/// reaches the store but is then filtered back to null for a non-security event -- correct behaviour,
	/// and indistinguishable at the assertion from the delegation failure these arms exist to catch.
	/// ComplianceOfficer sees every event type, so a null result can only mean the decorator did not
	/// forward. The authorization and filtering behaviours are real and deserve their own tests; here they
	/// are held constant so the arms measure one thing.
	/// </remarks>
	private static RbacAuditStore CreateRbac(IAuditStore inner)
	{
		var roles = A.Fake<IAuditRoleProvider>();
		_ = A.CallTo(() => roles.GetCurrentRoleAsync(A<CancellationToken>._))
			.Returns(AuditLogRole.ComplianceOfficer);

		return new RbacAuditStore(inner, roles, A.Fake<IAuditLogger>(), NullLogger<RbacAuditStore>.Instance);
	}

	/// <remarks>
	/// Field encryption is switched OFF, and that is a scoping decision rather than a convenience. With the
	/// defaults the decorator tries to base64-decode every configured field on the way out, so a plaintext
	/// fixture fails inside the crypto path before the forwarding property is ever observed. These arms bind
	/// DELEGATION; round-tripping ciphertext is a different contract and a different test.
	/// </remarks>
	private static EncryptingAuditEventStore CreateEncrypting(IAuditStore inner) =>
		new(
			inner,
			A.Fake<IEncryptionProvider>(),
			Options.Create(new AuditEncryptionOptions
			{
				EncryptActorId = false,
				EncryptIpAddress = false,
				EncryptReason = false,
				EncryptUserAgent = false,
			}));

	/// <summary>
	/// NON-VACUITY: the safety predicate above must FAIL against a decorator that answers a read itself.
	/// </summary>
	/// <remarks>
	/// Four green arms prove nothing about whether they would notice the defect they are written for. A
	/// conformance arm was reported as binding earlier on exactly that basis and was not, so the predicate
	/// is exercised here against a decorator built to violate it: it wraps an inner store, ignores it, and
	/// answers from storage of its own -- the regression shape the arms guard. If this test ever goes green,
	/// the safety predicate has stopped discriminating and the four arms above are decorative.
	/// </remarks>
	[Fact]
	public async Task Red_whenADecoratorResolvesTheEventItself()
	{
		var inner = new RecordingAuditStore();
		var cheating = new SelfResolvingDecorator(inner, CreateEvent(KnownEventId));

		var resolved = await cheating.GetByIdAsync(KnownEventId, CancellationToken.None).ConfigureAwait(false);

		// This is the assertion the four arms above make. Against a self-resolving decorator it must not hold.
		_ = Should.Throw<ShouldAssertException>(
			() => resolved.ShouldBeNull(),
			"the safety predicate must reject a decorator that answers from its own storage; if it does not, "
			+ "the delegation arms cannot detect the regression they exist for");

		inner.GetByIdCalls.ShouldBeEmpty(
			"the fixture is only a faithful violation if the cheating decorator genuinely bypassed the inner "
			+ "store rather than forwarding and then caching");
	}

	/// <summary>
	/// A deliberately non-delegating decorator: it holds a row and never consults the store it wraps.
	/// </summary>
	private sealed class SelfResolvingDecorator(IAuditStore inner, AuditEvent own) : IAuditStore
	{
		public Task<AuditEvent?> GetByIdAsync(string eventId, CancellationToken cancellationToken) =>
			Task.FromResult<AuditEvent?>(
				string.Equals(own.EventId, eventId, StringComparison.Ordinal) ? own : null);

		public Task<AuditEventId> StoreAsync(AuditEvent auditEvent, CancellationToken cancellationToken) =>
			inner.StoreAsync(auditEvent, cancellationToken);

		public Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditQuery query, CancellationToken cancellationToken) =>
			inner.QueryAsync(query, cancellationToken);

		public Task<long> CountAsync(AuditQuery query, CancellationToken cancellationToken) =>
			inner.CountAsync(query, cancellationToken);

		public Task<AuditIntegrityResult> VerifyChainIntegrityAsync(
			DateTimeOffset startDate,
			DateTimeOffset endDate,
			CancellationToken cancellationToken) =>
			inner.VerifyChainIntegrityAsync(startDate, endDate, cancellationToken);

		public Task<AuditEvent?> GetLastEventAsync(string? tenantId, CancellationToken cancellationToken) =>
			inner.GetLastEventAsync(tenantId, cancellationToken);

		public Task<int> PurgeExpiredAsync(DateTimeOffset cutoff, CancellationToken cancellationToken) =>
			Task.FromResult(0);

		public Task<int> PurgeTenantAsync(
			DateTimeOffset cutoff,
			KeyedTenantPartition tenant,
			CancellationToken cancellationToken) =>
			Task.FromResult(0);

		public object? GetService(Type serviceType) => inner.GetService(serviceType);
	}

	private static AuditEvent CreateEvent(string eventId) =>
		new()
		{
			EventId = eventId,
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-1",
		};

	/// <summary>
	/// An inner store that holds at most one event and records the reads it was asked for. Withholding the
	/// row is what makes the safety arm discriminating: any row the decorator returns cannot have come from
	/// here.
	/// </summary>
	private sealed class RecordingAuditStore : IAuditStore
	{
		public List<string> GetByIdCalls { get; } = [];

		public AuditEvent? Held { get; init; }

		public Task<AuditEvent?> GetByIdAsync(string eventId, CancellationToken cancellationToken)
		{
			GetByIdCalls.Add(eventId);

			return Task.FromResult(
				Held is not null && string.Equals(Held.EventId, eventId, StringComparison.Ordinal)
					? Held
					: null);
		}

		public Task<AuditEventId> StoreAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(auditEvent);

			return Task.FromResult(new AuditEventId
			{
				EventId = auditEvent.EventId,
				EventHash = "recording-hash",
				SequenceNumber = 1,
				RecordedAt = DateTimeOffset.UtcNow,
			});
		}

		public Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditQuery query, CancellationToken cancellationToken) =>
			Task.FromResult<IReadOnlyList<AuditEvent>>([]);

		public Task<long> CountAsync(AuditQuery query, CancellationToken cancellationToken) =>
			Task.FromResult(0L);

		public Task<AuditIntegrityResult> VerifyChainIntegrityAsync(
			DateTimeOffset startDate,
			DateTimeOffset endDate,
			CancellationToken cancellationToken) =>
			Task.FromResult(AuditIntegrityResult.Valid(0, startDate, endDate));

		public Task<AuditEvent?> GetLastEventAsync(string? tenantId, CancellationToken cancellationToken) =>
			Task.FromResult<AuditEvent?>(null);

		public Task<int> PurgeExpiredAsync(DateTimeOffset cutoff, CancellationToken cancellationToken) =>
			Task.FromResult(0);

		public Task<int> PurgeTenantAsync(
			DateTimeOffset cutoff,
			KeyedTenantPartition tenant,
			CancellationToken cancellationToken) =>
			Task.FromResult(0);

		public object? GetService(Type serviceType) => null;
	}
}
