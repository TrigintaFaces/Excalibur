// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Azure.Messaging.ServiceBus;

using Google.Cloud.PubSub.V1;

namespace Excalibur.Dispatch.Transport.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Pattern", "Regression")]
public sealed class SdkAdapterSubstitutionContractShould
{
	[Fact]
	public void PreserveEverySdkMemberSubstitutedByTheCertifiedAdapterFixtures()
	{
		AssertProxyConstructor(typeof(ServiceBusClient));
		AssertProxyConstructor(typeof(ServiceBusSender));
		AssertProxyConstructor(typeof(PublisherClient));
		typeof(PublisherClient).IsAbstract.ShouldBeTrue();
		AssertOverridable(typeof(ServiceBusClient), "CreateSender", typeof(string));
		AssertOverridable(typeof(ServiceBusClient), "DisposeAsync");
		AssertOverridable(typeof(ServiceBusSender), "SendMessageAsync", typeof(ServiceBusMessage), typeof(CancellationToken));
		AssertOverridable(typeof(ServiceBusSender), "DisposeAsync");
		AssertOverridable(typeof(PublisherClient), "PublishAsync", typeof(PubsubMessage));
		AssertOverridable(typeof(PublisherClient), "DisposeAsync");
	}

	[Fact]
	public void RejectNonvirtualAndSealedMembersEvenWhenTheirNamesMatch()
	{
		CanOverride(typeof(NonvirtualPublisher).GetMethod("DisposeAsync")!).ShouldBeFalse();
		CanOverride(typeof(SealedPublisher).GetMethod("DisposeAsync")!).ShouldBeFalse();
		CanOverride(typeof(VirtualPublisher).GetMethod("DisposeAsync")!).ShouldBeTrue();
	}

	private static void AssertProxyConstructor(Type type)
	{
		type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			.Any(ctor => ctor.GetParameters().Length == 0 && (ctor.IsPublic || ctor.IsFamily || ctor.IsFamilyOrAssembly))
			.ShouldBeTrue($"{type.FullName} must retain its proxy-accessible parameterless constructor");
	}

	private static void AssertOverridable(Type type, string name, params Type[] parameters)
	{
		var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public, parameters);
		method.ShouldNotBeNull($"{type.FullName}.{name} must preserve the exact substituted overload");
		CanOverride(method).ShouldBeTrue($"{type.FullName}.{name} must remain public, instance, virtual and nonfinal");
	}

	private static bool CanOverride(MethodInfo method) => method.IsPublic && !method.IsStatic && method.IsVirtual && !method.IsFinal;

	private class VirtualPublisher
	{
		public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}

	private sealed class SealedPublisher : VirtualPublisher
	{
		public sealed override ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}

	private sealed class NonvirtualPublisher
	{
		public ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}
}
