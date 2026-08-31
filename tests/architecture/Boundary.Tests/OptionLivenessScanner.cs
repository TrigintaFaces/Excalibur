// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Boundary.Tests;

/// <summary>
/// Metadata + IL census answering one question per public option property: does any shipped code
/// <b>read</b> it?
/// <para>
/// This is the <i>liveness</i> half of option honesty. Existing guards prove an option EXISTS (it is
/// in the public API baseline, it binds from configuration, a unit test sets it and reads it back).
/// None of those can fail when the option is wired to nothing. A settable public option whose getter
/// is called from nowhere in the shipped assemblies is a documented lie: a consumer can set it and
/// the framework provably cannot observe the value.
/// </para>
/// <para>
/// The instrument is IL, not text. A read of <c>options.Foo</c> compiles to a call to <c>get_Foo</c>
/// on the declaring type, so the census resolves call targets through the metadata tables rather than
/// matching identifiers. That is immune to the two failure modes a source grep has here: it cannot be
/// defeated by C# 12 primary-constructor declaration shapes, and it cannot be satisfied by an
/// unrelated type that happens to declare a property of the same name.
/// </para>
/// <para>
/// Deliberately conservative in one direction: every ambiguity resolves toward "read", so the census
/// can under-report an inert option but never accuse a live one. The known blind spots are listed on
/// <see cref="ScanResult"/>.
/// </para>
/// </summary>
internal static class OptionLivenessScanner
{
	/// <summary>One public settable property on one public <c>*Options</c> type.</summary>
	internal sealed record OptionProperty(string AssemblyName, string DeclaringType, string PropertyName)
	{
		/// <summary>Metadata identity of the getter a read would call.</summary>
		public string GetterId => DeclaringType + "::get_" + PropertyName;

		/// <summary>Baseline-file identity.</summary>
		public string Id => DeclaringType + "::" + PropertyName;

		public override string ToString() => Id;
	}

	/// <summary>
	/// Outcome of one census.
	/// <para>
	/// <b>What a result in <see cref="Inert"/> proves:</b> no method in any scanned assembly, outside
	/// the property's own declaring type, calls the property's getter. The value cannot reach any
	/// behaviour in the shipped code.
	/// </para>
	/// <para>
	/// <b>What it does NOT prove, and the blind spots that follow:</b> (1) a property read on a code
	/// path nothing reaches is counted live — this census is call-site presence, not reachability;
	/// (2) a property read only through reflection or a configuration binder leaves no call site, so it
	/// would be reported inert — a genuine case belongs in the baseline with that rationale; (3) a call
	/// whose receiver is an un-resolvable generic instantiation is credited to every same-named getter,
	/// which can mask an inert one — <see cref="ScanResult.LiveOnlyViaUnresolvedReceiver"/> sizes that
	/// residual so it is a measured quantity rather than an open-ended one.
	/// </para>
	/// <para>
	/// <b>Closed:</b> a read performed by the option's OWN validator or post-configurer no longer counts
	/// as liveness. Validating a value the framework then never observes is exactly the lie this census
	/// exists to catch, so crediting the validator's read certified inertness as liveness. The exclusion
	/// is bound to the options type in the plumbing interface's generic argument, so a validator for A
	/// that reads B still counts as a genuine read of B.
	/// </para>
	/// </summary>
	internal sealed record ScanResult(
		ImmutableArray<OptionProperty> Population,
		ImmutableArray<OptionProperty> Inert,
		int AssembliesScanned,
		int MethodBodiesWalked,
		ImmutableArray<string> UnreadableAssemblies,
		ImmutableArray<string> MalformedMethodBodies,
		ImmutableArray<OptionProperty> LiveOnlyViaUnresolvedReceiver);

	/// <summary>
	/// The options-plumbing interfaces whose implementations read a value without that value reaching
	/// any behaviour. A read from one of these, on the options type named in its generic argument, is
	/// not evidence of liveness.
	/// </summary>
	private static readonly string[] PlumbingInterfaces =
	[
		"Microsoft.Extensions.Options.IValidateOptions",
		"Microsoft.Extensions.Options.IPostConfigureOptions",
	];

	/// <summary>
	/// Runs the census over <paramref name="assemblyPaths"/>. The same set supplies both the option
	/// population and the call sites, so a scan of one assembly asks "does this assembly read its own
	/// options" and a scan of the shipped set asks "does the framework read them".
	/// </summary>
	/// <param name="assemblyPaths">The assemblies supplying both the population and the call sites.</param>
	/// <param name="excludeOwnPlumbing">
	/// When <see langword="true"/> (the contract), a read performed by the option's own validator or
	/// post-configurer is not liveness. <see langword="false"/> reproduces the pre-correction census and
	/// exists so the size of that blind spot stays a measured number rather than a remembered one.
	/// </param>
	internal static ScanResult Scan(IEnumerable<string> assemblyPaths, bool excludeOwnPlumbing = true)
	{
		var population = new List<(OptionProperty Property, List<string> GetterAliases)>();

		// getterId -> the set of types whose methods call it.
		var callers = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

		// Getter names reached through a receiver we could not resolve to a named type.
		var unresolvedGetterNames = new HashSet<string>(StringComparer.Ordinal);

		// callerType -> the options types it validates or post-configures.
		var plumbingFor = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

		var unreadable = new List<string>();
		var malformed = new List<string>();
		var assemblies = 0;
		var bodies = 0;

		foreach (var path in assemblyPaths)
		{
			try
			{
				using var stream = File.OpenRead(path);
				using var pe = new PEReader(stream);

				if (!pe.HasMetadata)
				{
					continue;
				}

				var md = pe.GetMetadataReader();
				assemblies++;

				var assemblyName = Path.GetFileNameWithoutExtension(path);
				var methodOwner = BuildMethodOwnerMap(md);

				CollectOptionProperties(md, assemblyName, population);
				CollectPlumbingTypes(md, plumbingFor);
				bodies += CollectCalls(pe, md, methodOwner, callers, unresolvedGetterNames, malformed);
			}
			catch (Exception ex) when (ex is BadImageFormatException or IOException or InvalidOperationException)
			{
				// Never a silent skip: an assembly that could not be read is reported, and the guard
				// refuses rather than reporting a clean census over a partial corpus.
				unreadable.Add(Path.GetFileName(path) + ": " + ex.GetType().Name + " " + ex.Message);
			}
		}

		if (!excludeOwnPlumbing)
		{
			plumbingFor.Clear();
		}

		var inert = population
			.Where(entry => !IsRead(entry.Property, entry.GetterAliases, callers, unresolvedGetterNames, plumbingFor))
			.Select(entry => entry.Property)
			.OrderBy(p => p.Id, StringComparer.Ordinal)
			.ToImmutableArray();

		// Blind spot (3), sized: properties whose ONLY evidence of liveness is a call through a receiver
		// the walker could not resolve. Each is a maybe, not a read.
		var unresolvedOnly = population
			.Where(entry => unresolvedGetterNames.Contains("get_" + entry.Property.PropertyName)
				&& !HasNamedReader(entry.Property, entry.GetterAliases, callers, plumbingFor))
			.Select(entry => entry.Property)
			.OrderBy(p => p.Id, StringComparer.Ordinal)
			.ToImmutableArray();

		return new ScanResult(
			population.Select(e => e.Property).OrderBy(p => p.Id, StringComparer.Ordinal).ToImmutableArray(),
			inert,
			assemblies,
			bodies,
			unreadable.ToImmutableArray(),
			malformed.ToImmutableArray(),
			unresolvedOnly);
	}

	private static bool IsRead(
		OptionProperty property,
		List<string> getterAliases,
		Dictionary<string, HashSet<string>> callers,
		HashSet<string> unresolvedGetterNames,
		Dictionary<string, HashSet<string>> plumbingFor)
	{
		// A call whose receiver type could not be resolved (a generic instantiation) is credited to
		// every getter of that name. Conservative: it can hide an inert option, never invent one.
		if (unresolvedGetterNames.Contains("get_" + property.PropertyName))
		{
			return true;
		}

		return HasNamedReader(property, getterAliases, callers, plumbingFor);
	}

	/// <summary>
	/// True when some named type outside the option's own declaration, and outside its own validation
	/// plumbing, calls the getter.
	/// </summary>
	private static bool HasNamedReader(
		OptionProperty property,
		List<string> getterAliases,
		Dictionary<string, HashSet<string>> callers,
		Dictionary<string, HashSet<string>> plumbingFor)
	{
		foreach (var getterId in getterAliases.Prepend(property.GetterId))
		{
			if (!callers.TryGetValue(getterId, out var callingTypes))
			{
				continue;
			}

			// Reads from the option type's own members do not count. They are either compiler
			// synthesised (a record's copy constructor, Equals, GetHashCode, PrintMembers and ToString
			// all read every property, which would make every record-shaped options type look live) or
			// they are one property feeding a sibling property on the same object, which moves the
			// question rather than answering it.
			//
			// Neither does a read by the option's own validator or post-configurer: the value goes into
			// a check and comes back out as a pass or a fail, never into behaviour a consumer can
			// observe. Crediting that read is how a validated-but-unobserved option passed as live.
			if (callingTypes.Any(t =>
				!string.Equals(t, property.DeclaringType, StringComparison.Ordinal)
				&& !IsOwnPlumbing(t, property.DeclaringType, plumbingFor)))
			{
				return true;
			}
		}

		return false;
	}

	private static bool IsOwnPlumbing(
		string callerType,
		string declaringType,
		Dictionary<string, HashSet<string>> plumbingFor)
	{
		if (plumbingFor.TryGetValue(callerType, out var direct) && direct.Contains(declaringType))
		{
			return true;
		}

		// A lambda inside a validator compiles to a nested closure class, so the caller may be
		// Ns.FooValidator+[closure] rather than Ns.FooValidator. Step out to the IMMEDIATE enclosing
		// type — the last separator, not the first. Taking the first would attribute a closure nested
		// several levels down to an unrelated outermost type, and this exclusion may only ever err
		// toward counting a read, never toward accusing a live option.
		var nest = callerType.LastIndexOf('+');

		return nest >= 0
			&& plumbingFor.TryGetValue(callerType[..nest], out var enclosing)
			&& enclosing.Contains(declaringType);
	}

	/// <summary>
	/// Records, for every type implementing one of <see cref="PlumbingInterfaces"/>, the options type
	/// named in that interface's generic argument.
	/// </summary>
	private static void CollectPlumbingTypes(MetadataReader md, Dictionary<string, HashSet<string>> plumbingFor)
	{
		foreach (var typeHandle in md.TypeDefinitions)
		{
			var type = md.GetTypeDefinition(typeHandle);
			string? typeName = null;

			foreach (var implHandle in type.GetInterfaceImplementations())
			{
				var optionsType = PlumbedOptionsType(md, md.GetInterfaceImplementation(implHandle).Interface);
				if (optionsType is null)
				{
					continue;
				}

				typeName ??= FullName(md, typeHandle);
				if (!plumbingFor.TryGetValue(typeName, out var set))
				{
					set = new HashSet<string>(StringComparer.Ordinal);
					plumbingFor[typeName] = set;
				}

				_ = set.Add(optionsType);
			}
		}
	}

	/// <summary>
	/// Decodes <c>IValidateOptions&lt;TOptions&gt;</c> / <c>IPostConfigureOptions&lt;TOptions&gt;</c> from
	/// an interface-implementation handle and returns <c>TOptions</c>, or <see langword="null"/> when the
	/// handle is not one of those. Reads the TypeSpec blob directly: the generic argument is the whole
	/// point of the check, and an undecoded interface name does not carry it.
	/// </summary>
	private static string? PlumbedOptionsType(MetadataReader md, EntityHandle interfaceHandle)
	{
		if (interfaceHandle.Kind != HandleKind.TypeSpecification)
		{
			return null;
		}

		const byte ElementTypeGenericInst = 0x15;
		const byte ElementTypeValueType = 0x11;
		const byte ElementTypeClass = 0x12;

		try
		{
			var blob = md.GetBlobReader(md.GetTypeSpecification((TypeSpecificationHandle)interfaceHandle).Signature);

			if (blob.ReadByte() != ElementTypeGenericInst)
			{
				return null;
			}

			if (blob.ReadByte() is not (ElementTypeClass or ElementTypeValueType))
			{
				return null;
			}

			var openName = TypeHandleName(md, blob.ReadTypeHandle());
			if (openName is null || !PlumbingInterfaces.Any(i => openName.StartsWith(i, StringComparison.Ordinal)))
			{
				return null;
			}

			if (blob.ReadCompressedInteger() != 1)
			{
				return null;
			}

			return blob.ReadByte() is ElementTypeClass or ElementTypeValueType
				? TypeHandleName(md, blob.ReadTypeHandle())
				: null;
		}
		catch (BadImageFormatException)
		{
			// An undecodable signature is not evidence of plumbing. Falling through to "not plumbing"
			// keeps any read it performs counting as a genuine read, which is the safe direction.
			return null;
		}
	}

	// ---- population ---------------------------------------------------------------------------

	private static void CollectOptionProperties(
		MetadataReader md,
		string assemblyName,
		List<(OptionProperty, List<string>)> population)
	{
		foreach (var typeHandle in md.TypeDefinitions)
		{
			var type = md.GetTypeDefinition(typeHandle);

			// Top-level public only: the consumer-facing option surface, the type a consumer can name
			// in AddOptions<T> or bind an appsettings section to.
			if ((type.Attributes & TypeAttributes.VisibilityMask) != TypeAttributes.Public)
			{
				continue;
			}

			var name = md.GetString(type.Name);
			if (!name.EndsWith("Options", StringComparison.Ordinal))
			{
				continue;
			}

			var declaringType = FullName(md, typeHandle);
			var interfaceNames = InterfaceNames(md, type);

			foreach (var propertyHandle in type.GetProperties())
			{
				var property = md.GetPropertyDefinition(propertyHandle);
				var accessors = property.GetAccessors();

				if (accessors.Getter.IsNil || accessors.Setter.IsNil)
				{
					continue;
				}

				var getter = md.GetMethodDefinition(accessors.Getter);
				var setter = md.GetMethodDefinition(accessors.Setter);

				if (!IsPublic(getter) || !IsPublic(setter))
				{
					continue;
				}

				var propertyName = md.GetString(property.Name);
				if (propertyName is "Item")
				{
					// Indexer: not an option a consumer configures by name.
					continue;
				}

				// A consumer may hold the option through an interface the type implements, in which
				// case the read compiles to a call on the interface getter instead. Credit both.
				var aliases = interfaceNames.Select(i => i + "::get_" + propertyName).ToList();

				population.Add((new OptionProperty(assemblyName, declaringType, propertyName), aliases));
			}
		}
	}

	private static bool IsPublic(MethodDefinition method) =>
		(method.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public;

	private static List<string> InterfaceNames(MetadataReader md, TypeDefinition type)
	{
		var names = new List<string>();

		foreach (var implHandle in type.GetInterfaceImplementations())
		{
			var name = TypeHandleName(md, md.GetInterfaceImplementation(implHandle).Interface);
			if (name is not null)
			{
				names.Add(name);
			}
		}

		return names;
	}

	// ---- call sites ---------------------------------------------------------------------------

	private static Dictionary<int, string> BuildMethodOwnerMap(MetadataReader md)
	{
		var map = new Dictionary<int, string>();

		foreach (var typeHandle in md.TypeDefinitions)
		{
			var typeName = FullName(md, typeHandle);

			foreach (var methodHandle in md.GetTypeDefinition(typeHandle).GetMethods())
			{
				map[MetadataTokens.GetToken(methodHandle)] = typeName;
			}
		}

		return map;
	}

	private static int CollectCalls(
		PEReader pe,
		MetadataReader md,
		Dictionary<int, string> methodOwner,
		Dictionary<string, HashSet<string>> callers,
		HashSet<string> unresolvedGetterNames,
		List<string> malformed)
	{
		var bodies = 0;
		var tokens = new List<int>();

		foreach (var methodHandle in md.MethodDefinitions)
		{
			var method = md.GetMethodDefinition(methodHandle);
			if (method.RelativeVirtualAddress == 0)
			{
				continue;
			}

			var callerType = methodOwner.TryGetValue(MetadataTokens.GetToken(methodHandle), out var owner)
				? owner
				: "<unknown>";

			var il = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes();
			if (il is null)
			{
				continue;
			}

			bodies++;
			tokens.Clear();

			if (!TryWalkCallTokens(il, tokens))
			{
				malformed.Add(callerType + "::" + md.GetString(method.Name));
				continue;
			}

			foreach (var token in tokens)
			{
				RecordCall(md, methodOwner, token, callerType, callers, unresolvedGetterNames);
			}
		}

		return bodies;
	}

	private static void RecordCall(
		MetadataReader md,
		Dictionary<int, string> methodOwner,
		int token,
		string callerType,
		Dictionary<string, HashSet<string>> callers,
		HashSet<string> unresolvedGetterNames)
	{
		if (token == 0)
		{
			return;
		}

		EntityHandle handle;
		try
		{
			handle = MetadataTokens.EntityHandle(token);
		}
		catch (ArgumentException)
		{
			return;
		}


		string? calleeName;
		string? calleeType;

		switch (handle.Kind)
		{
			case HandleKind.MethodDefinition:
				calleeName = md.GetString(md.GetMethodDefinition((MethodDefinitionHandle)handle).Name);
				calleeType = methodOwner.TryGetValue(token, out var owner) ? owner : null;
				break;

			case HandleKind.MemberReference:
				var memberRef = md.GetMemberReference((MemberReferenceHandle)handle);
				calleeName = md.GetString(memberRef.Name);
				calleeType = TypeHandleName(md, memberRef.Parent);
				break;

			case HandleKind.MethodSpecification:
				// Generic method instantiation: unwrap to the open method and re-record.
				var spec = md.GetMethodSpecification((MethodSpecificationHandle)handle);
				RecordCall(md, methodOwner, MetadataTokens.GetToken(spec.Method), callerType, callers, unresolvedGetterNames);
				return;

			default:
				return;
		}

		if (calleeName is null || !calleeName.StartsWith("get_", StringComparison.Ordinal))
		{
			return;
		}

		if (calleeType is null)
		{
			unresolvedGetterNames.Add(calleeName);
			return;
		}

		var key = calleeType + "::" + calleeName;
		if (!callers.TryGetValue(key, out var set))
		{
			set = new HashSet<string>(StringComparer.Ordinal);
			callers[key] = set;
		}

		set.Add(callerType);
	}

	// ---- names --------------------------------------------------------------------------------

	private static string FullName(MetadataReader md, TypeDefinitionHandle handle)
	{
		var type = md.GetTypeDefinition(handle);
		var name = md.GetString(type.Name);

		if (type.IsNested)
		{
			return FullName(md, type.GetDeclaringType()) + "+" + name;
		}

		var ns = md.GetString(type.Namespace);
		return ns.Length == 0 ? name : ns + "." + name;
	}

	/// <summary>
	/// Resolves a type handle to a namespace-qualified name, or <see langword="null"/> when the handle
	/// is a generic instantiation or other construct with no single named type. A null deliberately
	/// widens the match rather than narrowing it.
	/// </summary>
	private static string? TypeHandleName(MetadataReader md, EntityHandle handle) => handle.Kind switch
	{
		HandleKind.TypeDefinition => FullName(md, (TypeDefinitionHandle)handle),
		HandleKind.TypeReference => TypeReferenceName(md, (TypeReferenceHandle)handle),
		_ => null,
	};

	private static string TypeReferenceName(MetadataReader md, TypeReferenceHandle handle)
	{
		var typeRef = md.GetTypeReference(handle);
		var name = md.GetString(typeRef.Name);

		if (typeRef.ResolutionScope.Kind == HandleKind.TypeReference)
		{
			return TypeReferenceName(md, (TypeReferenceHandle)typeRef.ResolutionScope) + "+" + name;
		}

		var ns = md.GetString(typeRef.Namespace);
		return ns.Length == 0 ? name : ns + "." + name;
	}

	// ---- IL -------------------------------------------------------------------------------------

	/// <summary>
	/// Walks a method body and collects the metadata tokens of every call-shaped instruction.
	/// <para>
	/// Returns <see langword="false"/> if the walk does not consume the body exactly. That is the
	/// walker's own correctness check: instruction lengths are the only thing this routine has to get
	/// right, and a wrong length desynchronises the stream, which then shows up as an over-run or an
	/// unknown opcode rather than as quietly wrong answers.
	/// </para>
	/// </summary>
	private static bool TryWalkCallTokens(byte[] il, List<int> tokens)
	{
		var start = tokens.Count;
		var i = 0;

		while (i < il.Length)
		{
			var op = il[i++];

			if (op == 0xFE)
			{
				if (i >= il.Length)
				{
					return false;
				}

				var op2 = il[i++];
				var extendedSize = ExtendedOperandSize(op2);
				if (extendedSize < 0 || i + extendedSize > il.Length)
				{
					return false;
				}

				// ldftn / ldvirtftn: a method reference taken without an immediate call.
				if (op2 is 0x06 or 0x07)
				{
					tokens.Add(BitConverter.ToInt32(il, i));
				}

				i += extendedSize;
				continue;
			}

			if (op == 0x45)
			{
				// switch <count> <count * int32>
				if (i + 4 > il.Length)
				{
					return false;
				}

				var count = BitConverter.ToUInt32(il, i);
				i += 4;

				var jumpTable = (long)count * 4;
				if (i + jumpTable > il.Length)
				{
					return false;
				}

				i += (int)jumpTable;
				continue;
			}

			var size = OperandSize(op);
			if (size < 0 || i + size > il.Length)
			{
				return false;
			}

			// jmp / call / callvirt / newobj
			if (op is 0x27 or 0x28 or 0x6F or 0x73)
			{
				tokens.Add(BitConverter.ToInt32(il, i));
			}

			i += size;
		}

		if (i != il.Length)
		{
			return false;
		}

		// Second, independent check on the same walk. A call-shaped instruction can only carry a
		// MethodDef, MemberRef or MethodSpec token. Anything else means the stream desynchronised
		// somewhere earlier and stayed in bounds, which the length check alone cannot see.
		for (var t = start; t < tokens.Count; t++)
		{
			var table = (uint)tokens[t] >> 24;
			if (table is not (0x06 or 0x0A or 0x2B))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>Operand byte count for a single-byte opcode; -1 for one this walker will not guess at.</summary>
	private static int OperandSize(byte op) => op switch
	{
		>= 0x0E and <= 0x13 => 1,         // ldarg.s ldarga.s starg.s ldloc.s ldloca.s stloc.s
		0x1F => 1,                        // ldc.i4.s
		>= 0x2B and <= 0x37 => 1,         // br.s .. blt.un.s
		0x21 or 0x23 => 8,                // ldc.i8 ldc.r8
		0x20 or 0x22 => 4,                // ldc.i4 ldc.r4
		>= 0x27 and <= 0x29 => 4,         // jmp call calli
		>= 0x38 and <= 0x44 => 4,         // br .. blt.un
		0x6F => 4,                        // callvirt
		>= 0x70 and <= 0x75 => 4,         // cpobj ldobj ldstr newobj castclass isinst
		0x79 => 4,                        // unbox
		>= 0x7B and <= 0x81 => 4,         // ldfld ldflda stfld ldsfld ldsflda stsfld stobj
		0x8C or 0x8D or 0x8F => 4,        // box newarr ldelema
		>= 0xA3 and <= 0xA5 => 4,         // ldelem stelem unbox.any
		0xC2 or 0xC6 or 0xD0 => 4,        // refanyval mkrefany ldtoken
		0xDD => 4,                        // leave      (0xDC is endfinally, no operand)
		0xDE => 1,                        // leave.s
		>= 0xE1 => -1,                    // reserved (0xFE is handled before this switch)
		_ => 0,
	};

	/// <summary>Operand byte count for the second byte of an <c>0xFE</c> two-byte opcode; -1 if unknown.</summary>
	private static int ExtendedOperandSize(byte op2) => op2 switch
	{
		<= 0x05 => 0,                     // arglist ceq cgt cgt.un clt clt.un
		0x06 or 0x07 => 4,                // ldftn ldvirtftn
		>= 0x09 and <= 0x0E => 2,         // ldarg ldarga starg ldloc ldloca stloc
		0x0F => 0,                        // localloc
		0x11 => 0,                        // endfilter
		0x12 => 1,                        // unaligned.
		0x13 or 0x14 => 0,                // volatile. tail.
		0x15 or 0x16 => 4,                // initobj constrained.
		0x17 or 0x18 => 0,                // cpblk initblk
		0x19 => 1,                        // no.
		0x1A => 0,                        // rethrow
		0x1C => 4,                        // sizeof
		0x1D or 0x1E => 0,                // refanytype readonly.
		_ => -1,
	};
}
