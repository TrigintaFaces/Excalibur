// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Boundary.Tests.Architecture;

/// <summary>
/// On the in-memory outbox store, the fencing check and the mutation it authorises must occur inside
/// one lock region.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect this exists to catch.</b> Mark-sent compared the presented token against the
/// high-water and advanced it under <c>lock (_claimLock)</c>, RELEASED that lock, and then applied
/// <c>message.MarkSent()</c> under a different per-message lock. Two disjoint critical sections, so
/// with a high-water of 5: A presents token 5 and passes the check; A leaves the region; B presents
/// token 9 and advances the high-water to 9; A then takes the message lock and writes. A's check was
/// truthful against a value that no longer held by the time it wrote, and a superseded tenure's write
/// lands on a message the live leader already owns. The claim path never had this shape — one region
/// spanned check, advance, select and lease — so the file contained the correct construction and the
/// defective one side by side.
/// </para>
/// <para>
/// <b>Why this is structural rather than behavioural, which is the load-bearing decision.</b> The
/// window is intra-call and in-process: it opens and closes between two statements of one method, with
/// no round trip, no I/O and no scheduling point a test can aim at. A conformance arm written against
/// the requirement was measured GREEN on both the defective revision and its fix — it locks nothing,
/// because nothing it can do from outside the store deterministically places another thread inside a
/// gap that exists only between two instructions. The alternative, reflecting the private lock object
/// out and holding it to widen the window, binds the field NAME rather than the property, and it broke
/// the moment the fix removed that field. The property here is a property of the code's shape, so the
/// shape is what is asserted. The five stores backed by a real service keep behavioural arms: their
/// equivalent window is a round trip, which a test can reach.
/// </para>
/// <para>
/// <b>What counts as the mutation the fence authorises,</b> stated because a guard that flags every
/// read-then-write in the file is noise and gets deleted rather than obeyed. Two writes carry a fence
/// decision: a status transition on an outbound message (<c>Mark…()</c>), which is the completion the
/// token buys, and recording a claim by writing a lease into one of the store's own collections.
/// Everything else in these members is excluded on purpose — reads, removals of auxiliary lease and
/// backoff entries after the authoritative transition has landed (idempotent, and correct outside the
/// region today), and writes into locals. <see cref="Detector_MustNotFlagCleanupAfterTheGuardedMutation"/>
/// binds that exclusion, because it is the false positive that would cost this file its life.
/// </para>
/// <para>
/// <b>Non-vacuity.</b> Four detector arms run the same predicate over synthetic sources — two shapes it
/// must flag, two it must not. The production arm additionally carries floors on how many members touch
/// the fence and how many guarded mutations were recognised inside them, so a rename, a move, or a
/// mutation the predicate stops recognising reports RED rather than reading as a clean store.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class FenceCheckAndGuardedMutationShareOneLockTests
{
	private const string StoreTypeName = "InMemoryOutboxStore";

	private const string FenceField = "_fencingHighWaterMark";

	private static readonly string RepoRoot = TestHelpers.GetRepositoryRoot();

	/// <summary>
	/// The detector must flag a fence check split from the mutation it guards by a lock boundary.
	/// </summary>
	/// <remarks>
	/// This is the shipped defect, written down rather than waited for: the fence under one lock, the
	/// write under another. If the predicate cannot separate these two regions it cannot separate them
	/// in the store either, and every clean report from the production arm would be worthless.
	/// </remarks>
	[Fact]
	public void Detector_MustFlagAFenceCheckSplitFromTheMutationItGuards()
	{
		const string Split = """
			internal sealed class Store
			{
				private readonly object _claimLock = new();
				private readonly object _messageLock = new();
				private long _fencingHighWaterMark;

				public void CompleteAsSent(string id, long fencingToken)
				{
					lock (_claimLock)
					{
						if (fencingToken < _fencingHighWaterMark)
						{
							throw new System.InvalidOperationException("stale token");
						}

						_fencingHighWaterMark = fencingToken;
					}

					lock (_messageLock)
					{
						_messages[id].MarkSent();
					}
				}
			}
			""";

		Scan(Split).Offences.ShouldNotBeEmpty(
			"the deliberately-split subject was not detected, so the production arm below has stopped "
			+ "being a detector and would pass over a reintroduction of the defect it was written for");
	}

	/// <summary>
	/// The detector must flag a terminal write performed as a PLAIN MEMBER ASSIGNMENT outside the fence
	/// region, not only one performed through a <c>Mark*</c> call or an indexer write.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This arm exists because the scanner could not see that shape at all. It recognised an invocation
	/// (<c>message.MarkSent()</c>) and an indexer assignment (<c>_leases[id] = …</c>), and a direct
	/// <c>message.Status = …</c> fell through to "not a mutation" — so a member whose terminal write is a
	/// plain assignment contributed NOTHING, and moving that write out of the lock produced no offence.
	/// </para>
	/// <para>
	/// The fixture below is the exact shape that was invisible: the fence is checked and advanced inside
	/// the region, and the write the fence authorises sits outside it as a field assignment on the
	/// message. A scanner blind to that reports this store clean.
	/// </para>
	/// </remarks>
	[Fact]
	public void Detector_MustFlagATerminalWriteMadeAsAPlainMemberAssignmentOutsideTheRegion()
	{
		const string PlainAssignment = """
			internal sealed class Store
			{
				private readonly object _claimLock = new();
				private long _fencingHighWaterMark;

				public void CompleteAsDeadLettered(string id, long fencingToken, string reason)
				{
					lock (_claimLock)
					{
						if (fencingToken < _fencingHighWaterMark)
						{
							throw new System.InvalidOperationException("stale token");
						}

						_fencingHighWaterMark = fencingToken;
					}

					var message = _messages[id];
					message.Status = OutboxStatus.DeadLettered;
					message.LastError = reason;
				}
			}
			""";

		Scan(PlainAssignment).Offences.ShouldNotBeEmpty(
			"a terminal write performed as a plain member assignment outside the fence region was not "
			+ "detected. That is the shape the scanner was blind to: it recognised Mark*() calls and "
			+ "indexer writes only, so a member whose terminal write is a direct field assignment "
			+ "contributed no mutations and could never be reported as outside the region");
	}

	/// <summary>The detector must flag a fence read and its advance placed in two separate regions.</summary>
	/// <remarks>
	/// The same defect one step earlier: the check and the advance are themselves separable, and a
	/// fresher tenure landing between them supersedes a caller that is then admitted anyway.
	/// </remarks>
	[Fact]
	public void Detector_MustFlagAFenceCheckSplitFromItsOwnAdvance()
	{
		const string SplitFence = """
			internal sealed class Store
			{
				private readonly object _claimLock = new();
				private long _fencingHighWaterMark;

				public void CompleteAsSent(string id, long fencingToken)
				{
					lock (_claimLock)
					{
						if (fencingToken < _fencingHighWaterMark)
						{
							throw new System.InvalidOperationException("stale token");
						}
					}

					lock (_claimLock)
					{
						_fencingHighWaterMark = fencingToken;
						_messages[id].MarkSent();
					}
				}
			}
			""";

		Scan(SplitFence).Offences.ShouldNotBeEmpty(
			"the fence was read in one region and advanced in another and the detector did not notice, "
			+ "so it only recognises the one arrangement it was shown and not the property");
	}

	/// <summary>The detector must flag a fence touched with no mutual exclusion at all.</summary>
	[Fact]
	public void Detector_MustFlagAFenceTouchedOutsideAnyLock()
	{
		const string Unlocked = """
			internal sealed class Store
			{
				private long _fencingHighWaterMark;

				public void CompleteAsSent(string id, long fencingToken)
				{
					if (fencingToken < _fencingHighWaterMark)
					{
						throw new System.InvalidOperationException("stale token");
					}

					_fencingHighWaterMark = fencingToken;
					_messages[id].MarkSent();
				}
			}
			""";

		Scan(Unlocked).Offences.ShouldNotBeEmpty(
			"a fence read and advanced under no lock at all was accepted, so the detector is satisfied "
			+ "by the weakest possible construction");
	}

	/// <summary>
	/// The detector must NOT flag one region spanning the check, the advance and the write, with
	/// auxiliary cleanup outside it.
	/// </summary>
	/// <remarks>
	/// The precision half, and the reason it is a named arm rather than an assumption: the store
	/// deliberately drops lease and backoff entries AFTER leaving the region, because a message already
	/// transitioned is no longer claimable whatever those entries say. A detector that reported those
	/// removals would accuse the corrected code of the defect it was written to prevent, and would be
	/// deleted rather than obeyed — which is how a structural test costs more than it saves.
	/// </remarks>
	[Fact]
	public void Detector_MustNotFlagCleanupAfterTheGuardedMutation()
	{
		const string Correct = """
			internal sealed class Store
			{
				private readonly object _claimLock = new();
				private long _fencingHighWaterMark;

				public void CompleteAsSent(string id, long fencingToken)
				{
					lock (_claimLock)
					{
						if (fencingToken < _fencingHighWaterMark)
						{
							throw new System.InvalidOperationException("stale token");
						}

						_fencingHighWaterMark = fencingToken;
						_messages[id].MarkSent();
					}

					_ = _leases.TryRemove(id, out _);
					_ = _nextAttempt.TryRemove(id, out _);
				}
			}
			""";

		Scan(Correct).Offences.ShouldBeEmpty(
			"the corrected shape was reported as a defect. A structural test that fails the fix it asks "
			+ "for will be deleted rather than obeyed");
	}

	/// <summary>
	/// Every member of the in-memory outbox store that touches the fence keeps the fence and the
	/// mutation it authorises inside one lock region.
	/// </summary>
	[Fact]
	public void TheInMemoryOutboxStoreKeepsTheFenceAndItsMutationInOneLockRegion()
	{
		var parts = StoreParts();

		// The type is located by parsing rather than by path, so a move does not silently empty the
		// scan. Nothing below means anything if the subject was never found.
		parts.Count.ShouldBeGreaterThan(
			0,
			$"no declaration of {StoreTypeName} was found anywhere under src/**. The subject of this "
			+ "guard has been renamed or moved, so the clean result below is about nothing.");

		var offences = new List<string>();
		var blindMembers = new List<string>();
		var fencedMembers = 0;
		var guardedMutations = 0;

		foreach (var (file, type) in parts)
		{
			var scan = ScanType(type);

			offences.AddRange(scan.Offences.Select(offence => $"{file}: {offence}"));
			blindMembers.AddRange(scan.BlindMembers.Select(blind => $"{file}: {blind}"));
			fencedMembers += scan.FencedMembers;
			guardedMutations += scan.GuardedMutations;
		}

		// LIVENESS. Four members touch the fence today (the two claim entry points' shared core, the
		// fenced mark-sent core, and the two diagnostics members) and two guarded mutations sit inside
		// them: the lease write on claim and the status transition on mark-sent. A scan that finds
		// materially fewer has stopped recognising its subject, and an empty offender list from it
		// would read exactly like a correct store.
		fencedMembers.ShouldBeGreaterThanOrEqualTo(
			3,
			$"only {fencedMembers} member(s) of {StoreTypeName} were found to touch {FenceField}, "
			+ "against 4 when this was calibrated. The fence field has been renamed or the members have "
			+ "moved, so nothing below was actually examined.");

		guardedMutations.ShouldBeGreaterThanOrEqualTo(
			2,
			$"only {guardedMutations} fence-guarded mutation(s) were recognised inside those members, "
			+ "against 2 when this was calibrated. If the write the fence authorises is no longer "
			+ "recognised as a mutation then it cannot be found outside the region either, and this "
			+ "test has stopped being a detector.");

		// LIVENESS, PER MEMBER — and this is the arm the total above cannot supply. The total is met by
		// the whole type, so one member can evaluate the fence and contribute nothing while its siblings
		// carry the count. That member's write is then invisible: it is never tested against the lock
		// region, because it is never seen. The floor written to prove this guard is alive is exactly
		// what would hide it, which is why the per-member form is not a refinement of the total but a
		// different assertion.
		blindMembers.ShouldBeEmpty(
			"a member evaluates the fence and performs no write this scan recognises, so its mutation "
			+ "cannot be checked against the lock region at all. An unrecognised write is not a safe "
			+ "write — it is an unexamined one, and it reports identically to a correct member:");

		offences.ShouldBeEmpty(
			"the fencing check and the mutation it authorises no longer occur in one lock region, so a "
			+ "fresher tenure can advance the high-water between them and a superseded leader's write "
			+ "still lands:"
			+ Environment.NewLine + string.Join(Environment.NewLine, offences)
			+ Environment.NewLine
			+ "Put the check, the advance and the write it authorises inside a single lock region. The "
			+ "advance and the write must be indivisible: the write may apply only if this caller's "
			+ "token was the one the high-water accepted.");
	}

	/// <summary>The result of scanning one type declaration.</summary>
	/// <param name="Offences">Members whose fence and guarded mutation are not in one region.</param>
	/// <param name="FencedMembers">Members found to touch the fence field at all.</param>
	/// <param name="GuardedMutations">Guarded mutations recognised inside those members.</param>
	private sealed record Scanned(
		IReadOnlyList<string> Offences,
		int FencedMembers,
		int GuardedMutations,
		IReadOnlyList<string> BlindMembers);

	/// <summary>Parses a synthetic source and scans its single type.</summary>
	/// <remarks>
	/// Syntax only — no compilation, no references. The property is a property of the arrangement of
	/// statements, so the fixtures need not resolve the symbols they name.
	/// </remarks>
	private static Scanned Scan(string source) =>
		ScanType(CSharpSyntaxTree.ParseText(source)
			.GetRoot()
			.DescendantNodes()
			.OfType<TypeDeclarationSyntax>()
			.Single());

	private static Scanned ScanType(TypeDeclarationSyntax type)
	{
		var offences = new List<string>();
		var blindMembers = new List<string>();
		var fencedMembers = 0;
		var guardedMutations = 0;

		foreach (var (member, body) in Bodies(type))
		{
			var fenceTouches = body.DescendantNodes()
				.OfType<IdentifierNameSyntax>()
				.Where(identifier => identifier.Identifier.ValueText == FenceField)
				.ToArray();

			if (fenceTouches.Length == 0)
			{
				continue;
			}

			fencedMembers++;

			// Reference identity throughout: two touches are in the same critical section exactly when
			// the SAME lock node encloses both, and value equality on syntax nodes would conflate two
			// textually identical regions — which is precisely the arrangement being ruled out.
			var regions = new List<LockStatementSyntax?>();
			foreach (var region in fenceTouches.Select(static touch => touch.FirstAncestorOrSelf<LockStatementSyntax>()))
			{
				if (!regions.Any(seen => ReferenceEquals(seen, region)))
				{
					regions.Add(region);
				}
			}

			if (regions.Any(static region => region is null))
			{
				offences.Add(
					$"{member}() reads or advances {FenceField} outside any lock region, so the value a "
					+ "caller's check passes against can change before the write it authorises lands.");
				continue;
			}

			if (regions.Count > 1)
			{
				offences.Add(
					$"{member}() touches {FenceField} in {regions.Count} separate lock regions. Between "
					+ "two regions a fresher tenure can advance the high-water, and the caller whose "
					+ "check already passed proceeds on a value that no longer holds.");
				continue;
			}

			var fenceRegion = regions[0]!;

			var mutationsInThisMember = 0;

			// A write this scan CANNOT classify — excluding the fence field's own assignment, which is the
			// fence being advanced rather than a write the fence authorises. The distinction matters: a
			// member with no writes at all is a READER and has nothing to guard, while a member holding a
			// write of an unknown shape is the dangerous case, because that write is never tested against
			// the lock region. Asserting on "no recognised writes" conflates the two and flags every
			// diagnostics member; asserting on "an unclassifiable write exists" separates them.
			var unclassifiedWrites = body.DescendantNodes()
				.OfType<AssignmentExpressionSyntax>()
				.Where(assignment => !IsFenceGuardedMutation(assignment))
				.Count(assignment =>
					assignment.Left is not IdentifierNameSyntax field
					|| field.Identifier.ValueText != FenceField);

			foreach (var mutation in body.DescendantNodes().Where(IsFenceGuardedMutation))
			{
				guardedMutations++;
				mutationsInThisMember++;

				if (!mutation.Ancestors().Any(ancestor => ReferenceEquals(ancestor, fenceRegion)))
				{
					offences.Add(
						$"{member}(): `{Describe(mutation)}` is the write the fence authorises and it is "
						+ "outside the lock region holding the fence check, so the check and the write "
						+ "are separable.");
				}
			}

			// PER MEMBER, and this is the arm a whole-scan total cannot express. A member that evaluates
			// the fence and then performs no write this scan recognises is a member whose write is
			// invisible to the offence check above — it cannot be reported as outside the region, because
			// it is not seen at all. A single total hides exactly this: the other fenced members supply
			// enough mutations to clear any floor while this one contributes nothing, so the floor that
			// exists to prove the detector is alive is precisely what conceals the member it is blind to.
			if (mutationsInThisMember == 0 && unclassifiedWrites > 0)
			{
				blindMembers.Add(
					$"{member}() evaluates {FenceField} and performs {unclassifiedWrites} write(s) this "
					+ "scan cannot classify, and none it can. Those writes are never tested against the "
					+ "lock region — not because they are outside it, but because they are not seen at "
					+ "all, which reports identically to a correct member.");
			}
		}

		return new Scanned(offences, fencedMembers, guardedMutations, blindMembers);
	}

	/// <summary>
	/// A write that carries a fence decision: the completion the presented token buys, or the claim it
	/// records.
	/// </summary>
	/// <remarks>
	/// Deliberately narrow. A predicate matching every write in these members would report the store's
	/// post-transition cleanup, which is correct outside the region, and the resulting noise is what
	/// gets a structural guard deleted. See the remarks on the type.
	/// </remarks>
	private static bool IsFenceGuardedMutation(SyntaxNode node) => node switch
	{
		// A status transition on an outbound message — `message.MarkSent()`. This is the completion the
		// token buys, and the write that must not survive a superseded tenure.
		InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member } =>
			IsStatusTransition(member.Name.Identifier.ValueText),

		// Recording a claim — `_leases[id] = …`. An indexer WRITE into one of the store's own
		// collections; a read or a removal through the same collection is not a write and carries no
		// fence decision.
		AssignmentExpressionSyntax
		{
			Left: ElementAccessExpressionSyntax { Expression: IdentifierNameSyntax receiver },
		} => receiver.Identifier.ValueText.StartsWith('_'),

		// A plain member write on the message — `message.Status = OutboxStatus.DeadLettered`. This case
		// was MISSING, and its absence made the most consequential write in the store invisible: the
		// dead-letter transition assigns Status and LastError directly rather than calling a Mark* method,
		// so that member contributed ZERO recognised mutations and its terminal write could have been
		// moved outside the lock with this guard still reporting clean.
		//
		// The receiver test is deliberately WIDE — any identifier that is not one of the store's own
		// fields. Over-matching here is the safe direction: a harmless assignment inside the lock region
		// produces no offence at all, and one outside it produces a loud, inspectable false positive.
		// Under-matching is what silently retires the detector, which is the failure this case repairs.
		AssignmentExpressionSyntax
		{
			Left: MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax target },
		} => !target.Identifier.ValueText.StartsWith('_'),

		_ => false,
	};

	private static bool IsStatusTransition(string name) =>
		name.Length > 4 && name.StartsWith("Mark", StringComparison.Ordinal) && char.IsUpper(name[4]);

	/// <summary>One line naming the offending write, for a report a reader can act on.</summary>
	private static string Describe(SyntaxNode node)
	{
		var text = string.Join(' ', node.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
		return text.Length <= 80 ? text : text[..77] + "...";
	}

	/// <summary>Every method and accessor body declared on <paramref name="type"/>.</summary>
	private static IEnumerable<(string Member, SyntaxNode Body)> Bodies(TypeDeclarationSyntax type)
	{
		foreach (var method in type.Members.OfType<MethodDeclarationSyntax>())
		{
			var body = (SyntaxNode?)method.Body ?? method.ExpressionBody;
			if (body is not null)
			{
				yield return (method.Identifier.ValueText, body);
			}
		}

		foreach (var property in type.Members.OfType<PropertyDeclarationSyntax>())
		{
			if (property.ExpressionBody is not null)
			{
				yield return (property.Identifier.ValueText, property.ExpressionBody);
			}

			if (property.AccessorList is null)
			{
				continue;
			}

			foreach (var accessor in property.AccessorList.Accessors)
			{
				var body = (SyntaxNode?)accessor.Body ?? accessor.ExpressionBody;
				if (body is not null)
				{
					yield return ($"{property.Identifier.ValueText}.{accessor.Keyword.ValueText}", body);
				}
			}
		}
	}

	/// <summary>Every declaration of the store, across however many files the partial spans.</summary>
	private static IReadOnlyList<(string File, TypeDeclarationSyntax Type)> StoreParts()
	{
		var parts = new List<(string, TypeDeclarationSyntax)>();

		foreach (var file in Directory.EnumerateFiles(Path.Combine(RepoRoot, "src"), "*.cs", SearchOption.AllDirectories))
		{
			if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
				|| file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
			{
				continue;
			}

			var source = File.ReadAllText(file);
			if (!source.Contains(StoreTypeName, StringComparison.Ordinal))
			{
				continue;
			}

			parts.AddRange(CSharpSyntaxTree.ParseText(source)
				.GetRoot()
				.DescendantNodes()
				.OfType<TypeDeclarationSyntax>()
				.Where(type => type.Identifier.ValueText == StoreTypeName)
				.Select(type => (Path.GetRelativePath(RepoRoot, file), type)));
		}

		return parts;
	}
}
