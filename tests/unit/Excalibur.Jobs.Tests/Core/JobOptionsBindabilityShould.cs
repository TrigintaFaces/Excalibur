// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Runtime.CompilerServices;

using Excalibur.Jobs.Core;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Jobs.Tests.Core;

/// <summary>
/// Locks the job options types into a shape the configuration binding source generator can bind.
/// </summary>
/// <remarks>
/// <para>
/// The generator emits a <c>BindCore</c> that assigns each property directly on an already-constructed
/// instance. An init-only accessor cannot be assigned there, so the generator omits the property from
/// <c>BindCore</c> entirely and emits no diagnostic — the bound instance silently keeps its defaults
/// (an empty cron schedule, an empty job name), which reads exactly like a successful bind. A member
/// marked <c>required</c> fails more loudly, because the generator cannot construct the type at all.
/// </para>
/// <para>
/// The reflection binder tolerates both shapes, so these assertions guard the generated path
/// specifically: they are the structural precondition the generator needs, not a restatement of what
/// the reflection binder already does.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
[Trait("Feature", "Core")]
public sealed class JobOptionsBindabilityShould : UnitTestBase
{
	private sealed class TestJobOptions : JobOptions;

	private static readonly string[] JobOptionProperties =
	[
		nameof(IJobOptions.JobName),
		nameof(IJobOptions.JobGroup),
		nameof(IJobOptions.CronSchedule),
		nameof(IJobOptions.DegradedThreshold),
		nameof(IJobOptions.Disabled),
		nameof(IJobOptions.UnhealthyThreshold),
	];

	public static TheoryData<string> PropertyNames()
	{
		var data = new TheoryData<string>();
		foreach (var name in JobOptionProperties)
		{
			data.Add(name);
		}

		return data;
	}

	[Theory]
	[MemberData(nameof(PropertyNames))]
	public void ExposeASettableAccessorOnTheJobOptionsBase(string propertyName) =>
		AssertAssignableAfterConstruction(typeof(JobOptions), propertyName);

	[Theory]
	[MemberData(nameof(PropertyNames))]
	public void ExposeASettableAccessorOnTheJobOptionsContract(string propertyName) =>
		AssertAssignableAfterConstruction(typeof(IJobOptions), propertyName);

	[Fact]
	public void ConstructJobOptionsWithoutRequiredMembers()
	{
		// A `required` member makes the generator emit `new TOptions()`, which does not compile.
		foreach (var property in typeof(TestJobOptions).GetProperties())
		{
			property.GetCustomAttribute<RequiredMemberAttribute>()
				.ShouldBeNull($"{property.Name} is marked required, which the binding generator cannot satisfy.");
		}
	}

	[Fact]
	public void BindEveryPropertyFromConfiguration()
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Jobs:Test:JobName"] = "outbox-sweep",
				["Jobs:Test:JobGroup"] = "maintenance",
				["Jobs:Test:CronSchedule"] = "0 0/5 * * * ?",
				["Jobs:Test:DegradedThreshold"] = "00:02:00",
				["Jobs:Test:Disabled"] = "true",
				["Jobs:Test:UnhealthyThreshold"] = "00:07:00",
			})
			.Build();

		var options = configuration.GetSection("Jobs:Test").Get<TestJobOptions>();

		_ = options.ShouldNotBeNull();
		options.JobName.ShouldBe("outbox-sweep");
		options.JobGroup.ShouldBe("maintenance");
		options.CronSchedule.ShouldBe("0 0/5 * * * ?");
		options.DegradedThreshold.ShouldBe(TimeSpan.FromMinutes(2));
		options.Disabled.ShouldBeTrue();
		options.UnhealthyThreshold.ShouldBe(TimeSpan.FromMinutes(7));
	}

	private static void AssertAssignableAfterConstruction(Type declaringType, string propertyName)
	{
		var property = declaringType.GetProperty(propertyName);
		_ = property.ShouldNotBeNull();

		var setter = property.SetMethod;
		_ = setter.ShouldNotBeNull($"{declaringType.Name}.{propertyName} has no setter to bind through.");

		setter.ReturnParameter.GetRequiredCustomModifiers()
			.ShouldNotContain(
				typeof(IsExternalInit),
				$"{declaringType.Name}.{propertyName} is init-only, so the binding source generator omits it from BindCore and it silently keeps its default.");
	}
}
