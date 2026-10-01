// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Options;

using Excalibur.Dispatch;

namespace Excalibur.Compliance.Erasure;

/// <summary>
/// Validates the aggregate retentions declared on <see cref="AggregateRetentionOptions.Aggregates"/> at
/// startup. Reflection-free (AOT-safe).
/// </summary>
/// <remarks>
/// <para>
/// A retention withholds destruction from a data subject who asked for it, so the declaration has to carry
/// enough for an auditor to evaluate whether withholding was lawful. Everything checked here is a property
/// of the declaration itself — nothing here can establish that the obligation is real, and nothing pretends
/// to.
/// </para>
/// <para>
/// <b>Startup, not erasure time.</b> The first erasure against a bad declaration is the wrong place to
/// discover it: by then the aggregate's personal fields have been written under whichever key the
/// declaration selected, and the choice is no longer free.
/// </para>
/// </remarks>
internal sealed class ErasureRetentionDeclarationValidator : IValidateOptions<AggregateRetentionOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, AggregateRetentionOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();
		var seenScopes = new HashSet<(string Tenant, string Type)>();
		var seenDiscriminators = new Dictionary<string, string>(StringComparer.Ordinal);

		for (var index = 0; index < options.Aggregates.Count; index++)
		{
			var retention = options.Aggregates[index];
			var where = $"{nameof(AggregateRetentionOptions.Aggregates)}[{index}]";

			if (retention is null)
			{
				failures.Add($"{where} is null.");
				continue;
			}

			if (string.IsNullOrWhiteSpace(retention.AggregateType))
			{
				failures.Add(
					$"{where}.{nameof(ErasureRetention.AggregateType)} must name the aggregate type to retain, "
					+ "exactly as the event store records it.");
			}

			// The TENANT is checked for BLANKNESS, and it is the member `required` cannot protect: `required`
			// binds the C# compiler, while a reflection binder or a hand-built options instance reaches an
			// empty string. A blank tenant is not a declaration that the deployment is untenanted -- it is a
			// declaration that says nothing, and a reader cannot tell the two apart. Name
			// TenantScope.UntenantedSentinel to mean untenanted, which is also what such a deployment's own
			// erasure requests carry.
			if (string.IsNullOrWhiteSpace(retention.TenantId))
			{
				failures.Add(
					$"{where}.{nameof(ErasureRetention.TenantId)} must name the tenant whose obligation this "
					+ $"is, or '{TenantScope.UntenantedSentinel}' when the deployment is not multi-tenant. A "
					+ "blank value states neither, and an erasure cannot decide whose retention applies from "
					+ "it.");
			}

			if (!string.IsNullOrWhiteSpace(retention.AggregateType))
			{
				// Keyed on the NORMALISED tenant, which is what the registry groups on. Keying on the
				// tenant as written let two declarations whose tenants normalise together pass this rule
				// and then collapse in the registry, where the first declared wins and the second is
				// discarded silently -- so a signed erasure record could carry the wrong lawful basis and
				// a shorter retention period, decided by declaration order.
				if (!seenScopes.Add((
					ErasureRetentionRegistry.NormaliseTenant(retention.TenantId), retention.AggregateType)))
				{
					failures.Add(
						$"{where} declares '{retention.AggregateType}' a second time for the same tenant. One "
						+ "aggregate type has one retention; two declarations leave it undecided which "
						+ "justification and period the erasure record would carry.");
				}

				// The retained handle carries a truncated digest of the aggregate type, so two declared
				// types that share a digest would share a key -- and destroying one subject's key for the
				// first would take the second with it. The set is known here, so the collision is decidable
				// here rather than assumed away.
				var discriminator = RetainedKeyHandle.DiscriminatorFor(retention.AggregateType);
				if (seenDiscriminators.TryGetValue(discriminator, out var other)
					&& !string.Equals(other, retention.AggregateType, StringComparison.Ordinal))
				{
					failures.Add(
						$"{where} declares '{retention.AggregateType}', which resolves to the same "
						+ $"retained-key handle as '{other}'. The two would share one key, so erasing a "
						+ "subject from either would make the other unreadable. Rename one of them.");
				}
				else
				{
					seenDiscriminators[discriminator] = retention.AggregateType;
				}
			}

			// The BASIS is checked, and it is the member `required` cannot protect. `required` binds the
			// C# compiler; a reflection binder, a deserializer or an out-of-range cast all reach a
			// LegalHoldBasis this type never assigned -- and its default is Article 17(3)(a), freedom of
			// expression. That would read, on a SIGNED certificate, as the ground under which a tax record
			// was kept. A wrong basis is worse than a missing one because it is defensible-looking.
			if (!Enum.IsDefined(retention.Basis))
			{
				failures.Add(
					$"{where}.{nameof(ErasureRetention.Basis)} is not a defined Article 17(3) ground. Name "
					+ "the ground the retention actually relies on; an unset value reads as freedom of "
					+ "expression on the erasure record.");
			}

			if (string.IsNullOrWhiteSpace(retention.Justification))
			{
				failures.Add(
					$"{where}.{nameof(ErasureRetention.Justification)} must state the obligation in terms an "
					+ "auditor can evaluate — the statute, the retention schedule or the regulator's "
					+ "requirement. Write it about the record, not about one person: every data subject named "
					+ "in a retained aggregate is shown this text.");
			}
			else if (RestatesTheBasis(retention))
			{
				// The basis is an Article 17(3) ground and there are seven of them, so repeating its name
				// says only that a ground was picked from a list. The justification is the part that says
				// WHICH obligation, and a declaration that cannot say that has not established one.
				failures.Add(
					$"{where}.{nameof(ErasureRetention.Justification)} only restates "
					+ $"{nameof(ErasureRetention.Basis)} ('{retention.Basis}'). Name the obligation itself — "
					+ "the statute, the schedule or the regulator's requirement — not the Article 17(3) "
					+ "ground it falls under.");
			}

			if (retention.RetentionPeriod <= TimeSpan.Zero)
			{
				failures.Add(
					$"{where}.{nameof(ErasureRetention.RetentionPeriod)} must be greater than zero. A "
					+ "statutory retention ends, and a period of zero or less declares a retention that "
					+ "either never applies or never lapses.");
			}
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}

	/// <summary>
	/// Whether the justification carries nothing beyond the name of the basis it sits next to.
	/// </summary>
	/// <param name="retention">The declaration under validation.</param>
	/// <returns><see langword="true"/> when the justification is a restatement of the basis.</returns>
	/// <remarks>
	/// Deliberately narrow: it catches the justification that IS the basis name and nothing else. A
	/// broader rule would have to judge prose, which this cannot do and should not claim to — the point of
	/// the check is to refuse the empty gesture, not to grade the writing.
	/// </remarks>
	private static bool RestatesTheBasis(ErasureRetention retention)
	{
		var justification = retention.Justification.Trim().TrimEnd('.');
		var basis = retention.Basis.ToString();

		if (string.Equals(justification, basis, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		// "Legal obligation", "legal-obligation", "LEGAL_OBLIGATION" are the same empty gesture as
		// "LegalObligation"; compare with the separators a consumer might have inserted removed.
		var squashed = string.Concat(justification.Where(static c => char.IsLetterOrDigit(c)));
		return string.Equals(squashed, basis, StringComparison.OrdinalIgnoreCase);
	}
}
