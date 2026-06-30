// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.IO;
using System.Text.RegularExpressions;

namespace Excalibur.Cdc.Tests;

/// <summary>
/// Persistent structural regression guard for bead <c>q3w5cv</c> (S855 REVIEW_ARCH / pxhqri follow-up,
/// FR-B2 / ADR-338): the poll-batch CDC processors advance the durable checkpoint <b>only</b> when the
/// single shared guard's <see cref="Excalibur.Cdc.CdcFatalDecision.AdvanceCheckpoint"/> field is read and
/// true — <em>true structural enforcement</em>, not placement-only.
/// </summary>
/// <remarks>
/// <para>
/// The sibling <c>CdcProcessorFatalGateStructureShould</c> proves the <b>placement</b> property (no durable
/// advance inside a fault <c>catch</c>). This lock proves the stronger <b>field-gate</b> property the
/// REVIEW_ARCH demanded: in each poll-batch processor's <c>ProcessBatchInternalAsync</c>, the durable
/// advance call sits <b>inside an <c>if (decision.AdvanceCheckpoint …)</c> block</b> that reads the field.
/// </para>
/// <para>
/// <b>Mutant → RED (non-vacuous):</b> mutating the gate to <c>if (true)</c> (or otherwise dropping the
/// <c>AdvanceCheckpoint</c> read so the advance becomes unconditional) deletes the field read from the
/// method body and moves the advance call outside any <c>AdvanceCheckpoint</c>-gated block — both
/// assertions below turn RED. The "advance past an unprocessed change on a fault" violation (AC-N3.1)
/// is therefore inexpressible without editing this one literal gate, and that edit is caught here.
/// </para>
/// <para>
/// <b>Cosmos</b> takes a real <c>CosmosClient</c> (the change-feed iterator is not unit-fakeable), so this
/// gate is verified structurally for both poll-batch processors uniformly via a comment-stripped scan of
/// the repo <c>src/</c> tree (assembly-independent — the two processors live in two separate packages).
/// The behavioural at-least-once proof for the streaming providers lives in the Postgres/Mongo real-infra
/// restart-redelivery locks (AC-N3.4).
/// </para>
/// <para>
/// <b>Non-vacuity floor:</b> fails if it cannot locate <c>src/</c>, if either processor source is missing,
/// if <c>ProcessBatchInternalAsync</c> cannot be isolated, or if the expected durable-advance anchor call
/// is absent — a mis-located or mis-parsed scan fails LOUD rather than passing silently.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Cdc")]
public sealed class CdcCheckpointAdvanceGateShould
{
	// The poll-batch CDC processors whose ProcessBatchInternalAsync durable advance must be gated on the
	// CdcFatalDecision.AdvanceCheckpoint field. Each entry pins the processor file name and the impl's
	// durable-advance anchor call that MUST appear inside the AdvanceCheckpoint-gated block.
	private static readonly (string Processor, string AdvanceAnchor)[] PollBatchGates =
	[
		// Cosmos: `if (decision.AdvanceCheckpoint && lastPosition is not null) { await ConfirmPositionAsync(...) }`
		("CosmosDbCdcProcessor", "ConfirmPositionAsync"),

		// DynamoDb: `if (decision.AdvanceCheckpoint) { AdvanceOrRetireShard(...); if (autoConfirm) { ConfirmPositionAsync } }`
		("DynamoDbCdcProcessor", "AdvanceOrRetireShard"),
	];

	[Fact]
	public void GateEveryPollBatchDurableAdvanceOnTheAdvanceCheckpointField()
	{
		var srcRoot = LocateSrcRoot();
		_ = srcRoot.ShouldNotBeNull(
			"Could not locate the repository 'src/' directory from the test assembly location; "
			+ "the CDC checkpoint-advance gate guard cannot run (refusing to pass vacuously).");

		var violations = new List<string>();

		foreach (var (processor, advanceAnchor) in PollBatchGates)
		{
			var file = LocateProcessorFile(srcRoot!, processor);
			file.ShouldNotBeNull(
				$"Expected to locate '{processor}.cs' under '{srcRoot}', but did not — the source scan is "
				+ "mis-located; refusing to pass vacuously.");

			var source = StripComments(File.ReadAllText(file!));

			var methodBody = ExtractMethodBody(source, "ProcessBatchInternalAsync");
			if (methodBody is null)
			{
				violations.Add(
					$"{processor}: could not isolate the 'ProcessBatchInternalAsync' method body — if it was "
					+ "renamed/inlined, update this lock to bind the new poll-batch entry point.");
				continue;
			}

			// (1) The field read MUST be present in the method body. The `if (true)` mutant deletes it → RED.
			if (!Regex.IsMatch(methodBody, @"\bdecision\s*\.\s*AdvanceCheckpoint\b"))
			{
				violations.Add(
					$"{processor}.ProcessBatchInternalAsync: no 'decision.AdvanceCheckpoint' read found — the "
					+ "durable checkpoint advance is no longer gated on the shared guard's decision (mutated to an "
					+ "unconditional advance?). A fault could advance past an unprocessed change (AC-N3.1).");
				continue;
			}

			// (2) The durable-advance anchor MUST sit inside the AdvanceCheckpoint-gated `if` block. The mutant
			// `if (true)` moves the anchor outside any AdvanceCheckpoint-gated block → RED.
			var gatedBlock = ExtractAdvanceCheckpointGatedBlock(methodBody);
			if (gatedBlock is null)
			{
				violations.Add(
					$"{processor}.ProcessBatchInternalAsync: found an 'AdvanceCheckpoint' read but could not isolate "
					+ "the `if (decision.AdvanceCheckpoint …) {{ … }}` gated block — the gate shape changed; review.");
				continue;
			}

			if (!gatedBlock.Contains(advanceAnchor, StringComparison.Ordinal))
			{
				violations.Add(
					$"{processor}.ProcessBatchInternalAsync: the durable-advance anchor '{advanceAnchor}' is NOT inside "
					+ $"the `if (decision.AdvanceCheckpoint …)` block — the advance is no longer gated on the decision. "
					+ "A fault would advance the checkpoint past the failing change (AC-N3.1 violation).");
			}
		}

		violations.ShouldBeEmpty(
			"q3w5cv checkpoint-advance field-gate regression — every poll-batch processor must gate its durable "
			+ "advance on a 'decision.AdvanceCheckpoint' read:\n" + string.Join("\n", violations));
	}

	// Isolates the body `{ … }` of the named method via brace matching (comment-stripped input). Returns the
	// inner text of the method body, or null if the method/opening brace cannot be located.
	private static string? ExtractMethodBody(string text, string methodName)
	{
		// Find the DECLARATION site (not a call site): an access modifier + return type precede the method
		// name on the same statement, so `await ProcessBatchInternalAsync(...)` call sites are excluded
		// ([^;{}()]* spans `async Task<int> ` but stops at any paren/brace/semicolon).
		var decl = new Regex(
			@"\b(?:private|protected|internal|public)\b[^;{}()]*?\b" + Regex.Escape(methodName) + @"\s*\(",
			RegexOptions.Compiled);
		var m = decl.Match(text);
		if (!m.Success)
		{
			return null;
		}

		// Skip past the parameter list `(...)` (brace-agnostic: parens balance), then find the body's `{`.
		var parenDepth = 0;
		var i = text.IndexOf('(', m.Index);
		if (i < 0)
		{
			return null;
		}

		for (; i < text.Length; i++)
		{
			if (text[i] == '(')
			{
				parenDepth++;
			}
			else if (text[i] == ')')
			{
				parenDepth--;
				if (parenDepth == 0)
				{
					i++;
					break;
				}
			}
		}

		var open = text.IndexOf('{', i);
		if (open < 0)
		{
			return null;
		}

		var end = MatchBrace(text, open);
		return end > open ? text[(open + 1)..end] : null;
	}

	// Within a method body, finds the first `if ( … AdvanceCheckpoint … ) { … }` and returns the inner text
	// of its block. Returns null if no AdvanceCheckpoint-gated if/block is found.
	private static string? ExtractAdvanceCheckpointGatedBlock(string methodBody)
	{
		var ifGate = new Regex(@"\bif\s*\(", RegexOptions.Compiled);
		foreach (Match m in ifGate.Matches(methodBody))
		{
			// Brace-agnostic paren match over the `if (...)` condition.
			var parenDepth = 0;
			var i = m.Index + m.Length - 1; // points at the '('
			var condStart = i + 1;
			for (; i < methodBody.Length; i++)
			{
				if (methodBody[i] == '(')
				{
					parenDepth++;
				}
				else if (methodBody[i] == ')')
				{
					parenDepth--;
					if (parenDepth == 0)
					{
						break;
					}
				}
			}

			if (i >= methodBody.Length)
			{
				continue;
			}

			var condition = methodBody[condStart..i];
			if (!Regex.IsMatch(condition, @"\bdecision\s*\.\s*AdvanceCheckpoint\b"))
			{
				continue;
			}

			var open = methodBody.IndexOf('{', i);
			if (open < 0)
			{
				continue;
			}

			var end = MatchBrace(methodBody, open);
			if (end > open)
			{
				return methodBody[(open + 1)..end];
			}
		}

		return null;
	}

	// Returns the index of the `}` matching the `{` at openIndex, or -1 if unbalanced.
	private static int MatchBrace(string text, int openIndex)
	{
		var depth = 0;
		for (var i = openIndex; i < text.Length; i++)
		{
			if (text[i] == '{')
			{
				depth++;
			}
			else if (text[i] == '}')
			{
				depth--;
				if (depth == 0)
				{
					return i;
				}
			}
		}

		return -1;
	}

	private static string? LocateProcessorFile(string srcRoot, string processor)
		=> Directory
			.EnumerateFiles(srcRoot, processor + ".cs", SearchOption.AllDirectories)
			.FirstOrDefault(p =>
				!p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
				&& !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

	private static string StripComments(string text)
	{
		text = Regex.Replace(text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
		text = Regex.Replace(text, @"//[^\n]*", string.Empty);
		return text;
	}

	// Walks up from the test assembly location to the repository root (a directory containing both src/ and
	// tests/) and returns its src/ path, or null if not found.
	private static string? LocateSrcRoot()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null)
		{
			var src = Path.Combine(dir.FullName, "src");
			var tests = Path.Combine(dir.FullName, "tests");
			if (Directory.Exists(src) && Directory.Exists(tests))
			{
				return src;
			}

			dir = dir.Parent;
		}

		return null;
	}
}
