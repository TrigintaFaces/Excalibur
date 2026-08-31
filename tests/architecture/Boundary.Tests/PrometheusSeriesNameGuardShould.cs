// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

namespace Boundary.Tests;

/// <summary>
/// Structural guard on the EXPORTED PROMETHEUS SERIES NAME of every metric instrument the framework ships.
/// </summary>
/// <remarks>
/// <para>
/// <b>The invariant.</b> An OpenTelemetry instrument's unit string is not decoration: the Prometheus
/// exporter appends it to the series name. It expands a recognised UCUM code to its Prometheus base-unit
/// word (<c>ms</c> becomes <c>milliseconds</c>), drops a dimensionless one (<c>1</c>, or an annotation such
/// as <c>{messages}</c>), and — this is the trap — appends anything it does NOT recognise <b>verbatim</b>.
/// </para>
/// <para>
/// <b>What that costs a consumer.</b> A counter named <c>postgres.deadlocks.total</c> declaring the
/// free-text unit <c>"deadlocks"</c> does not export as <c>postgres_deadlocks_total</c>. It exports as
/// <c>postgres_deadlocks_total_deadlocks_total</c> — the unit wedged in ahead of the counter suffix. A
/// dashboard or alert written against the documented name matches nothing, forever, with no error to
/// explain it. The failure is silent on both sides: the process starts, the scrape succeeds, and the panel
/// is simply empty.
/// </para>
/// <para>
/// <b>Why a guard rather than a runtime check.</b> The exporter cannot reasonably refuse an unrecognised
/// unit — throwing inside a metrics export path would take down a consumer's process over a naming
/// problem, trading a wrong dashboard for an outage. The right place to fail is here, at build time, where
/// the cost of being wrong is a red test.
/// </para>
/// <para>
/// <b>What this asserts.</b> Not "the instrument exists" — the emitted series NAME. For every
/// <c>Meter.Create*</c> call under <c>src/</c> it reconstructs the name the Prometheus exporter would
/// publish and fails when the unit lands in it verbatim.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
public sealed class PrometheusSeriesNameGuardShould
{
	/// <summary>
	/// The exporter's unit table: UCUM code to the Prometheus base-unit word it expands to. Any unit
	/// outside this table (and not reducible to empty by annotation-stripping) is appended unchanged.
	/// </summary>
	private static readonly Dictionary<string, string> UnitTable = new(StringComparer.Ordinal)
	{
		["d"] = "days",
		["h"] = "hours",
		["min"] = "minutes",
		["s"] = "seconds",
		["ms"] = "milliseconds",
		["us"] = "microseconds",
		["ns"] = "nanoseconds",
		["By"] = "bytes",
		["KiBy"] = "kibibytes",
		["MiBy"] = "mebibytes",
		["GiBy"] = "gibibytes",
		["TiBy"] = "tibibytes",
		["KBy"] = "kilobytes",
		["MBy"] = "megabytes",
		["GBy"] = "gigabytes",
		["TBy"] = "terabytes",
		["B"] = "bytes",
		["KB"] = "kilobytes",
		["MB"] = "megabytes",
		["GB"] = "gigabytes",
		["TB"] = "terabytes",
		["m"] = "meters",
		["V"] = "volts",
		["A"] = "amperes",
		["J"] = "joules",
		["W"] = "watts",
		["g"] = "grams",
		["Cel"] = "celsius",
		["Hz"] = "hertz",
		["1"] = "",
		["%"] = "percent",
		["$"] = "dollars",
	};

	/// <summary>The exporter's "per" table, used for a rate unit such as <c>{messages}/s</c>.</summary>
	private static readonly Dictionary<string, string> PerUnitTable = new(StringComparer.Ordinal)
	{
		["s"] = "second",
		["m"] = "minute",
		["h"] = "hour",
		["d"] = "day",
		["w"] = "week",
		["mo"] = "month",
		["y"] = "year",
	};

	/// <summary>
	/// Informal unit abbreviations that are not UCUM codes but mean the same thing, so a name ending
	/// in one still collects the expanded unit as a second suffix.
	/// </summary>
	private static readonly string[] InformalUnitAbbreviations =
	[
		"millis", "msec", "msecs", "sec", "secs", "micros", "nanos", "pct",
	];

	/// <summary>
	/// Instrument names that end in a unit token and are deliberately left that way, each with a
	/// written reason.
	/// </summary>
	private static readonly IReadOnlyDictionary<string, string> NameAllowlist =
		new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["elasticsearch_operation_duration_ms"] =
				"Real instance of the same defect, exporting as "
				+ "elasticsearch_operation_duration_ms_milliseconds. Owned by the Elasticsearch package, "
				+ "which a different workstream holds; recorded here so the guard stays green rather "
				+ "than being weakened, and so this entry disappears when that package renames it.",
		};

	/// <summary>
	/// Instruments whose free-text unit is deliberately retained, each with a written reason. An entry
	/// here is a claim that strict UCUM would make the exported name WORSE, not a place to park work.
	/// </summary>
	private static readonly IReadOnlyDictionary<string, string> Allowlist =
		new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["dispatch.batch.throughput"] =
				"Rate gauge. 'messages/second' exports as '..._messages_per_second', which is the name a "
				+ "consumer wants. The strict-UCUM spelling '{messages}/s' reduces to an empty left operand "
				+ "and exports as '..._per_second' with a doubled underscore — a worse name, not a better one.",
			["dispatch.pubsub.throughput"] =
				"Rate gauge; same reasoning as dispatch.batch.throughput.",
		};

	[Fact]
	public void EveryShippedInstrument_ExportsASeriesNameFreeOfItsRawUnit()
	{
		var srcRoot = Path.Combine(TestHelpers.GetRepositoryRoot(), "src");
		Directory.Exists(srcRoot).ShouldBeTrue($"Expected source root at '{srcRoot}'.");

		var sites = Directory
			.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
			.SelectMany(path => FindInstruments(path, File.ReadAllText(path)))
			.ToList();

		// Non-vacuity floor. The framework declares several hundred instruments; a scan finding far fewer
		// means the parser or the source layout drifted, and every assertion below would pass over an empty
		// set. Both arms matter: the second proves units are being READ, not merely that calls were found.
		sites.Count.ShouldBeGreaterThanOrEqualTo(
			250,
			$"Expected to find the framework's metric instruments under '{srcRoot}'. Found only "
			+ $"{sites.Count} — the scanner or the source layout drifted; this guard must not pass vacuously.");

		var withUnits = sites.Where(s => s.Unit is { Length: > 0 }).ToList();
		withUnits.Count.ShouldBeGreaterThanOrEqualTo(
			200,
			$"Found {sites.Count} instruments but only {withUnits.Count} with a unit literal. The unit "
			+ "argument is no longer being read, so this guard would pass without inspecting anything.");

		var violations = withUnits
			.Where(s => s.Name is not null && !Allowlist.ContainsKey(s.Name))
			.Select(s => new
			{
				Site = s,
				Exported = ExportedSeriesName(s.Name!, s.Unit!, s.IsCounter),
				Clean = ExportedSeriesName(s.Name!, unit: null, s.IsCounter),
			})
			.Where(x => UnitIsAppendedVerbatim(x.Site.Unit!))
			.OrderBy(x => x.Site.Location, StringComparer.Ordinal)
			.Select(x =>
				$"{x.Site.Location}: unit \"{x.Site.Unit}\" on \"{x.Site.Name}\" exports as "
				+ $"'{x.Exported}' (should be '{x.Clean}' or a UCUM-suffixed form of it)")
			.ToList();

		violations.ShouldBeEmpty(
			"Every shipped instrument must declare a unit the Prometheus exporter recognises, so that the "
			+ "exported series name is the one a consumer's dashboard and alert can match. Use a UCUM code "
			+ "('ms', 's', 'By', '%'), the dimensionless '1', or a UCUM annotation for a count of things "
			+ "('{messages}', '{operations}') — an annotation is stripped, so the name keeps its meaning "
			+ "without gaining a suffix. A free-text unit is appended to the name unchanged. Offenders:"
			+ Environment.NewLine
			+ string.Join(Environment.NewLine, violations));
	}

	[Fact]
	public void NoShippedInstrument_CarriesItsUnitInItsName()
	{
		var srcRoot = Path.Combine(TestHelpers.GetRepositoryRoot(), "src");

		var named = Directory
			.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
			.SelectMany(path => FindInstruments(path, File.ReadAllText(path)))
			.Where(s => s.Name is not null)
			.ToList();

		// Non-vacuity floor for the NAME arm, which is necessarily lower than the unit arm's: this
		// scans instruments declared with a literal name, and roughly a third of the framework's
		// instruments take their name from a constant instead. Every instrument that carries a unit
		// in its name today is in the literal set, so current coverage is complete; a FUTURE
		// violation introduced through a constant would not be seen here. Resolving constants is the
		// upgrade if that ever happens.
		named.Count.ShouldBeGreaterThanOrEqualTo(
			190,
			$"Found only {named.Count} literal-named instruments — the scanner or the source layout "
			+ "drifted, and this guard would pass over an empty set.");

		var violations = named
			.Where(s => !NameAllowlist.ContainsKey(s.Name!))
			.Select(s => new { Site = s, Token = TrailingUnitToken(s.Name!) })
			.Where(x => x.Token is not null)
			.OrderBy(x => x.Site.Location, StringComparer.Ordinal)
			.Select(x => $"{x.Site.Location}: \"{x.Site.Name}\" ends in the unit token \"{x.Token}\"")
			.ToList();

		violations.ShouldBeEmpty(
			"An instrument name must not carry its own unit. The unit belongs in the unit argument, "
			+ "where the exporter can expand it once; a name that already ends in the unit gets it a "
			+ "second time, so a histogram called 'x.duration_ms' with unit 'ms' exports as "
			+ "'x_duration_ms_milliseconds'. Drop the unit from the name and keep the unit argument. "
			+ "Note this changes the OTLP instrument name a non-Prometheus backend keys on, so it is a "
			+ "breaking change and belongs in the changelog. Offenders:"
			+ Environment.NewLine
			+ string.Join(Environment.NewLine, violations));
	}

	/// <summary>
	/// Returns the unit ABBREVIATION an instrument name ends with, or null when it ends with none.
	/// </summary>
	/// <remarks>
	/// Only an abbreviation counts, never the expanded word. A name ending in the expanded word —
	/// <c>caching.adaptive_ttl.ttl_seconds</c> with unit <c>s</c>, or
	/// <c>dispatch.context.flow.size_bytes</c> with unit <c>By</c> — is NOT a defect: the exporter
	/// skips the suffix when the name already ends with the expanded unit, so those export as
	/// <c>caching_adaptive_ttl_ttl_seconds</c> and <c>dispatch_context_flow_size_bytes</c>, which is
	/// exactly the Prometheus base-unit-in-the-name convention. Flagging them would demand a breaking
	/// rename that makes nothing better. The abbreviation is the harmful case, because the exporter
	/// appends the expansion alongside it: <c>x.duration_ms</c> + <c>ms</c> exports as
	/// <c>x_duration_ms_milliseconds</c>.
	/// </remarks>
	private static string? TrailingUnitToken(string name)
	{
		var last = name.Split('.', '_').LastOrDefault();
		if (last is null)
		{
			return null;
		}

		var isUcumCode = UnitTable.Keys.Any(k => k.Equals(last, StringComparison.OrdinalIgnoreCase));
		var isInformal = InformalUnitAbbreviations.Contains(last, StringComparer.OrdinalIgnoreCase);
		return isUcumCode || isInformal ? last : null;
	}

	/// <summary>
	/// Liveness arm. A guard that cannot go red is a green with nothing behind it, so this pins the
	/// detector itself against both a unit that must be caught and units that must not be.
	/// </summary>
	[Fact]
	public void Detector_FlagsAFreeTextUnitAndAcceptsEveryUcumForm()
	{
		// RED arm: the exact shape this guard exists to catch, named from the defect that motivated it.
		UnitIsAppendedVerbatim("deadlocks").ShouldBeTrue("A free-text unit must be detected.");
		UnitIsAppendedVerbatim("count").ShouldBeTrue("A free-text unit must be detected.");
		ExportedSeriesName("postgres.deadlocks.total", "deadlocks", isCounter: true)
			.ShouldBe("postgres_deadlocks_total_deadlocks_total");
		ExportedSeriesName("dispatch.messages.processed", "count", isCounter: true)
			.ShouldBe("dispatch_messages_processed_count_total");

		// GREEN arm: each accepted spelling, and the name it actually produces.
		UnitIsAppendedVerbatim("{deadlocks}").ShouldBeFalse("An annotation is stripped by the exporter.");
		UnitIsAppendedVerbatim("1").ShouldBeFalse("The dimensionless unit is dropped by the exporter.");
		UnitIsAppendedVerbatim("ms").ShouldBeFalse("A UCUM code is expanded, not appended raw.");
		ExportedSeriesName("postgres.deadlocks.total", "{deadlocks}", isCounter: true)
			.ShouldBe("postgres_deadlocks_total");
		ExportedSeriesName("dispatch.messages.processed", "{messages}", isCounter: true)
			.ShouldBe("dispatch_messages_processed_total");
		ExportedSeriesName("dispatch.handler.duration", "ms", isCounter: false)
			.ShouldBe("dispatch_handler_duration_milliseconds");

		// RED arm for the name check: a name ending in a unit ABBREVIATION collects the expansion too.
		TrailingUnitToken("dispatch.saga.duration_ms").ShouldBe("ms");
		TrailingUnitToken("dispatch.context.flow.size_by").ShouldBe("by");
		ExportedSeriesName("dispatch.saga.duration_ms", "ms", isCounter: false)
			.ShouldBe("dispatch_saga_duration_ms_milliseconds");

		// GREEN arm: the corrected name; a name ending in the EXPANDED unit, which the exporter does
		// not double; and an ordinary word that merely looks unit-ish.
		TrailingUnitToken("dispatch.saga.duration").ShouldBeNull();
		TrailingUnitToken("caching.adaptive_ttl.ttl_seconds").ShouldBeNull();
		TrailingUnitToken("dispatch.context.flow.size_bytes").ShouldBeNull();
		TrailingUnitToken("dispatch.transport.starts_total").ShouldBeNull();
		TrailingUnitToken("pubsub.streaming.streams").ShouldBeNull();
		ExportedSeriesName("dispatch.saga.duration", "ms", isCounter: false)
			.ShouldBe("dispatch_saga_duration_milliseconds");
		ExportedSeriesName("caching.adaptive_ttl.ttl_seconds", "s", isCounter: false)
			.ShouldBe("caching_adaptive_ttl_ttl_seconds");
		ExportedSeriesName("dispatch.context.flow.size_bytes", "By", isCounter: false)
			.ShouldBe("dispatch_context_flow_size_bytes");
	}

	/// <summary>
	/// Reports whether the exporter would append <paramref name="unit"/> to the series name unchanged,
	/// which is the defect. A recognised UCUM code is expanded; an annotation-only unit reduces to empty
	/// and is dropped.
	/// </summary>
	private static bool UnitIsAppendedVerbatim(string unit)
	{
		var resolved = ResolveUnit(unit);
		return resolved.Length > 0 && string.Equals(resolved, unit, StringComparison.Ordinal);
	}

	/// <summary>Reconstructs the series name the Prometheus exporter publishes for an instrument.</summary>
	private static string ExportedSeriesName(string name, string? unit, bool isCounter)
	{
		var sanitized = SanitizeMetricName(name);

		if (!string.IsNullOrEmpty(unit))
		{
			var resolved = ResolveUnit(unit);
			if (!sanitized.EndsWith(resolved, StringComparison.Ordinal))
			{
				sanitized += "_" + resolved;
			}
		}

		if (isCounter && !sanitized.EndsWith("_total", StringComparison.Ordinal))
		{
			sanitized += "_total";
		}

		return sanitized;
	}

	/// <summary>Applies annotation-stripping, then rate expansion, then the unit table.</summary>
	private static string ResolveUnit(string unit)
	{
		var stripped = RemoveAnnotations(unit);

		var slash = stripped.IndexOf('/', StringComparison.Ordinal);
		if (slash >= 0 && slash != stripped.Length - 1)
		{
			return MapUnit(stripped[..slash]) + "_per_" + MapPerUnit(stripped[(slash + 1)..]);
		}

		return MapUnit(stripped);
	}

	private static string MapUnit(string unit) =>
		UnitTable.TryGetValue(unit, out var mapped) ? mapped : unit;

	private static string MapPerUnit(string unit) =>
		PerUnitTable.TryGetValue(unit, out var mapped) ? mapped : unit;

	/// <summary>Removes UCUM annotations, so <c>{messages}</c> reduces to the empty string.</summary>
	private static string RemoveAnnotations(string unit)
	{
		if (!unit.Contains('{', StringComparison.Ordinal))
		{
			return unit;
		}

		var sb = new StringBuilder(unit.Length);
		var depth = 0;
		foreach (var c in unit)
		{
			if (c == '{')
			{
				depth++;
			}
			else if (c == '}')
			{
				if (depth > 0)
				{
					depth--;
				}
			}
			else if (depth == 0)
			{
				sb.Append(c);
			}
		}

		return sb.ToString();
	}

	/// <summary>Replaces every run of non-alphanumeric characters with a single underscore.</summary>
	private static string SanitizeMetricName(string name)
	{
		var sb = new StringBuilder(name.Length);
		var lastWasUnderscore = false;
		for (var i = 0; i < name.Length; i++)
		{
			var c = name[i];
			if (i == 0 && char.IsAsciiDigit(c))
			{
				sb.Append('_');
				lastWasUnderscore = true;
				continue;
			}

			if (!char.IsLetterOrDigit(c) && c != ':')
			{
				if (!lastWasUnderscore)
				{
					sb.Append('_');
					lastWasUnderscore = true;
				}
			}
			else
			{
				sb.Append(c);
				lastWasUnderscore = false;
			}
		}

		return sb.ToString();
	}

	/// <summary>One <c>Meter.Create*</c> call site.</summary>
	private sealed record InstrumentSite(string Location, string? Name, string? Unit, bool IsCounter);

	private static readonly string[] InstrumentFactories =
	[
		"CreateCounter",
		"CreateHistogram",
		"CreateUpDownCounter",
		"CreateGauge",
		"CreateObservableGauge",
		"CreateObservableCounter",
		"CreateObservableUpDownCounter",
	];

	/// <summary>
	/// Locates every instrument factory call in one file and extracts its name and unit literals.
	/// </summary>
	/// <remarks>
	/// The observable overloads take the observe-callback second, so the unit is their THIRD argument
	/// rather than their second. Getting that wrong reads a callback as a unit and silently finds nothing,
	/// which is why the non-vacuity floor above checks the count of units actually parsed.
	/// </remarks>
	private static IEnumerable<InstrumentSite> FindInstruments(string path, string text)
	{
		var fileName = Path.GetFileName(path);

		foreach (var factory in InstrumentFactories)
		{
			var searchFrom = 0;
			while (true)
			{
				var at = text.IndexOf(factory, searchFrom, StringComparison.Ordinal);
				if (at < 0)
				{
					break;
				}

				searchFrom = at + factory.Length;

				// Reject a longer identifier that merely starts with this one (CreateCounter vs
				// CreateCounterBuilder), and any member of a different name ending in it.
				if (at > 0 && (char.IsLetterOrDigit(text[at - 1]) || text[at - 1] == '_'))
				{
					continue;
				}

				var open = SkipGenericArguments(text, searchFrom);
				if (open < 0 || text[open] != '(')
				{
					continue;
				}

				var arguments = SplitArguments(text, open);
				if (arguments.Count == 0)
				{
					continue;
				}

				// Named arguments are common here — `unit:` and `description:` especially — and an
				// earlier revision of this guard skipped any call that used one. That silently
				// excluded whole files, including one holding the exact defect this guard exists to
				// catch, which it then reported clean. Read the named form rather than skipping it:
				// only the leading arguments are positional, so the name still comes from index 0 and
				// the unit from its label whenever it carries one.
				var positional = arguments.TakeWhile(a => !IsNamedArgument(a)).ToList();
				var named = arguments
					.Where(IsNamedArgument)
					.GroupBy(NamedArgumentLabel, StringComparer.Ordinal)
					.ToDictionary(g => g.Key, g => NamedArgumentValue(g.First()), StringComparer.Ordinal);

				var unitIndex = factory.StartsWith("CreateObservable", StringComparison.Ordinal) ? 2 : 1;

				var nameArgument = named.TryGetValue("name", out var labelledName)
					? labelledName
					: positional.Count > 0 ? positional[0] : null;

				var unitArgument = named.TryGetValue("unit", out var labelledUnit)
					? labelledUnit
					: positional.Count > unitIndex ? positional[unitIndex] : null;

				var line = text.Take(at).Count(c => c == '\n') + 1;

				yield return new InstrumentSite(
					$"{fileName}:{line}",
					nameArgument is null ? null : Literal(nameArgument),
					unitArgument is null ? null : Literal(unitArgument),
					factory is "CreateCounter" or "CreateObservableCounter");
			}
		}
	}

	/// <summary>Reports whether an argument is written in <c>name: value</c> form.</summary>
	private static bool IsNamedArgument(string argument)
	{
		var i = 0;
		while (i < argument.Length && char.IsWhiteSpace(argument[i]))
		{
			i++;
		}

		var start = i;
		while (i < argument.Length && (char.IsLetterOrDigit(argument[i]) || argument[i] == '_'))
		{
			i++;
		}

		if (i == start)
		{
			return false;
		}

		while (i < argument.Length && char.IsWhiteSpace(argument[i]))
		{
			i++;
		}

		return i < argument.Length
			&& argument[i] == ':'
			&& (i + 1 >= argument.Length || argument[i + 1] != ':');
	}

	/// <summary>Returns the label of a <c>name: value</c> argument.</summary>
	private static string NamedArgumentLabel(string argument) =>
		argument.Trim().Split(':', 2)[0].Trim();

	/// <summary>Returns the value part of a <c>name: value</c> argument.</summary>
	private static string NamedArgumentValue(string argument) =>
		argument.Trim().Split(':', 2)[1];

	/// <summary>Returns the string literal an argument consists of, or null when it is an expression.</summary>
	private static string? Literal(string argument)
	{
		var trimmed = argument.Trim();
		return trimmed.Length >= 2
			&& trimmed[0] == '"'
			&& trimmed[^1] == '"'
			&& !trimmed[1..^1].Contains('"', StringComparison.Ordinal)
				? trimmed[1..^1]
				: null;
	}

	/// <summary>Skips a generic argument list and any following whitespace, returning the next index.</summary>
	private static int SkipGenericArguments(string text, int index)
	{
		if (index < text.Length && text[index] == '<')
		{
			var depth = 0;
			while (index < text.Length)
			{
				if (text[index] == '<')
				{
					depth++;
				}
				else if (text[index] == '>')
				{
					depth--;
					if (depth == 0)
					{
						index++;
						break;
					}
				}
				else if (text[index] is ';' or '{' or '}')
				{
					return -1; // Not a generic argument list after all.
				}

				index++;
			}
		}

		while (index < text.Length && char.IsWhiteSpace(text[index]))
		{
			index++;
		}

		return index < text.Length ? index : -1;
	}

	/// <summary>Splits the top-level, comma-separated arguments of the call whose '(' is at <paramref name="open"/>.</summary>
	private static List<string> SplitArguments(string text, int open)
	{
		var arguments = new List<string>();
		var depth = 1;
		var start = open + 1;
		var inString = '\0';

		for (var i = start; i < text.Length; i++)
		{
			var c = text[i];

			if (inString != '\0')
			{
				if (c == '\\')
				{
					i++;
				}
				else if (c == inString)
				{
					inString = '\0';
				}

				continue;
			}

			switch (c)
			{
				case '"':
				case '\'':
					inString = c;
					break;
				case '(':
				case '[':
				case '{':
					depth++;
					break;
				case ')':
				case ']':
				case '}':
					depth--;
					if (depth == 0)
					{
						arguments.Add(text[start..i]);
						return arguments;
					}

					break;
				case ',' when depth == 1:
					arguments.Add(text[start..i]);
					start = i + 1;
					break;
				default:
					break;
			}
		}

		return [];
	}
}
