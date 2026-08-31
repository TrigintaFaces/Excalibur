// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Boundary.Tests;

/// <summary>
/// Pins the BSON wire representation of every persisted MongoDB document field whose CLR type does not
/// determine one.
/// <para>
/// <b>Why this is a contract and not formatting.</b> MongoDB equality and range comparison are
/// type-sensitive: a filter encoded as one BSON type does not match a value stored as another, even when
/// both render the same text. These collections are queried by consumers from other languages, so the
/// persisted type is a published interface. Changing it is a breaking change that no compiler, no public
/// API baseline and no round-trip-through-our-own-code test can see, because our reader and our writer
/// drift together.
/// </para>
/// <para>
/// <b>Which CLR types are ambiguous.</b> <see cref="Guid"/>, <see cref="DateTime"/>,
/// <see cref="DateTimeOffset"/> and <see cref="decimal"/> each have more than one defensible BSON
/// encoding, so the driver's choice depends on configuration rather than on the type. Everything else
/// (<c>string</c>, <c>int</c>, <c>bool</c>, …) maps to exactly one BSON type and needs no declaration.
/// Enums are deliberately out of scope: their default is well-defined (<c>Int32</c>), and whether they
/// should instead persist as strings is a modelling question, not a drift hazard.
/// </para>
/// <para>
/// <b>The three arms.</b> Arm one is exhaustiveness — every ambiguous property is either annotated or
/// listed in the baseline, so a NEW unannotated field fails. Arm two is liveness — for annotated
/// properties, the type actually produced by the serializer must equal the type the attribute declares,
/// which is what an attribute rendered ineffective by a class map or a globally registered serializer
/// would break. Arm three is drift — baselined properties must still persist as the shape recorded for
/// them, so an unannotated field cannot silently change representation while nobody is looking.
/// </para>
/// <para>
/// The baseline is a one-directional ratchet and its entries are evidence, not exemptions: each records
/// the representation the field <i>actually</i> persists as today. Annotating a field is what removes its
/// line. Nothing may be added without a rationale.
/// </para>
/// </summary>
public sealed class MongoBsonRepresentationGuardShould
{
	private const string BaselineRelativePath =
		"tests/architecture/Boundary.Tests/mongo-bson-representation-baseline.txt";

	/// <summary>
	/// A sentinel that must always be discovered. Without it, a census that finds nothing — a renamed
	/// assembly, a changed type-name suffix, an unloaded dependency — would report a clean sweep.
	/// </summary>
	private const string CensusControlType = "MongoDbOutboxDocument";

	/// <summary>CLR types whose BSON encoding is a configuration decision rather than a property of the type.</summary>
	private static readonly ImmutableHashSet<Type> AmbiguousTypes =
	[
		typeof(Guid), typeof(DateTime), typeof(DateTimeOffset), typeof(decimal)
	];

	/// <summary>One persisted field whose representation has to be decided by somebody.</summary>
	private sealed record AmbiguousField(Type Document, PropertyInfo Property, string ElementName)
	{
		/// <summary>Baseline-file identity. Stable across element renames so a rename is visible as churn.</summary>
		public string Id => Document.FullName + "::" + Property.Name;

		/// <summary>The representation the source declares, or <see langword="null"/> if it declares none.</summary>
		public BsonType? Declared =>
			Property.GetCustomAttribute<BsonRepresentationAttribute>()?.Representation;

		public override string ToString() => Id;
	}

	[Fact]
	public void Discover_the_persisted_document_types_it_claims_to_guard()
	{
		var documents = DiscoverDocumentTypes();

		documents.ShouldNotBeEmpty(
			"the census found no MongoDB document types at all, so every other assertion in this class " +
			"would pass vacuously. Check that the Mongo assemblies are loaded and that document types " +
			"still end in 'Document'.");

		documents.Select(t => t.Name).ShouldContain(
			CensusControlType,
			$"the census did not find {CensusControlType}, which is known to exist. The discovery rule " +
			"is broken, so a clean result from the other arms proves nothing.");

		Census().ShouldNotBeEmpty(
			"no ambiguous persisted field was found across the discovered document types, which cannot " +
			"be right while any document stores an instant. The property filter is broken.");
	}

	[Fact]
	public void Leave_no_ambiguous_persisted_field_undeclared_and_unbaselined()
	{
		var baseline = ReadBaseline();

		var undeclared = new List<string>();

		foreach (var (document, fields) in Census().GroupBy(f => f.Document).Select(g => (g.Key, g.ToArray())))
		{
			var serializable = TrySerialize(document, fields, out var serialized, out var failure);

			foreach (var field in fields.Where(f => f.Declared is null && !baseline.ContainsKey(f.Id)))
			{
				// Report what it persists as today, so the reader can decide between annotating it and
				// baselining it without having to run the serializer themselves.
				var actual = !serializable
					? $"unserializable: {failure}"
					: serialized.TryGetElement(field.ElementName, out var element)
						? element.Value.BsonType.ToString()
						: "<absent>";

				undeclared.Add(
					$"{field.Id} = {actual}   # {Describe(field.Property.PropertyType)} -> element '{field.ElementName}'");
			}
		}

		undeclared.Sort(StringComparer.Ordinal);

		undeclared.ShouldBeEmpty(
			$"{undeclared.Count} persisted MongoDB field(s) have a representation-ambiguous CLR type, no " +
			"[BsonRepresentation] attribute, and no baseline entry. The driver will pick an encoding for " +
			"them, and that encoding is then a storage contract nobody wrote down. Annotate the property, " +
			$"or add it to {BaselineRelativePath} with a rationale:\n  " +
			string.Join("\n  ", undeclared));
	}

	[Fact]
	public void Persist_every_declared_representation_as_the_type_it_declares()
	{
		var mismatches = new List<string>();

		// Liveness: this arm only inspects ANNOTATED properties, so it would pass by examining none of
		// them if every attribute were deleted at once — the exact change it exists to catch.
		Census().Count(f => f.Declared is not null).ShouldBeGreaterThan(
			0,
			"no persisted field declares a representation at all, so this arm asserted nothing. Either " +
			"every [BsonRepresentation] attribute has been removed, or the census can no longer read them.");

		foreach (var (document, fields) in Census().GroupBy(f => f.Document).Select(g => (g.Key, g.ToArray())))
		{
			if (!TrySerialize(document, fields, out var serialized, out var failure))
			{
				mismatches.Add($"{document.FullName}  could not be serialized at all: {failure}");
				continue;
			}

			foreach (var field in fields.Where(f => f.Declared is not null))
			{
				if (!serialized.TryGetElement(field.ElementName, out var element))
				{
					mismatches.Add(
						$"{field.Id}  declares {field.Declared} but no element '{field.ElementName}' was " +
						"written at all");
					continue;
				}

				if (element.Value.BsonType != field.Declared!.Value)
				{
					mismatches.Add(
						$"{field.Id}  declares {field.Declared} but persists as {element.Value.BsonType}");
				}
			}
		}

		mismatches.ShouldBeEmpty(
			"a [BsonRepresentation] attribute is not producing the representation it declares. An " +
			"attribute can be overridden by a registered serializer or a class map, in which case the " +
			"source reads correctly and the stored data does not match it:\n  " +
			string.Join("\n  ", mismatches.Order(StringComparer.Ordinal)));
	}

	[Fact]
	public void Persist_every_baselined_field_as_the_shape_recorded_for_it()
	{
		var drifted = ReconcileBaseline().Drifted;

		drifted.ShouldBeEmpty(
			"an unannotated MongoDB field changed its persisted representation. Existing documents are " +
			"still stored in the old shape, so reads and range filters no longer match them:\n  " +
			string.Join("\n  ", drifted));
	}

	[Fact]
	public void List_no_baselined_field_that_now_declares_its_representation()
	{
		var fixedUp = ReconcileBaseline().FixedUp;

		fixedUp.ShouldBeEmpty(
			"baselined field(s) now declare a representation explicitly, so the gap they recorded is " +
			$"closed. Delete their lines from {BaselineRelativePath} — the list only ever shrinks:\n  " +
			string.Join("\n  ", fixedUp));
	}

	[Fact]
	public void List_no_baseline_entry_that_is_no_longer_a_persisted_ambiguous_field()
	{
		var stale = ReconcileBaseline().Stale;

		stale.ShouldBeEmpty(
			"the baseline lists field(s) that are no longer a persisted ambiguous property on a Mongo " +
			$"document type. Remove the stale entries from {BaselineRelativePath}, so the file cannot " +
			"accumulate exemptions for code that no longer exists:\n  " +
			string.Join("\n  ", stale));
	}

	/// <summary>
	/// Reconciles the baseline against the live census once, so each ratchet arm is a separate
	/// <c>[Fact]</c> and one failing arm cannot hide the others behind a short-circuit.
	/// </summary>
	private static (ImmutableArray<string> Drifted, ImmutableArray<string> FixedUp, ImmutableArray<string> Stale)
		ReconcileBaseline()
	{
		var baseline = ReadBaseline();
		var drifted = new List<string>();
		var fixedUp = new List<string>();
		var unseen = new HashSet<string>(baseline.Keys, StringComparer.Ordinal);

		foreach (var (document, fields) in Census().GroupBy(f => f.Document).Select(g => (g.Key, g.ToArray())))
		{
			var serializable = TrySerialize(document, fields, out var serialized, out _);

			foreach (var field in fields.Where(f => baseline.ContainsKey(f.Id)))
			{
				_ = unseen.Remove(field.Id);

				if (field.Declared is not null)
				{
					fixedUp.Add(field.Id);
					continue;
				}

				if (!serializable)
				{
					continue; // reported by the declared-representation arm
				}

				var actual = serialized.TryGetElement(field.ElementName, out var element)
					? element.Value.BsonType.ToString()
					: "<absent>";

				if (!string.Equals(actual, baseline[field.Id], StringComparison.Ordinal))
				{
					drifted.Add(
						$"{field.Id}  was recorded as {baseline[field.Id]} and now persists as {actual}");
				}
			}
		}

		return (
			[.. drifted.Order(StringComparer.Ordinal)],
			[.. fixedUp.Order(StringComparer.Ordinal)],
			[.. unseen.Order(StringComparer.Ordinal)]);
	}

	// ---------------------------------------------------------------------------------------------
	// Census
	// ---------------------------------------------------------------------------------------------

	private static ImmutableArray<Type> DiscoverDocumentTypes() =>
		[.. AppDomain.CurrentDomain.GetAssemblies()
			.Where(a => a.GetName().Name is { } n
				&& n.StartsWith("Excalibur.", StringComparison.Ordinal)
				&& n.Contains("Mongo", StringComparison.OrdinalIgnoreCase))
			.SelectMany(SafeGetTypes)
			.Where(t => t.IsClass
				&& !t.IsAbstract
				&& !t.ContainsGenericParameters
				&& t.Name.EndsWith("Document", StringComparison.Ordinal))
			.OrderBy(t => t.FullName, StringComparer.Ordinal)];

	private static ImmutableArray<AmbiguousField> Census() =>
		[.. DiscoverDocumentTypes()
			.SelectMany(t => t
				.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(p => p.CanRead
					&& p.CanWrite
					&& p.GetCustomAttribute<BsonIgnoreAttribute>() is null
					&& AmbiguousTypes.Contains(Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType))
				.Select(p => new AmbiguousField(t, p, ElementNameOf(p))))
			.OrderBy(f => f.Id, StringComparer.Ordinal)];

	private static string ElementNameOf(PropertyInfo property)
	{
		if (property.GetCustomAttribute<BsonIdAttribute>() is not null)
		{
			return "_id";
		}

		return property.GetCustomAttribute<BsonElementAttribute>()?.ElementName is { Length: > 0 } named
			? named
			: property.Name;
	}

	private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
	{
		try
		{
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException ex)
		{
			// A partially-loadable assembly still yields its resolvable types. Returning them is
			// enforcing: a type we cannot see is a type we cannot clear, and the control test above
			// fails if the loss is wide enough to empty the census.
			return ex.Types.Where(t => t is not null)!;
		}
	}

	// ---------------------------------------------------------------------------------------------
	// Round-trip
	// ---------------------------------------------------------------------------------------------

	/// <summary>
	/// Serializes a document with every ambiguous field set to a distinctive non-default value, so a field
	/// that would be omitted at its default (<c>[BsonIgnoreIfNull]</c>, <c>[BsonIgnoreIfDefault]</c>) is
	/// still written and can be inspected.
	/// </summary>
	private static bool TrySerialize(
		Type document,
		IReadOnlyCollection<AmbiguousField> fields,
		out BsonDocument serialized,
		out string failure)
	{
		serialized = [];

		try
		{
			var instance = Activator.CreateInstance(document, nonPublic: true);
			if (instance is null)
			{
				failure = "Activator.CreateInstance returned null";
				return false;
			}

			foreach (var field in fields)
			{
				field.Property.SetValue(instance, ProbeValueFor(field.Property.PropertyType));
			}

			serialized = instance.ToBsonDocument(document);
			failure = string.Empty;
			return true;
		}
		catch (Exception ex) when (ex is not Xunit.Sdk.XunitException)
		{
			// A serialization throw is itself a finding — most commonly a Guid whose representation was
			// never configured, which MongoDB.Driver 3.x refuses to encode rather than guessing.
			failure = ex.GetBaseException().Message;
			return false;
		}
	}

	private static object ProbeValueFor(Type propertyType)
	{
		var underlying = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

		if (underlying == typeof(Guid))
		{
			return Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
		}

		if (underlying == typeof(DateTime))
		{
			return new DateTime(2026, 8, 26, 12, 34, 56, DateTimeKind.Utc);
		}

		if (underlying == typeof(DateTimeOffset))
		{
			return new DateTimeOffset(2026, 8, 26, 12, 34, 56, TimeSpan.Zero);
		}

		if (underlying == typeof(decimal))
		{
			return 12345.6789m;
		}

		throw new InvalidOperationException(
			$"no probe value for {underlying}. It was admitted to the census by {nameof(AmbiguousTypes)} " +
			"but has no representative value, so the two lists have drifted apart.");
	}

	private static string Describe(Type type) =>
		Nullable.GetUnderlyingType(type) is { } underlying
			? underlying.Name + "?"
			: type.Name;

	// ---------------------------------------------------------------------------------------------
	// Baseline
	// ---------------------------------------------------------------------------------------------

	/// <summary>Reads the ratchet file as <c>id -&gt; recorded BsonType</c>, ignoring blanks and comments.</summary>
	private static ImmutableDictionary<string, string> ReadBaseline()
	{
		var path = Path.Combine(
			TestHelpers.GetRepositoryRoot(),
			BaselineRelativePath.Replace('/', Path.DirectorySeparatorChar));

		File.Exists(path).ShouldBeTrue(
			$"the baseline file is missing at {BaselineRelativePath}. Without it this guard cannot tell " +
			"a known gap from a new one, so it must fail rather than pass.");

		var entries = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);

		foreach (var raw in File.ReadAllLines(path))
		{
			var line = raw.Trim();
			if (line.Length == 0 || line.StartsWith('#'))
			{
				continue;
			}

			var separator = line.IndexOf('=', StringComparison.Ordinal);
			separator.ShouldBeGreaterThan(
				0,
				$"malformed baseline line in {BaselineRelativePath}, expected '<Type>::<Property> = " +
				$"<BsonType>':\n  {raw}");

			var id = line[..separator].Trim();
			var recorded = line[(separator + 1)..].Trim();

			// A trailing rationale after '#' documents the entry without becoming part of the value.
			var comment = recorded.IndexOf('#', StringComparison.Ordinal);
			if (comment >= 0)
			{
				recorded = recorded[..comment].Trim();
			}

			recorded.ShouldNotBeEmpty(
				string.Create(
					CultureInfo.InvariantCulture,
					$"baseline entry '{id}' records no representation. An entry is evidence of what the " +
					$"field persists as today, not a bare exemption."));

			entries[id] = recorded;
		}

		return entries.ToImmutable();
	}
}
