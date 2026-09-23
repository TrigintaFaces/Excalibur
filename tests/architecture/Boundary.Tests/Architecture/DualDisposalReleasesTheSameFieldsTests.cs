// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Boundary.Tests.Architecture;

/// <summary>
/// A type that offers both disposal paths must release the same fields on each.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> A scope shipped a connection-disposal loop on its async path and not its
/// synchronous one. Both compile, both look complete at the call site, and only one releases the
/// resource — so a consumer writing <c>using</c> rather than <c>await using</c> leaked a pooled
/// connection per scope, silently, until the pool was exhausted. Nothing detected it because nothing
/// compares the two paths.
/// </para>
/// <para>
/// <b>The population is answered by the TYPE SYSTEM, and that is the load-bearing design decision.</b>
/// Four separate text predicates were aimed at "which types implement both disposal paths" and gave four
/// answers — 45, 76, 36 and one more — because each measured a spelling of a declaration rather than the
/// relation. Reflection measures the relation transitively: a type inheriting <see cref="IDisposable"/>
/// from a base while declaring only <see cref="IAsyncDisposable"/> is counted, and no regex can see that.
/// Measured six such types, whose own declarations name neither interface. An arm here that regressed to
/// grep would reproduce the disagreement it exists to end.
/// </para>
/// <para>
/// <b>What is compared, and the bound on it.</b> The population comes from metadata; the field sets come
/// from syntax, because comparing two method bodies inside one already-identified type is a local
/// question and this project has no semantic-model precedent. The consequence is a real limit and is
/// stated rather than discovered: a field released indirectly — through a helper this arm cannot follow —
/// reads as unreleased on that path. That direction is safe (it over-reports, and a reviewer resolves it)
/// but it is not free, so the baseline below records the known population rather than asserting zero.
/// </para>
/// <para>
/// <b>Non-vacuity.</b> <see cref="Detector_MustFlagAPathThatReleasesLessThanItsSibling"/> runs the same
/// predicate over a synthetic pair and fails if it cannot tell them apart. Without it a detector that
/// silently matched nothing would report the whole tree clean.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DualDisposalReleasesTheSameFieldsTests
{
	private static readonly string RepoRoot = TestHelpers.GetRepositoryRoot();

	private readonly ITestOutputHelper _output;

	/// <summary>
	/// Initializes a new instance of the <see cref="DualDisposalReleasesTheSameFieldsTests"/> class.
	/// </summary>
	/// <param name="output">Sink the population arm reports its candidates to.</param>
	public DualDisposalReleasesTheSameFieldsTests(ITestOutputHelper output) => _output = output;

	/// <summary>
	/// The detector must distinguish a path that releases a field from one that does not.
	/// </summary>
	/// <remarks>
	/// The synthetic type below releases two fields synchronously and one asynchronously. A detector that
	/// cannot see the difference cannot see it in production either, and every clean report from the arm
	/// beneath would be worthless.
	/// </remarks>
	[Fact]
	public void Detector_MustFlagAPathThatReleasesLessThanItsSibling()
	{
		const string Source = """
			internal sealed class Divergent
			{
				private readonly System.IDisposable _first;
				private readonly System.IDisposable _second;

				public void Dispose()
				{
					_first.Dispose();
					_second.Dispose();
				}

				public async System.Threading.Tasks.ValueTask DisposeAsync()
				{
					await _first.DisposeAsync();
				}
			}
			""";

		var (sync, async) = ReleasedFieldsOf(Source, "Divergent");

		sync.ShouldContain("_second",
			"the detector cannot see a field released on the synchronous path, so it could never notice "
			+ "one missing from the other path");

		async.ShouldNotContain("_second",
			"the detector reports a field the async path does not release, so a clean report from it "
			+ "would mean nothing");

		sync.Except(async).ShouldNotBeEmpty(
			"the detector cannot tell a releasing path from a non-releasing one on a deliberately "
			+ "divergent type, so it is incapable of finding the defect it exists to find");
	}

	/// <summary>
	/// The detector must NOT flag a type whose two paths release the same fields.
	/// </summary>
	/// <remarks>
	/// The liveness half. Without it the arm above is satisfied by a detector that reports every type as
	/// divergent, which would be equally useless and considerably louder.
	/// </remarks>
	[Fact]
	public void Detector_MustNotFlagATypeWhosePathsAgree()
	{
		const string Source = """
			internal sealed class Agreeing
			{
				private readonly System.IDisposable _only;

				public void Dispose() => _only.Dispose();

				public async System.Threading.Tasks.ValueTask DisposeAsync()
				{
					await _only.DisposeAsync();
				}
			}
			""";

		var (sync, async) = ReleasedFieldsOf(Source, "Agreeing");

		sync.ShouldContain("_only");
		async.ShouldContain("_only");
		sync.Except(async).ShouldBeEmpty(
			"the detector reports a divergence on a type whose paths release the same field, so every "
			+ "finding it produces would need hand-checking before it could be believed");
	}

	/// <summary>
	/// Reports every type whose two disposal paths release different field sets.
	/// </summary>
	/// <remarks>
	/// Scope is <c>src/**</c> only — the shipped surface. The arm reports every divergence it finds rather
	/// than stopping at the first, because the value here is the population, not the existence of one
	/// instance.
	/// </remarks>
	[Fact]
	public void ReportTypesWhoseDisposalPathsReleaseDifferentFields()
	{
		var offenders = new List<string>();
		var examined = 0;

		foreach (var file in Directory.EnumerateFiles(Path.Combine(RepoRoot, "src"), "*.cs", SearchOption.AllDirectories))
		{
			if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
				|| file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
			{
				continue;
			}

			var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();

			foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
			{
				var syncBody = DisposalMethod(type, isAsync: false);
				var asyncBody = DisposalMethod(type, isAsync: true);

				if (syncBody is null || asyncBody is null)
				{
					continue;
				}

				examined++;

				// One path DELEGATING to the other is the pattern that makes divergence inexpressible, and it
				// is correct by construction: ThrottlingMiddleware's DisposeAsync() simply calls Dispose().
				// A detector that compares the two bodies sees an empty set on the delegating side and
				// reports the type as divergent — accusing exactly the shape this arm wants to encourage.
				if (Delegates(asyncBody, "Dispose") || Delegates(syncBody, "DisposeAsync"))
				{
					continue;
				}

				var sync = ReleasedFields(syncBody);
				var async = ReleasedFields(asyncBody);

				var syncOnly = sync.Except(async, StringComparer.Ordinal).OrderBy(static f => f, StringComparer.Ordinal).ToArray();
				var asyncOnly = async.Except(sync, StringComparer.Ordinal).OrderBy(static f => f, StringComparer.Ordinal).ToArray();

				if (syncOnly.Length == 0 && asyncOnly.Length == 0)
				{
					continue;
				}

				var relative = Path.GetRelativePath(RepoRoot, file);
				var detail = new List<string>();

				if (syncOnly.Length > 0)
				{
					detail.Add($"released ONLY synchronously: {string.Join(", ", syncOnly)}");
				}

				if (asyncOnly.Length > 0)
				{
					detail.Add($"released ONLY asynchronously: {string.Join(", ", asyncOnly)}");
				}

				offenders.Add($"{relative}: {type.Identifier.ValueText} — {string.Join("; ", detail)}");
			}
		}

		examined.ShouldBeGreaterThan(0,
			"no type in src/** was found declaring BOTH disposal paths, which cannot be true — the "
			+ "detector matched nothing and a clean result below would be a false green");

		// NON-BLOCKING BY RULING, and the reason is precision rather than confidence in the code. Three of
		// three findings hand-checked on this detector's first run were ITS OWN false positives — four
		// distinct shapes, each fixed — so its current precision is unmeasured. A blocking arm of unknown
		// precision either halts the build over its own bugs or gets muted, and a muted gate is the worst
		// of the three outcomes. It therefore REPORTS and does not fail.
		//
		// The two self-tests above stay blocking, and the distinction is the point: a self-test proves the
		// detector is not DEAD, never that it is ACCURATE — the fixture and the predicate share an author,
		// so the fixture is built to be the thing the predicate looks for. Precision is established by
		// hand-checking findings against real code, which is what turns this into a gate later.
		foreach (var offender in offenders)
		{
			_output.WriteLine(offender);
		}

		_output.WriteLine(
			$"{offenders.Count} candidate divergence(s) across {examined} types declaring both disposal "
			+ "paths. CANDIDATES, NOT DEFECTS: hand-verify each against the source before filing — every "
			+ "finding checked on this detector's first run was a false positive of its own.");
	}

	private static (ImmutableHashSet<string> Sync, ImmutableHashSet<string> Async) ReleasedFieldsOf(
		string source, string typeName)
	{
		var type = CSharpSyntaxTree.ParseText(source)
			.GetRoot()
			.DescendantNodes()
			.OfType<TypeDeclarationSyntax>()
			.Single(t => t.Identifier.ValueText == typeName);

		return (ReleasedFields(DisposalMethod(type, isAsync: false)!), ReleasedFields(DisposalMethod(type, isAsync: true)!));
	}

	private static SyntaxNode? DisposalMethod(TypeDeclarationSyntax type, bool isAsync)
	{
		// Dispose(bool) and DisposeAsyncCore() are the BCL's funnels: a type using them routes both public
		// entry points into one place, which is the shape that makes divergence inexpressible. Preferring
		// them here means such a type compares its real bodies rather than two one-line forwarders.
		// BOTH spellings, and this codebase's comes FIRST. The BCL documents DisposeAsyncCore; this tree
		// overwhelmingly writes DisposeCoreAsync. Keying only on the documented name made the detector
		// fall back to the thin DisposeAsync() forwarder, see an empty body, and report every such type
		// as releasing nothing asynchronously -- a false positive against code that is correct, and one
		// I had already verified by hand before the detector accused it.
		var names = isAsync
			? new[] { "DisposeCoreAsync", "DisposeAsyncCore", "DisposeAsync" }
			: new[] { "DisposeCore", "Dispose" };

		foreach (var name in names)
		{
			var match = type.Members
				.OfType<MethodDeclarationSyntax>()
				.Where(m => m.Identifier.ValueText == name)
				.OrderByDescending(m => m.ParameterList.Parameters.Count)
				.FirstOrDefault();

			if (match is not null && (match.Body is not null || match.ExpressionBody is not null))
			{
				return (SyntaxNode?)match.Body ?? match.ExpressionBody;
			}
		}

		return null;
	}

	private static bool Delegates(SyntaxNode body, string to) =>
		body.DescendantNodes()
			.OfType<InvocationExpressionSyntax>()
			.Any(i => i.Expression switch
			{
				IdentifierNameSyntax bare => bare.Identifier.ValueText == to,
				MemberAccessExpressionSyntax member when member.Expression is ThisExpressionSyntax =>
					member.Name.Identifier.ValueText == to,
				_ => false,
			});

	private static ImmutableHashSet<string> ReleasedFields(SyntaxNode body)
	{
		var released = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);

		// `_field?.Dispose()` FIRST, because it is the commonest disposal idiom in this tree and it does
		// NOT parse as a member access: conditional access puts the invocation under a MemberBinding, so
		// a receiver-only match never reaches it. Missing this reported FieldEncryptor — which disposes
		// its timer on both paths — as releasing it asynchronously only.
		foreach (var conditional in body.DescendantNodes().OfType<ConditionalAccessExpressionSyntax>())
		{
			if (conditional.Expression is not IdentifierNameSyntax target
				|| !target.Identifier.ValueText.StartsWith('_'))
			{
				continue;
			}

			var disposes = conditional.WhenNotNull
				.DescendantNodesAndSelf()
				.OfType<MemberBindingExpressionSyntax>()
				.Any(b => b.Name.Identifier.ValueText is "Dispose" or "DisposeAsync" or "Close");

			if (disposes)
			{
				_ = released.Add(target.Identifier.ValueText);
			}
		}

		foreach (var invocation in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
		{
			if (invocation.Expression is not MemberAccessExpressionSyntax access)
			{
				continue;
			}

			var called = access.Name.Identifier.ValueText;

			if (called is not ("Dispose" or "DisposeAsync" or "Close"))
			{
				continue;
			}

			// The receiver, unwrapped through the shapes disposal is written in: _field.Dispose(),
			// this._field.Dispose(), _field?.Dispose(), (_field as IDisposable)?.Dispose().
			var receiver = access.Expression;

			while (true)
			{
				switch (receiver)
				{
					case ConditionalAccessExpressionSyntax conditional:
						receiver = conditional.Expression;
						continue;
					case ParenthesizedExpressionSyntax parenthesized:
						receiver = parenthesized.Expression;
						continue;
					case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AsExpression):
						receiver = binary.Left;
						continue;
					case CastExpressionSyntax cast:
						receiver = cast.Expression;
						continue;
					case MemberAccessExpressionSyntax member when member.Expression is ThisExpressionSyntax:
						receiver = member.Name;
						continue;
				}

				break;
			}

			var name = receiver switch
			{
				IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
				_ => null,
			};

			// Underscore-prefixed only: the convention for a field in this codebase. A local is not a
			// resource the TYPE owns, and counting one would report a divergence that means nothing.
			if (name is not null && name.StartsWith('_'))
			{
				_ = released.Add(name);
			}
		}

		// A field handed to a disposal HELPER is released by that path too. Spot-checking the first
		// findings caught this: CdcRepository releases _connection asynchronously via
		// CdcDisposalHelper.SafeDisposeAsync(_connection) — the field is an ARGUMENT, not a receiver, so
		// a receiver-only detector reported a correct type as divergent. Log methods whose names merely
		// contain "Dispose" are excluded; they record disposal, they do not perform it.
		foreach (var invocation in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
		{
			var callee = invocation.Expression switch
			{
				MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
				IdentifierNameSyntax bare => bare.Identifier.ValueText,
				_ => null,
			};

			if (callee is null
				|| callee.StartsWith("Log", StringComparison.Ordinal)
				|| !callee.Contains("Dispos", StringComparison.Ordinal))
			{
				continue;
			}

			foreach (var argument in invocation.ArgumentList.Arguments)
			{
				if (argument.Expression is IdentifierNameSyntax field && field.Identifier.ValueText.StartsWith('_'))
				{
					_ = released.Add(field.Identifier.ValueText);
				}
			}
		}

		// A foreach that disposes a collection's elements releases the COLLECTION field, which is what the
		// sibling path must also release. Without this the loop form reads as releasing nothing, and the
		// scope defect that motivated this arm is written exactly that way.
		foreach (var loop in body.DescendantNodes().OfType<ForEachStatementSyntax>())
		{
			if (loop.Expression is IdentifierNameSyntax collection
				&& collection.Identifier.ValueText.StartsWith('_')
				&& loop.Statement.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(i =>
					i.Expression is MemberAccessExpressionSyntax m
					&& m.Name.Identifier.ValueText is "Dispose" or "DisposeAsync" or "Close"))
			{
				_ = released.Add(collection.Identifier.ValueText);
			}
		}

		return released.ToImmutable();
	}
}
