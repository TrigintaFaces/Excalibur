#!/usr/bin/env bash
# receipt-without-work-gate.sh — a method that RECORDS SUCCESS IT DID NOT PERFORM. This gate
# detects one.
#
# THE DEFECT CLASS:
#
#   Doing nothing is often correct. The defect is doing nothing AND leaving behind evidence that
#   something happened — a done-flag, a status, a completion timestamp, or a past-tense log line —
#   which a later reader treats as proof. The shape is not "no-op". It is NO-OP PLUS A RECEIPT.
#
#   Three instances were confirmed by hand before this gate existed, and every one of them passed
#   every gate in the tree at the time:
#
#     * a breach-notification service that set the status to notified, stamped the notified-at
#       timestamp, wrote a "notification sent" log line, and sent nothing — three independent
#       false witnesses on a statutory obligation;
#     * six stores whose initialiser read  if (autoCreate) { createSchema(); }  and then set the
#       initialised flag REGARDLESS, so with the flag off initialisation did nothing and recorded
#       that it had succeeded — and the flag then suppressed every later attempt;
#     * an evidence collector that emitted "controls documented: 14 of 14" as a literal, so a run
#       that collected nothing still produced an auditor-facing document asserting completeness.
#
#   The class is invisible to tests, because the code under test reports success. It is invisible
#   to review, because each instance reads as reasonable in isolation. It is visible only when
#   someone asks "what did this actually DO?", which is not a question any other gate here asks.
#
# WHAT IT ASSERTS: for every durable mark written in this tree, either the mark is reached only
#   after the work it attests, or the type writing it cannot perform work at all, or nobody reads
#   the mark.
#
# THE ORACLE, AND WHY IT IS DECIDABLE:
#
#   The hand discriminator for this class is two questions, and taken literally only one of them
#   can be mechanised:
#
#     Q1  is there a path on which the mark is set WITHOUT the work it attests having occurred?
#     Q2  does any later reader treat that mark as proof?
#
#   Q2 in full generality is whole-program reachability and no grep decides it. So the oracle
#   restricts itself to the mark shapes whose readers are STRUCTURALLY KNOWN, which is what turns
#   an undecidable question into a bounded one:
#
#     a done-flag        its reader must be in the same file, or nothing consults it. Decided by
#                        counting reads against writes: a flag only ever written is UNREAD, which
#                        is Q1 without Q2 — harmless, recorded, and never failed on.
#     a status / stamp   IS the observable. It is persisted or projected precisely so that a later
#                        reader consults it; that reader is the consumer, and no in-file evidence
#                        can rule it out.
#     a past-tense log   IS the observable, and its reader is the operator. "Sending" is not a
#                        receipt; "Sent" is. Only the past tense is matched.
#
#   Q1 is then per-method dominance, decided on CONDITIONAL depth rather than brace depth. A mark
#   at conditional depth zero is reached on every path through the method. A try, using, lock or
#   finally block is NOT conditional — it always runs — and counting it as one would report every
#   disposal pattern in the tree. An if, else, switch or catch may not execute, and a mark outside
#   one, while every unit of work sits inside it, is a receipt written on a path that did nothing.
#   That is the defect, stated structurally.
#
#   A LOOP is deliberately NOT conditional either, and the reason is semantic rather than
#   syntactic. A loop that runs zero times means there was NOTHING TO DO, so "the pass completed"
#   is true — a commit scope with no enlisted transaction did commit, and a cascade erasure over
#   no subjects did complete. An `if` that is false means the work was SKIPPED, and the mark then
#   lies about work that was due. Counting loops as conditional was measured against this tree: it
#   adds three findings, and reading all three showed every one of them honest. A gate that
#   reports a correct batch loop trains its readers to ignore it.
#
# THE ONE STRUCTURAL EXEMPTION, WHICH IS NOT AN ALLOWLIST:
#
#   An entity's whole job is to record a transition its CALLER performed. OutboundMessage.MarkSent
#   sets the status and the sent-at stamp and sends nothing, and that is correct: the sending is
#   the outbox processor's, and the entity is where the result is written down. Failing on it
#   would fail on every state mutator in the tree, and this gate would be deleted within a week
#   and would deserve it.
#
#   The property that separates the two is structural: a type that holds NO COLLABORATOR cannot
#   perform out-of-process work, so "this method did no work" is not a finding about it. Measured
#   on this tree the separation is total — the entity types score zero collaborators and every
#   service, adapter and store scores one or more. No type is named anywhere, a new entity is
#   exempt the day it is written, and an entity that grows a collaborator stops being exempt the
#   same day.
#
#   A collaborator is a readonly field OR a PRIMARY-CONSTRUCTOR parameter. A field-only test is
#   blind to the 155 files in this tree that declare their dependencies positionally, and would
#   exempt every one of them as an entity.
#
#   Resolved per FILE. A file mixing an entity with a service scores the service's collaborators
#   and the entity's mutators stay under judgement, so the approximation OVER-reports a subject
#   and can never drop one.
#
# WHAT IT DOES NOT CLAIM, stated so its green is not read as wider than it is:
#
#   * A receipt written in a DIFFERENT METHOD from the work it attests is not detected. The oracle
#     is per-method; a call graph is a different instrument.
#   * A capability MARKER registered independently of the wiring it attests is spelled as a DI
#     descriptor, not a field, and is not in this census.
#   * A receipt that is a LITERAL FIGURE in a generated report — the evidence-collector instance
#     above — is not a field assignment and is not in this census.
#   * A receipt written by an ABSENCE, such as a conformance arm that returns without asserting,
#     is the sibling gate's subject (conformance-arm-skip-gate.sh), not this one.
#   * Only C# under src/ is scanned. Scripts that emit auditor-facing documents are out of scope.
#
# WHY IT BLOCKS RATHER THAN REPORTS:
#
#   A report-only reporter over this class was designed once and measured before it shipped: about
#   5 percent precision and 17 percent recall, because it was aimed at the whole done-flag
#   population, which is roughly 95 percent correct. A report nobody must act on, over a set that
#   is mostly correct, is a control that cannot fail — this epic's own defect class, one level up,
#   committed by the fix. It was retired rather than shipped.
#
#   This gate is aimed at the shape instead of the population: of 436 marks in the tree it fails
#   on 9. A gate that cannot fail is the defect it was written to find.
#
# EXIT CODES (every one mapped by the caller; a non-0/1 is NEVER a pass):
#   0  PASS    scanned; every mark is dominated by its work, written by a type that cannot work,
#              or read by nobody
#   1  FAIL    a mark is reached on a path that skips the work it attests, and somebody reads it
#   2  REFUSE  could not evaluate (no src tree / census program missing / census failed / zero
#              marks seen anywhere == the census is blind to how this tree writes them)
#   3  REFUSE  --self-test failed (the gate itself is broken or vacuous)
#   *  REFUSE  unknown argument == could-not-evaluate
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CENSUS_PY="$(dirname "${BASH_SOURCE[0]}")/receipt-without-work-census.py"

# ── Census ──────────────────────────────────────────────────────────────────────────────────────
# One python pass over src/. It emits tab-separated records and renders no verdict; the shell
# decides, so the exit-code contract stays in one readable place.
#
#   ESCAPES   file line method mark kind   mark reached on a path skipping every unit of work
#   BARE      file line method mark kind   the method performs no work at all and marks anyway
#   DOMINATED file line method mark        the mark is reached only after the work -- correct
#   MUTATOR   file line method mark        written by a type holding no collaborator -- correct
#   UNREAD    file line method mark        escapes its guard, nobody reads it -- harmless
#   COUNTS    files methods marks

run_gate() {
	local root="$1" records

	if [ ! -d "$root/src" ]; then
		echo "REFUSE: no src tree at $root/src -- cannot evaluate." >&2; return 2
	fi
	if [ ! -f "$CENSUS_PY" ]; then
		echo "REFUSE: the census program is missing at $CENSUS_PY -- cannot evaluate." >&2; return 2
	fi

	records="$(python3 "$CENSUS_PY" "$root")" || {
		echo "REFUSE: the census failed to run -- cannot evaluate." >&2; return 2; }

	local files methods marks
	IFS=$'\t' read -r _ files methods marks <<<"$(printf '%s\n' "$records" | grep '^COUNTS' | head -1)"

	if [ "${marks:-0}" -eq 0 ]; then
		echo "REFUSE: no durable mark was found anywhere under $root/src. Either this tree stopped" >&2
		echo "        recording completion, or the census is blind to how it records it. A tree" >&2
		echo "        with nothing to judge is not a clean tree. Not a pass." >&2
		return 2
	fi

	local escapes bare dominated mutator unread findings
	escapes="$(printf '%s\n' "$records" | grep -c '^ESCAPES' || true)"
	bare="$(printf '%s\n' "$records" | grep -c '^BARE' || true)"
	dominated="$(printf '%s\n' "$records" | grep -c '^DOMINATED' || true)"
	mutator="$(printf '%s\n' "$records" | grep -c '^MUTATOR' || true)"
	unread="$(printf '%s\n' "$records" | grep -c '^UNREAD' || true)"
	findings=$((escapes + bare))

	echo "durable marks: $marks across $files file(s), $methods method(s)."
	echo "  $dominated reached only after the work they attest"
	echo "  $mutator written by a type holding no collaborator (an entity records its caller's work)"
	echo "  $unread reached without the work, and read by nobody (recorded, not failed on)"
	echo "  $findings reached without the work, and read"

	if [ "$findings" -gt 0 ]; then
		echo "FAIL: each line below writes down that work happened on a path where it did not." >&2
		printf '%s\n' "$records" | grep '^ESCAPES' | while IFS=$'\t' read -r _ f l m mark kind; do
			echo "  - $f:$l $m() -- the $kind '$mark' sits outside the conditional that holds all the work." >&2
		done
		printf '%s\n' "$records" | grep '^BARE' | while IFS=$'\t' read -r _ f l m mark kind; do
			echo "  - $f:$l $m() -- writes the $kind '$mark' and performs no work at all." >&2
		done
		echo "Two dispositions are acceptable per site, and it is an architecture call which:" >&2
		echo "  (a) fail loudly when the work did not happen, or (b) do not write the receipt." >&2
		echo "Silently succeeding is not one of them. Deleting the mark outright is not either --" >&2
		echo "something legitimately reads it when the work DID happen." >&2
		return 1
	fi

	echo "PASS: every durable mark is earned, unwritable, or unread."
	return 0
}

# ── Self-test ───────────────────────────────────────────────────────────────────────────────────
# A gate that cannot fail is the defect it was written to find, so the failing arms are the
# load-bearing ones. Every arm below is a whole synthetic tree run through the real run_gate.

self_test() {
	local tmp fails=0
	tmp="$(mktemp -d)"
	trap 'rm -rf "$tmp"' RETURN

	# A failing arm that only checks the exit code can be green for the WRONG REASON -- an
	# unrelated finding elsewhere in the same synthetic tree fails the gate and the arm reads it
	# as proof. Every FAIL arm below therefore names the record it expects, so the arm asserts
	# the property it is about rather than merely that something went red. This caught a real
	# false-green in this gate's own self-test before it shipped.
	finds() { # finds <name> <expected-record-regex> <root>
		local name="$1" want="$2" root="$3"
		if python3 "$CENSUS_PY" "$root" 2>/dev/null | grep -qE "$want"; then
			echo "  self-test ok:   $name (record matched)"
		else
			echo "  self-test FAIL: $name -- no census record matching /$want/" >&2
			fails=$((fails + 1))
		fi
	}

	arm() { # arm <name> <expected-rc> <root>
		local name="$1" want="$2" root="$3" got
		run_gate "$root" >/dev/null 2>&1
		got=$?
		if [ "$got" -ne "$want" ]; then
			echo "  self-test FAIL: $name -- expected exit $want, got $got" >&2
			fails=$((fails + 1))
		else
			echo "  self-test ok:   $name (exit $got)"
		fi
	}

	mkdir -p "$tmp/base/src/Widget"
	earned "$tmp/base"

	# ---- PASS 1: the mark is reached only after the work. The ordinary, correct case, and the
	# arm that stops this gate becoming "fail on every done-flag" -- which would be wrong.
	arm "a flag set AFTER the work passes" 0 "$tmp/base"

	# ---- FAIL 2: THE CONFIRMED SHAPE. The work sits inside a conditional and the flag is set
	# regardless, so with the option off initialisation does nothing and records that it
	# succeeded. This is the arm that makes the gate non-vacuous; it is the six-store instance.
	cp -r "$tmp/base" "$tmp/escapes"; escapes_flag "$tmp/escapes"
	arm "a flag set REGARDLESS of a conditional that holds all the work fails" 1 "$tmp/escapes"
	finds "  ...and it is the InitializeAsync flag that fired, not something else" 		'^ESCAPES.*InitializeAsync.*_initialized' "$tmp/escapes"

	# ---- FAIL 3: the same defect wearing a status and a timestamp instead of a boolean. This is
	# the breach-notification instance: status moved to notified, stamp written, nothing sent.
	cp -r "$tmp/base" "$tmp/status"; escapes_status "$tmp/status"
	arm "a status+timestamp receipt on a path that sent nothing fails" 1 "$tmp/status"
	finds "  ...and BOTH witnesses fired -- the status and the timestamp" 		'^ESCAPES.*NotifyAsync.*NotifiedAt' "$tmp/status"

	# ---- FAIL 4: a past-tense log line as the only witness. The log IS the receipt and the
	# operator IS the reader, so a method that logs "sent" and sends nothing is the class.
	cp -r "$tmp/base" "$tmp/logonly"; bare_log "$tmp/logonly"
	arm "a past-tense 'sent' log over a method that sends nothing fails" 1 "$tmp/logonly"
	finds "  ...and it is the log line that fired" '^BARE.*SendAsync.*LogWidgetSent' "$tmp/logonly"

	# ---- PASS 5: THE EXEMPTION. An entity holding no collaborator cannot perform work, so its
	# mark-writing method is recording a transition its caller performed. Without this arm the
	# gate fails on every state mutator in the tree and gets deleted.
	cp -r "$tmp/base" "$tmp/entity"; entity_mutator "$tmp/entity"
	arm "a state mutator on a type holding no collaborator is exempt, not a finding" 0 "$tmp/entity"

	# ---- FAIL 6: the exemption must not swallow a real finding. This file holds an entity AND a
	# service whose flag escapes its guard. Exempting the file because it CONTAINS an entity
	# would hide the defect sitting beside it.
	cp -r "$tmp/base" "$tmp/mixed"; entity_beside_service "$tmp/mixed"
	arm "an entity beside a real service does NOT exempt the file (still fails)" 1 "$tmp/mixed"
	finds "  ...and it is the SERVICE that fired, not the entity beside it" 		'^ESCAPES.*StartAsync.*_started' "$tmp/mixed"

	# ---- PASS 7: Q1 WITHOUT Q2. The flag escapes its guard and nothing ever reads it, so no
	# reader is misled. Failing here would be a finding with no victim.
	cp -r "$tmp/base" "$tmp/unread"; unread_flag "$tmp/unread"
	arm "a flag nobody reads is harmless, not a failure" 0 "$tmp/unread"

	# ---- PASS 8: try / using / lock are NOT conditional -- they always run. Reading them as
	# guards would report every disposal and every connection-scope pattern in the tree.
	cp -r "$tmp/base" "$tmp/trylock"; mark_in_try "$tmp/trylock"
	arm "a mark inside try/using/lock is not 'outside a guard'" 0 "$tmp/trylock"

	# ---- FAIL 9: a service holding its collaborators as PRIMARY-CONSTRUCTOR PARAMETERS rather
	# than readonly fields. A field-only test reads such a type as holding no collaborator and
	# exempts it as an entity, which would silently hide every finding in it. Measured on this
	# tree: 155 files declare their dependencies that way, so the hole is wide even though none
	# of them writes a receipt today. Closing it cost zero added findings.
	cp -r "$tmp/base" "$tmp/primary"; primary_ctor_service "$tmp/primary"
	arm "a service holding collaborators in a PRIMARY CONSTRUCTOR is not exempt" 1 "$tmp/primary"
	finds "  ...and it is judged, not exempted as an entity" 		'^BARE.*StartAsync.*_started' "$tmp/primary"

	# ---- PASS 10: the work is a GENERIC call. A work detector requiring the paren to follow the
	# identifier cannot see GetService<T>(...) at all, and then reports a method that does real
	# work through generics as doing none. This arm was added after that blindness produced a
	# live false positive; without it the regression is silent and looks like a finding.
	cp -r "$tmp/base" "$tmp/generic"; work_is_generic "$tmp/generic"
	arm "work performed through a GENERIC call is still work" 0 "$tmp/generic"

	# ---- PASS 11: a batch loop that ran zero times. Nothing was due, so "completed" is true. The
	# same method with an `if` in place of the loop DOES fail (arm 2) -- the distinction is that a
	# skipped `if` means work was due and dropped, while an empty loop means none was due.
	cp -r "$tmp/base" "$tmp/emptyloop"; mark_after_loop "$tmp/emptyloop"
	arm "a completion mark after a loop that may run zero times is not a finding" 0 "$tmp/emptyloop"

	# ---- PASS 12: a mark quoted in a doc comment or a string literal is prose, not an assignment.
	# The census blanks comments and string bodies; without that, documentation becomes findings.
	cp -r "$tmp/base" "$tmp/prose"; mark_in_prose "$tmp/prose"
	arm "a mark quoted in a doc comment or string is not an assignment" 0 "$tmp/prose"

	# ---- REFUSE 11: no mark anywhere. Either the tree stopped recording completion, or the
	# census is blind to how it records it. A tree with nothing to judge is not a clean tree, and
	# rendering a pass over it is this gate's own defect class turned on itself.
	cp -r "$tmp/base" "$tmp/blind"; no_marks "$tmp/blind"
	arm "zero marks REFUSES (the census is blind, not the tree clean)" 2 "$tmp/blind"

	# ---- REFUSE 12/13: nothing to evaluate.
	cp -r "$tmp/base" "$tmp/nosrc"; rm -rf "$tmp/nosrc/src"
	arm "a missing src tree REFUSES (not a pass)" 2 "$tmp/nosrc"

	local saved="$CENSUS_PY"
	CENSUS_PY="$tmp/definitely-not-here.py"
	arm "a missing census program REFUSES (not a pass)" 2 "$tmp/base"
	CENSUS_PY="$saved"

	if [ "$fails" -ne 0 ]; then
		echo "self-test: FAILED ($fails arm(s))" >&2
		return 3
	fi
	echo "self-test: PASS (20 arms; five FAIL the gate and five name the record that fired,
            which is what proves it can fail AND that it fails for the right reason)"
	return 0
}

# ── Self-test fixtures ──────────────────────────────────────────────────────────────────────────
# Written by printf rather than a here-document so the arms above read as a list of properties.

earned() {
	printf '%s\n' \
		'public sealed class WidgetStore' \
		'{' \
		'	private readonly IWidgetClient _client;' \
		'	private bool _initialized;' \
		'' \
		'	public async Task InitializeAsync(CancellationToken ct)' \
		'	{' \
		'		if (_initialized)' \
		'		{' \
		'			return;' \
		'		}' \
		'' \
		'		await _client.CreateSchemaAsync(ct);' \
		'		_initialized = true;' \
		'	}' \
		'}' > "$1/src/Widget/WidgetStore.cs"
}

escapes_flag() {
	printf '%s\n' \
		'public sealed class WidgetStore' \
		'{' \
		'	private readonly IWidgetClient _client;' \
		'	private bool _initialized;' \
		'' \
		'	public async Task InitializeAsync(CancellationToken ct)' \
		'	{' \
		'		if (_options.AutoCreateSchema)' \
		'		{' \
		'			await _client.CreateSchemaAsync(ct);' \
		'		}' \
		'' \
		'		_initialized = true;' \
		'	}' \
		'' \
		'	public bool IsReady() => _initialized;' \
		'}' > "$1/src/Widget/WidgetStore.cs"
}

escapes_status() {
	printf '%s\n' \
		'public sealed class BreachNotifier' \
		'{' \
		'	private readonly INotificationChannel _channel;' \
		'' \
		'	public async Task NotifyAsync(Breach breach, CancellationToken ct)' \
		'	{' \
		'		if (_channel is not null)' \
		'		{' \
		'			await _channel.DeliverAsync(breach, ct);' \
		'		}' \
		'' \
		'		breach.Status = BreachStatus.Notified;' \
		'		breach.NotifiedAt = DateTimeOffset.UtcNow;' \
		'	}' \
		'}' > "$1/src/Widget/BreachNotifier.cs"
}

bare_log() {
	printf '%s\n' \
		'public sealed class WidgetSender' \
		'{' \
		'	private readonly ILogger _logger;' \
		'' \
		'	public Task SendAsync(Widget widget, CancellationToken ct)' \
		'	{' \
		'		LogWidgetSent(widget.Id);' \
		'		return Task.CompletedTask;' \
		'	}' \
		'}' > "$1/src/Widget/WidgetSender.cs"
}

entity_mutator() {
	printf '%s\n' \
		'public sealed class OutboundWidget' \
		'{' \
		'	public WidgetStatus Status { get; set; }' \
		'' \
		'	public DateTimeOffset? SentAt { get; set; }' \
		'' \
		'	public void MarkSent()' \
		'	{' \
		'		Status = WidgetStatus.Sent;' \
		'		SentAt = DateTimeOffset.UtcNow;' \
		'	}' \
		'}' > "$1/src/Widget/OutboundWidget.cs"
}

entity_beside_service() {
	printf '%s\n' \
		'public sealed class OutboundWidget' \
		'{' \
		'	public WidgetStatus Status { get; set; }' \
		'' \
		'	public void MarkSent()' \
		'	{' \
		'		Status = WidgetStatus.Sent;' \
		'	}' \
		'}' \
		'' \
		'public sealed class WidgetPump' \
		'{' \
		'	private readonly IWidgetClient _client;' \
		'	private bool _started;' \
		'' \
		'	public async Task StartAsync(CancellationToken ct)' \
		'	{' \
		'		if (_options.Enabled)' \
		'		{' \
		'			await _client.OpenAsync(ct);' \
		'		}' \
		'' \
		'		_started = true;' \
		'	}' \
		'' \
		'	public bool Running() => _started;' \
		'}' > "$1/src/Widget/OutboundWidget.cs"
}

unread_flag() {
	printf '%s\n' \
		'public sealed class WidgetStore' \
		'{' \
		'	private readonly IWidgetClient _client;' \
		'	private bool _initialized;' \
		'' \
		'	public async Task InitializeAsync(CancellationToken ct)' \
		'	{' \
		'		if (_options.AutoCreateSchema)' \
		'		{' \
		'			await _client.CreateSchemaAsync(ct);' \
		'		}' \
		'' \
		'		_initialized = true;' \
		'	}' \
		'}' > "$1/src/Widget/WidgetStore.cs"
}

mark_in_try() {
	printf '%s\n' \
		'public sealed class WidgetStore' \
		'{' \
		'	private readonly IWidgetClient _client;' \
		'	private bool _initialized;' \
		'' \
		'	public async Task InitializeAsync(CancellationToken ct)' \
		'	{' \
		'		using (var scope = _client.BeginScope())' \
		'		{' \
		'			await _client.CreateSchemaAsync(ct);' \
		'		}' \
		'' \
		'		_initialized = true;' \
		'	}' \
		'' \
		'	public bool IsReady() => _initialized;' \
		'}' > "$1/src/Widget/WidgetStore.cs"
}

mark_in_prose() {
	printf '%s\n' \
		'public sealed class WidgetStore' \
		'{' \
		'	private readonly IWidgetClient _client;' \
		'	private bool _initialized;' \
		'' \
		'	/// <summary>Sets _initialized = true; once the schema exists.</summary>' \
		'	public async Task InitializeAsync(CancellationToken ct)' \
		'	{' \
		'		if (_options.AutoCreateSchema)' \
		'		{' \
		'			await _client.CreateSchemaAsync(ct);' \
		'			_initialized = true;' \
		'		}' \
		'		else' \
		'		{' \
		'			await _client.VerifySchemaAsync(ct);' \
		'			_initialized = true;' \
		'		}' \
		'' \
		'		LogSchemaReady("_initialized = true;");' \
		'	}' \
		'' \
		'	public bool IsReady() => _initialized;' \
		'}' > "$1/src/Widget/WidgetStore.cs"
}

primary_ctor_service() {
	printf '%s
' 		'internal sealed partial class WidgetPump(' 		'	IWidgetClient client,' 		'	ILogger<WidgetPump> logger) : IHostedService' 		'{' 		'	private bool _started;' 		'' 		'	public Task StartAsync(CancellationToken ct)' 		'	{' 		'		_started = true;' 		'		return Task.CompletedTask;' 		'	}' 		'' 		'	public bool Running() => _started;' 		'}' > "$1/src/Widget/WidgetPump.cs"
}

work_is_generic() {
	printf '%s
' 		'public sealed class WidgetWarmer' 		'{' 		'	private readonly IServiceProvider _serviceProvider;' 		'	private bool _warmed;' 		'' 		'	public Task WarmupAsync(CancellationToken ct)' 		'	{' 		'		_ = _serviceProvider.GetService<ILoggerFactory>();' 		'		_warmed = true;' 		'		return Task.CompletedTask;' 		'	}' 		'' 		'	public bool IsReady() => _warmed;' 		'}' > "$1/src/Widget/WidgetWarmer.cs"
}

mark_after_loop() {
	printf '%s
' 		'public sealed class WidgetCommitScope' 		'{' 		'	private readonly IWidgetClient _client;' 		'' 		'	public async Task CommitAsync(CancellationToken ct)' 		'	{' 		'		foreach (var transaction in _transactions.Values)' 		'		{' 		'			await transaction.CommitAsync(ct);' 		'		}' 		'' 		'		Status = TransactionStatus.Committed;' 		'	}' 		'}' > "$1/src/Widget/WidgetCommitScope.cs"
}

no_marks() {
	printf '%s\n' \
		'public sealed class WidgetStore' \
		'{' \
		'	private readonly IWidgetClient _client;' \
		'' \
		'	public Task CreateAsync(CancellationToken ct) => _client.CreateSchemaAsync(ct);' \
		'}' > "$1/src/Widget/WidgetStore.cs"
}

case "${1:-}" in
	"")          run_gate "$REPO_ROOT"; exit $? ;;
	--self-test) self_test; exit $? ;;
	-h|--help)   sed -n '2,110p' "${BASH_SOURCE[0]}"; exit 0 ;;
	*)           echo "REFUSE: unknown argument '${1}'. Usage: $(basename "$0") [--self-test]" >&2; exit 2 ;;
esac
