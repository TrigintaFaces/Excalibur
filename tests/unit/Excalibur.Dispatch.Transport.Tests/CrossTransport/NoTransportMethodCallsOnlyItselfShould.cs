// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Transport.AzureServiceBus;
using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.CrossTransport;

/// <summary>
/// No public extension method in a transport package may call itself.
/// </summary>
/// <remarks>
/// <para>
/// Four shipped Azure Storage Queue builder knobs -- <c>VisibilityTimeout</c>,
/// <c>MaxConcurrentMessages</c>, <c>PollingInterval</c> and <c>EnableDeadLetterQueue</c> -- each cast the
/// builder to its concrete type and called a same-named method on it. The concrete builder declared no
/// such method, so overload resolution bound each call straight back to the extension method itself.
/// Calling any of them recursed until the process died with a stack overflow, which .NET cannot catch.
/// </para>
/// <para>
/// The shape compiles cleanly and reads as ordinary delegation, so only a call reveals it -- and the call
/// ends the process, which is why a full test suite could never have found it. This scan reads the
/// compiled method bodies instead, so it detects the defect without executing it.
/// </para>
/// <para>
/// Non-vacuity is structural rather than by mutation: <see cref="RecursesForever"/> below genuinely calls
/// itself, and the same detector must flag it. If the detector ever stops working, that arm fails first.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class NoTransportMethodCallsOnlyItselfShould
{
	private static readonly Assembly[] TransportAssemblies =
	[
		typeof(AzureStorageQueueTransportOptions).Assembly,
		typeof(KafkaMessageBus).Assembly,
	];

	[Fact]
	public void FlagAMethodThatCallsItself()
	{
		// The detector's positive control. It never runs the method -- it reads its body.
		var control = typeof(NoTransportMethodCallsOnlyItselfShould)
			.GetMethod(nameof(RecursesForever), BindingFlags.NonPublic | BindingFlags.Static)!;

		// The control carries an argument guard before the self-call, exactly as the four shipped knobs did,
		// so it also proves the predicate is not defeated by a preceding call.
		CallsItself(control).ShouldBeTrue(
			"the detector cannot recognise a method that calls itself, so any clean result it reports "
			+ "below is meaningless");
	}

	[Fact]
	public void FindNoSuchMethodInTheTransportPackages()
	{
		var examined = 0;
		var offenders = new List<string>();

		foreach (var method in TransportAssemblies.SelectMany(PublicMethods))
		{
			examined++;
			if (CallsItself(method))
			{
				offenders.Add($"{method.DeclaringType!.FullName}.{method.Name}");
			}
		}

		// Liveness: the scan must actually have read method bodies. A query matching nothing reports the
		// same clean result as a healthy tree.
		examined.ShouldBeGreaterThan(10,
			$"only {examined} extension methods were examined, so the scan is not reading the transport "
			+ "assemblies");

		offenders.ShouldBeEmpty(
			"these extension methods call themselves, so calling one recurses until the process dies with a "
			+ "stack overflow: " + string.Join(", ", offenders));
	}

	/// <summary>
	/// The assembly's public extension methods. A forwarding extension method has no legitimate reason to
	/// call itself -- unlike an ordinary method, where direct recursion is a normal technique -- so this is
	/// the population where a self-call is always the defect.
	/// </summary>
	private static IEnumerable<MethodInfo> PublicMethods(Assembly assembly) =>
		assembly.GetExportedTypes()
			.Where(t => t is { IsAbstract: true, IsSealed: true })
			.SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
			.Where(m => m.IsDefined(typeof(ExtensionAttribute), inherit: false) && m.GetMethodBody() is not null);

	/// <summary>
	/// Whether this method calls itself.
	/// </summary>
	/// <remarks>
	/// Reads the IL for <c>call</c> (0x28) and <c>callvirt</c> (0x6F) and resolves each token. The
	/// population above is extension methods only, where a self-call is never intended: the four Storage
	/// Queue knobs each also called an argument guard first, so a stricter "calls nothing but itself"
	/// predicate would have missed every one of them.
	/// </remarks>
	private static bool CallsItself(MethodInfo method)
	{
		var il = method.GetMethodBody()?.GetILAsByteArray();
		if (il is null)
		{
			return false;
		}

		// The two call opcodes are single-byte and carry a 4-byte metadata token. Scanning for the byte
		// pattern can in principle land inside an operand, but a false positive would have to resolve to
		// this exact method to be reported, which makes a spurious hit vanishingly unlikely. The control
		// arm above proves the detector still fires on a real one.
		for (var i = 0; i + 4 < il.Length; i++)
		{
			if (il[i] is not (0x28 or 0x6F))
			{
				continue;
			}

			var token = BitConverter.ToInt32(il, i + 1);
			MethodBase? target;
			try
			{
				target = method.Module.ResolveMethod(token);
			}
			catch (ArgumentException)
			{
				continue;
			}

			if (target is not null && target.MetadataToken == method.MetadataToken
				&& target.Module == method.Module)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>The detector's positive control: a method that calls itself, behind an argument guard.</summary>
	private static int RecursesForever(int value)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(value);
		return RecursesForever(value);
	}
}
