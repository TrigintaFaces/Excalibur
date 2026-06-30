// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Outbox;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

/// <summary>
/// bd-1vqmei (S856, P1 SECURITY — tenancy isolation) independent regression lock (author≠impl,
/// TestsDeveloper): the multi-tenant <c>TenantId</c> survives a transport-context remap through the
/// <b>first-class field</b> across <b>all three</b> mappers — Default / Configured / Outbox — so a
/// header-less remap can never silently drop the tenant (cross-tenant misdelivery), and the canonical
/// <c>tenant-id</c> wire header is set <b>authoritatively from the field</b>, overriding any stale copy.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pre-fix (RED):</b> <c>ITransportMessageContext</c> had no <c>TenantId</c> field; the mappers relied
/// on the loose Headers bag, so a remap that didn't carry the <c>tenant-id</c> header dropped the tenant —
/// a cross-tenant-misdelivery risk. Each test below sets the tenant via the first-class field with <b>no
/// <c>tenant-id</c> header on the source</b>, so it is non-vacuous against the pre-fix code (and against a
/// mutant that drops the <c>target.TenantId = source.TenantId</c> copy — proven via cp-backup mutate-restore,
/// never a <c>git checkout</c> of a shared file, <c>commit-surface-before-parallel-edits</c>).
/// </para>
/// <para>
/// The Outbox test additionally seeds a <b>stale</b> <c>tenant-id</c> header and asserts the canonical
/// header is overwritten by the first-class <c>TenantId</c> — field/header divergence is inexpressible
/// (<c>enforce-invariants-structurally</c>; the single-source-of-truth invariant SA pinned in 17610).
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("SubComponent", "TenancyIsolation")]
public sealed class TransportContextTenantIsolationShould
{
	private const string Tenant = "tenant-acme";
	private const string StaleTenant = "tenant-evil-stale";

	[Fact]
	public void PreserveTenantId_ThroughDefaultMapperRemap_WithoutAnyTenantHeader()
	{
		var mapper = new DefaultMessageMapper("tenant-isolation-default");

		// Source carries the tenant ONLY via the first-class field — deliberately NO "tenant-id" header,
		// so survival cannot come from the Headers bag (the pre-fix path that dropped it).
		var source = new TransportMessageContext("msg-1")
		{
			TenantId = Tenant,
			SourceTransport = "in",
		};

		var target = mapper.Map(source, "out");

		target.TenantId.ShouldBe(
			Tenant,
			"bd-1vqmei: DefaultMessageMapper must copy TenantId first-class across a remap — a header-less "
			+ "remap that drops it is a cross-tenant-misdelivery bug.");
	}

	[Fact]
	public void PreserveTenantId_ThroughConfiguredMapperRemap_WithoutAnyTenantHeader()
	{
		var mapper = CreateConfiguredMapper();

		var source = new TransportMessageContext("msg-2")
		{
			TenantId = Tenant,
			SourceTransport = "in",
		};

		var target = mapper.Map(source, "out");

		target.TenantId.ShouldBe(
			Tenant,
			"bd-1vqmei: the configured remap path must ALSO copy TenantId first-class (the real-extent site "
			+ "ConfiguredMessageMapper.Map — scope-the-keystone), or the invariant leaks through this path.");
	}

	[Fact]
	public void SetTenantId_FirstClass_AndOverwriteStaleCanonicalHeader_ThroughOutboxMapper()
	{
		var mapper = new OutboxMessageMapper();

		// The outbound message authoritatively declares the tenant via its first-class field, but ALSO carries
		// a STALE "tenant-id" header from upstream — the canonical header must end up = the field, not the stale value.
		var message = new OutboundMessage
		{
			Id = "msg-3",
			MessageType = "TestEvent",
			Destination = "queue",
			TenantId = Tenant,
		};
		message.Headers[OutboxHeaderNames.TenantId] = StaleTenant;

		var context = mapper.CreateContext(message, "out");

		// First-class field carries the tenant.
		context.TenantId.ShouldBe(
			Tenant,
			"bd-1vqmei: OutboxMessageMapper must set TenantId first-class (never header-only).");

		// Canonical wire header is set authoritatively FROM the field — the stale upstream copy is overridden.
		context.Headers.ShouldContainKey(OutboxHeaderNames.TenantId);
		context.Headers[OutboxHeaderNames.TenantId].ShouldBe(
			Tenant,
			$"bd-1vqmei: the canonical '{OutboxHeaderNames.TenantId}' header must be authoritative from the "
			+ "first-class TenantId, overriding the stale upstream header — field/header divergence must be "
			+ "inexpressible (single source of truth).");
	}

	private static ConfiguredMessageMapper CreateConfiguredMapper()
	{
		var services = new ServiceCollection();
		var registry = new MessageMapperRegistry();
		var builder = new MessageMappingBuilder(services, registry);
		return builder.Build().ShouldBeOfType<ConfiguredMessageMapper>();
	}
}
