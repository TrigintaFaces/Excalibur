// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch;

/// <summary>
/// The two facts a caller must present to complete a message it claimed: which leadership TENURE it is
/// acting for, and which CLAIM it is reporting against.
/// </summary>
/// <param name="FencingToken">
/// The monotonic token identifying the caller's current leadership tenure.
/// </param>
/// <param name="ClaimIdentity">
/// The identity the store stamped on the row when this exact claim handed the caller the message, carried
/// on <see cref="OutboundMessage.DispatcherId"/>.
/// </param>
/// <remarks>
/// <para>
/// <b>Two values, one parameter, because neither answers the other's question and both are required.</b> A
/// fencing token identifies a tenure, so it cannot discriminate two claim cycles WITHIN one tenure — a
/// processor may claim a message, fail, reclaim it and fail again without any change of leadership. A claim
/// identity is per-claim, so it says nothing about whether the tenure is still current. A completion needs
/// both answers and is wrong with either alone.
/// </para>
/// <para>
/// <b>They travel together rather than as adjacent parameters, and that is a safety property.</b> Two
/// opaque identifiers side by side in a signature invite transposition, and a transposition would compile
/// and then silently compare the wrong values against the wrong columns. Naming the components makes the
/// mistake unavailable. This is the shape the framework libraries use wherever two values are meaningless
/// apart — an instant and its offset are one value, not two arguments.
/// </para>
/// <para>
/// <b>Neither component is optional, and that is what the type is for.</b> A member accepting this value is
/// on the fenced path by construction, where "no tenure" is not a state that can arise: a caller with no
/// leadership gate never reaches it, because the two cases are separated at the call site before any
/// authority is constructed. A nullable token here would re-admit exactly the shape this contract refuses
/// elsewhere — a guard you satisfy by omitting the thing it guards.
/// </para>
/// <para>
/// <b>A DEFAULTED value of this type is NOT a valid authority, and an implementation MUST reject one.</b>
/// Every struct carries a parameterless default that no constructor can intercept, so
/// <c>default(OutboxWriteAuthority)</c> exists and presents a zero token with no claim identity. That is
/// not merely meaningless — a zero token is ACCEPTED by a high-water comparison on a scope that has no
/// recorded mark yet, because the mark is created from the presented value and then equals it. The claim
/// term would refuse such a write for an unrelated reason, which is one guard doing the other's job by
/// accident rather than a guard that holds. Implementations therefore validate both components before
/// composing a statement: a fencing token is strictly positive, and a claim identity is non-empty.
/// </para>
/// <para>
/// A later component can be added source-compatibly through the constructor or an object initializer,
/// provided it carries a default. That is source compatibility and not binary compatibility, which is
/// stated rather than implied.
/// </para>
/// </remarks>
public readonly record struct OutboxWriteAuthority(long FencingToken, string ClaimIdentity);
