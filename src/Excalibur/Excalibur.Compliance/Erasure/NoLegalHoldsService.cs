// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// The legal-hold service for a deployment that has explicitly declared it operates no legal holds.
/// </summary>
/// <remarks>
/// <para>
/// <b>This exists so that erasure can require a legal-hold service unconditionally.</b> Before it, the
/// dependency was nullable and the hold check was wrapped in a null test, which meant the difference
/// between <i>"this deployment has no holds"</i> and <i>"nobody wired the service"</i> was invisible at the
/// point it mattered — and the second silently skipped an irreversible check. A deployment that genuinely
/// operates no holds is a legitimate configuration; it just has to say so, and this type is what saying so
/// resolves to.
/// </para>
/// <para>
/// <b>It is never registered implicitly, and that is load-bearing rather than stylistic.</b> The startup
/// validator answers "are holds enforced?" by probing whether <see cref="ILegalHoldService"/> is registered
/// at all. If anything registered this type unconditionally — including a <c>TryAdd</c> — that probe would
/// return true for every container ever built, the validator could never refuse, and the guard would go on
/// reporting success while checking nothing. A null object that is registered by default does not merely
/// risk losing a race; it disarms the guard that replaced the original defect. So the only way to obtain
/// this type is to call the registration method named for it.
/// </para>
/// <para>
/// <b>Reads answer honestly; writes refuse.</b> There are no holds, so reporting none is true and the read
/// members simply say so. Creating or releasing one is a different matter: accepting a hold here would
/// record something that this deployment has declared it will never enforce, which is precisely the
/// "holds are written and readable, and never enforced" configuration the validator warns about. Refusing
/// is the honest answer, and it surfaces the contradiction at the call that caused it rather than at an
/// erasure months later.
/// </para>
/// </remarks>
internal sealed class NoLegalHoldsService : ILegalHoldService
{
	/// <summary>
	/// The refusal message shared by both mutating members, so the two cannot drift apart.
	/// </summary>
	private const string DeclaredNoHolds =
		"This deployment declared that it operates no legal holds, so a legal hold cannot be created or "
		+ "released. Register a real legal-hold service instead of declaring no holds if holds are needed.";

	/// <inheritdoc/>
	/// <remarks>
	/// Refuses. Recording a hold that nothing will enforce is worse than declining to record it, because
	/// the caller would reasonably believe the subject was protected.
	/// </remarks>
	public Task<LegalHold> CreateHoldAsync(LegalHoldRequest request, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		return Task.FromException<LegalHold>(new InvalidOperationException(DeclaredNoHolds));
	}

	/// <inheritdoc/>
	/// <remarks>Refuses, for the same reason as creation: no hold can exist here to be released.</remarks>
	public Task ReleaseHoldAsync(
		Guid holdId,
		string reason,
		string releasedBy,
		CancellationToken cancellationToken) =>
		Task.FromException(new InvalidOperationException(DeclaredNoHolds));

	/// <inheritdoc/>
	/// <remarks>
	/// Reports no active holds. This is the true answer for this deployment rather than a stand-in for one,
	/// which is what makes erasure's unconditional call to it correct.
	/// </remarks>
	public Task<LegalHoldCheckResult> CheckHoldsAsync(
		string dataSubjectId,
		DataSubjectIdType idType,
		string? tenantId,
		CancellationToken cancellationToken) =>
		Task.FromResult(LegalHoldCheckResult.NoHolds);

	/// <inheritdoc/>
	/// <remarks>Reports absence, because no hold can have been created here.</remarks>
	public Task<LegalHold?> GetHoldAsync(Guid holdId, CancellationToken cancellationToken) =>
		Task.FromResult<LegalHold?>(null);

	/// <inheritdoc/>
	/// <remarks>Reports an empty set, for the same reason.</remarks>
	public Task<IReadOnlyList<LegalHold>> ListActiveHoldsAsync(
		string? tenantId,
		CancellationToken cancellationToken) =>
		Task.FromResult<IReadOnlyList<LegalHold>>([]);
}
