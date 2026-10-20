// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery.Registry;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Registry
{
	/// <summary>
	/// Covers the name forms the durable drains write and the refusal of an ambiguous name.
	/// </summary>
	[Trait("Category", TestCategories.Unit)]
	[Trait("Component", TestComponents.Messaging)]
	[Trait("Pattern", "Registration")]
	public sealed class MessageTypeRegistryShould
	{
		public enum NameForm
		{
			AssemblyQualified,
			FullName,
			FullNameWithAssembly,
			SimpleName,
		}

		[Fact]
		public void ResolveDeclaredHistoricalAliasesToTheExactCurrentType()
		{
			MessageTypeRegistry.RegisterType<AliasProbe>();
			MessageTypeRegistry.TryGetType("arb-registry-alias-old", out var resolved).ShouldBeTrue();
			resolved.ShouldBe(typeof(AliasProbe));
			MessageTypeRegistry.TryGetType("arb-registry-alias-current", out var current).ShouldBeTrue();
			current.ShouldBe(resolved);
		}

		[Fact]
		public void KeepCompetingAliasesPermanentlyAmbiguous()
		{
			MessageTypeRegistry.RegisterType<AliasFirst>();
			MessageTypeRegistry.RegisterType<AliasSecond>();
			MessageTypeRegistry.RegisterType<AliasFirst>();
			MessageTypeRegistry.TryGetType("arb-registry-alias-shared", out _).ShouldBeFalse();
			MessageTypeRegistry.TryGetType(typeof(AliasFirst).AssemblyQualifiedName!, out var first).ShouldBeTrue();
			first.ShouldBe(typeof(AliasFirst));
			MessageTypeRegistry.TryGetType(typeof(AliasSecond).AssemblyQualifiedName!, out var second).ShouldBeTrue();
			second.ShouldBe(typeof(AliasSecond));
		}

		[Fact]
		public void RefuseAnAliasThatConflictsWithAnotherPrimaryName()
		{
			MessageTypeRegistry.RegisterType<AliasPrimary>();
			MessageTypeRegistry.RegisterType<AliasPrimaryCompetitor>();
			MessageTypeRegistry.TryGetType("arb-registry-alias-primary", out _).ShouldBeFalse();
			MessageTypeRegistry.TryGetType(typeof(AliasPrimary).AssemblyQualifiedName!, out var primary).ShouldBeTrue();
			primary.ShouldBe(typeof(AliasPrimary));
		}

		[Fact]
		public async Task PreserveAliasCollisionRefusalAfterCompetingRegistrations()
		{
			await Task.WhenAll(Task.Run(() => MessageTypeRegistry.RegisterType<AliasFirst>()),
				Task.Run(() => MessageTypeRegistry.RegisterType<AliasSecond>()));
			MessageTypeRegistry.TryGetType("arb-registry-alias-shared", out _).ShouldBeFalse();
			MessageTypeRegistry.TryGetType("arb-registry-alias-first", out var first).ShouldBeTrue();
			first.ShouldBe(typeof(AliasFirst));
		}

		[Fact]
		public void RefuseAnUnqualifiedGenericAliasSharedByTwoConstructions()
		{
			MessageTypeRegistry.RegisterType<GenericAlias<int>>();
			MessageTypeRegistry.RegisterType<GenericAlias<string>>();
			MessageTypeRegistry.TryGetType("arb-registry-generic-alias-old", out _).ShouldBeFalse();
			MessageTypeRegistry.TryGetType(MessageNameHelper.GetName(typeof(GenericAlias<int>)), out var integer).ShouldBeTrue();
			integer.ShouldBe(typeof(GenericAlias<int>));
			MessageTypeRegistry.TryGetType(MessageNameHelper.GetName(typeof(GenericAlias<string>)), out var text).ShouldBeTrue();
			text.ShouldBe(typeof(GenericAlias<string>));
		}

		[MessageName("arb-registry-alias-current")]
		[MessageNameAlias("arb-registry-alias-old")]
		private sealed class AliasProbe : IDispatchEvent;

		[MessageName("arb-registry-alias-first")]
		[MessageNameAlias("arb-registry-alias-shared")]
		private sealed class AliasFirst : IDispatchEvent;

		[MessageName("arb-registry-alias-second")]
		[MessageNameAlias("arb-registry-alias-shared")]
		private sealed class AliasSecond : IDispatchEvent;

		[MessageName("arb-registry-alias-primary")]
		private sealed class AliasPrimary : IDispatchEvent;

		[MessageName("arb-registry-alias-competitor")]
		[MessageNameAlias("arb-registry-alias-primary")]
		private sealed class AliasPrimaryCompetitor : IDispatchEvent;

		[MessageName("arb-registry-generic-alias")]
		[MessageNameAlias("arb-registry-generic-alias-old")]
		private sealed class GenericAlias<T> : IDispatchEvent;

		[Fact]
		public void ResolveTheBareFullNameTheOutboxStages()
		{
			var type = typeof(RegistryProbeMessage);
			MessageTypeRegistry.RegisterType(type);

			// MessageOutbox stages exactly this form (`evt.GetType().FullName`), so a name it can write and the
			// registry cannot read is a message the drain can never dispatch.
			var fullName = type.FullName.ShouldNotBeNull();

			MessageTypeRegistry.TryGetType(fullName, out var resolved).ShouldBeTrue();
			resolved.ShouldBe(type);
		}

		[Theory]
		[InlineData(NameForm.AssemblyQualified)]
		[InlineData(NameForm.FullName)]
		[InlineData(NameForm.FullNameWithAssembly)]
		[InlineData(NameForm.SimpleName)]
		public void ResolveEveryNameFormTheFrameworkWrites(NameForm form)
		{
			var type = typeof(RegistryProbeMessage);
			MessageTypeRegistry.RegisterType(type);

			var name = form switch
			{
				NameForm.AssemblyQualified => type.AssemblyQualifiedName!,
				NameForm.FullName => type.FullName!,
				NameForm.FullNameWithAssembly => $"{type.FullName}, {type.Assembly.GetName().Name}",
				NameForm.SimpleName => type.Name,
				_ => throw new ArgumentOutOfRangeException(nameof(form)),
			};

			MessageTypeRegistry.TryGetType(name, out var resolved).ShouldBeTrue();
			resolved.ShouldBe(type);
		}

		[Fact]
		public void RefuseAnAmbiguousSimpleNameRatherThanPickAWinner()
		{
			var first = typeof(Contested.CollidingProbeMessage);
			var second = typeof(AlsoContested.CollidingProbeMessage);
			first.Name.ShouldBe(second.Name);

			MessageTypeRegistry.RegisterType(first);
			MessageTypeRegistry.RegisterType(second);

			// Resolving either one would be a guess, and a wrong guess deserializes the payload into a type that
			// merely shares a name -- the JSON reader fills what matches and silently defaults the rest.
			MessageTypeRegistry.TryGetType(first.Name, out var resolved).ShouldBeFalse();
			resolved.ShouldBeNull();
		}

		[Fact]
		public void KeepTheSpecificNameFormsResolvableAfterASimpleNameCollision()
		{
			var first = typeof(Contested.CollidingProbeMessage);
			var second = typeof(AlsoContested.CollidingProbeMessage);

			MessageTypeRegistry.RegisterType(first);
			MessageTypeRegistry.RegisterType(second);

			MessageTypeRegistry.TryGetType(first.FullName!, out var resolvedFirst).ShouldBeTrue();
			resolvedFirst.ShouldBe(first);
			MessageTypeRegistry.TryGetType(second.FullName!, out var resolvedSecond).ShouldBeTrue();
			resolvedSecond.ShouldBe(second);
		}

		[Fact]
		public void KeepAmbiguityStickyAcrossALaterRegistration()
		{
			var first = typeof(Contested.CollidingProbeMessage);
			var second = typeof(AlsoContested.CollidingProbeMessage);

			MessageTypeRegistry.RegisterType(first);
			MessageTypeRegistry.RegisterType(second);

			// A late-loading assembly re-registering must not get to decide the collision in its own favour.
			MessageTypeRegistry.RegisterType(first);

			MessageTypeRegistry.TryGetType(first.Name, out _).ShouldBeFalse();
		}

		[Fact]
		public void TreatRepeatedRegistrationOfTheSameTypeAsIdempotent()
		{
			var type = typeof(RegistryProbeMessage);

			MessageTypeRegistry.RegisterType(type);
			MessageTypeRegistry.RegisterType(type);
			MessageTypeRegistry.RegisterType(type);

			MessageTypeRegistry.TryGetType(type.Name, out var resolved).ShouldBeTrue();
			resolved.ShouldBe(type);
		}

		[Fact]
		public void ReportAnUnknownNameAsNotFound()
		{
			MessageTypeRegistry.TryGetType("Excalibur.Dispatch.Tests.NoSuchMessageTypeExists", out var resolved).ShouldBeFalse();
			resolved.ShouldBeNull();
		}

		[Fact]
		public void ListEachRegisteredTypeOnceDespiteFourNameForms()
		{
			var type = typeof(RegistryProbeMessage);
			MessageTypeRegistry.RegisterType(type);

			MessageTypeRegistry.GetAllMessageTypes().Count(t => t == type).ShouldBe(1);
		}

		private sealed class RegistryProbeMessage : IDispatchAction;
	}
}

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Registry.Contested
{
	internal sealed class CollidingProbeMessage : IDispatchAction;
}

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Registry.AlsoContested
{
	internal sealed class CollidingProbeMessage : IDispatchAction;
}
