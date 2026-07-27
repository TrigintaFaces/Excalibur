// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox;

using Shouldly;

using Xunit;

namespace Excalibur.Inbox.Oracle.Tests;

/// <summary>
/// Pure (no-infra) lock on the shared deployment-mode ↔ physical-schema handshake truth table
/// (<see cref="InboxSchemaContract.Verify"/>) — the single definition every relational inbox store delegates
/// to (guide §RULING 2A; <c>enforce-invariants-structurally</c> — one seam, not three drifting copies). Six
/// rows: exactly two START (matched mode/schema), the other four FAIL FAST. This is the fast, provider-agnostic
/// companion to the per-provider real-infra deployment-mode locks.
/// </summary>
/// <remarks>
/// Non-vacuous: the START rows assert the emitted <c>hasTenantColumn</c> signal (LIVENESS — a matched store
/// resolves the correct emission mode), the FAIL rows assert the throw (SAFETY). RED against a helper that
/// dropped the tenant checks — e.g. the nullable-tenant rows would stop throwing.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Inbox")]
public sealed class InboxSchemaContractShould
{
    private const string MessageId = "MessageId";
    private const string HandlerType = "HandlerType";
    private const string TenantId = "TenantId";

    private static readonly string[] PairKey = [MessageId, HandlerType];
    private static readonly string[] TripleKey = [MessageId, HandlerType, TenantId];

    // tenantIdIsNullable: null = column absent, true = nullable, false = NOT NULL.
    private static bool Verify(bool multiTenant, string[] primaryKey, bool? tenantIdIsNullable) =>
        InboxSchemaContract.Verify(
            "inbox_messages", multiTenant, primaryKey, tenantIdIsNullable, MessageId, HandlerType, TenantId);

    // ───────────── 2 START (matched mode ↔ schema) — LIVENESS ─────────────

    [Fact]
    public void Start_A_Single_Tenant_Store_On_The_Pair_Schema_Emitting_No_Tenant_Term()
    {
        // non-MT + pair key + no tenant column ⇒ START; emission off.
        Verify(multiTenant: false, PairKey, tenantIdIsNullable: null)
            .ShouldBeFalse("a matched single-tenant store must resolve hasTenantColumn=false (emit no tenant term).");
    }

    [Fact]
    public void Start_A_Multi_Tenant_Store_On_The_Triple_Schema_Emitting_The_Tenant_Term()
    {
        // MT + triple key + TenantId NOT NULL ⇒ START; emission on.
        Verify(multiTenant: true, TripleKey, tenantIdIsNullable: false)
            .ShouldBeTrue("a matched multi-tenant store must resolve hasTenantColumn=true (emit the tenant term).");
    }

    // ───────────── 4 FAIL FAST (mismatched) — SAFETY ─────────────

    [Fact]
    public void Fail_Fast_A_Single_Tenant_Store_On_A_Triple_Schema()
    {
        // non-MT + TenantId-in-key/NOT-NULL ⇒ fail (blocks the bloat sneaking back with no signal).
        _ = Should.Throw<InvalidOperationException>(
            () => Verify(multiTenant: false, TripleKey, tenantIdIsNullable: false));
    }

    [Fact]
    public void Fail_Fast_A_Single_Tenant_Store_With_A_Nullable_Tenant_Column()
    {
        // non-MT must have NO tenant column at all; a present (nullable) one is malformed for single-tenant.
        _ = Should.Throw<InvalidOperationException>(
            () => Verify(multiTenant: false, PairKey, tenantIdIsNullable: true));
    }

    [Fact]
    public void Fail_Fast_A_Multi_Tenant_Store_On_A_Pair_Schema()
    {
        // MT + pair key (no tenant column) ⇒ the leak: all tenants collide on the pair key. Fail fast.
        _ = Should.Throw<InvalidOperationException>(
            () => Verify(multiTenant: true, PairKey, tenantIdIsNullable: null));
    }

    [Fact]
    public void Fail_Fast_A_Multi_Tenant_Store_With_A_Nullable_Tenant_Column()
    {
        // MT requires TenantId NOT NULL; a nullable tenant column breaks key uniqueness / ON CONFLICT. Fail fast.
        _ = Should.Throw<InvalidOperationException>(
            () => Verify(multiTenant: true, TripleKey, tenantIdIsNullable: true));
    }
}
