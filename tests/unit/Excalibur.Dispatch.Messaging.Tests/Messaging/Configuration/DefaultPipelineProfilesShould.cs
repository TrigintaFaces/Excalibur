// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Middleware.Auth;
using Excalibur.Dispatch.Middleware.Versioning;

namespace Excalibur.Dispatch.Tests.Messaging.Configuration;

/// <summary>
/// Unit tests for <see cref="DefaultPipelineProfiles"/> public static class.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch")]
[Trait(TraitNames.Feature, TestFeatures.Configuration)]
public sealed class DefaultPipelineProfilesShould
{
	[Fact]
	public void BePublicAndStatic()
	{
		// Assert
		typeof(DefaultPipelineProfiles).IsPublic.ShouldBeTrue();
		typeof(DefaultPipelineProfiles).IsAbstract.ShouldBeTrue(); // static classes are abstract and sealed
		typeof(DefaultPipelineProfiles).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultProfileName()
	{
		// Assert
		DefaultPipelineProfiles.Default.ShouldBe("default");
	}

	[Fact]
	public void HaveStrictProfileName()
	{
		// Assert
		DefaultPipelineProfiles.Strict.ShouldBe("strict");
	}

	[Fact]
	public void HaveInternalEventProfileName()
	{
		// Assert
		DefaultPipelineProfiles.InternalEvent.ShouldBe("internal-event");
	}

	[Fact]
	public void HaveBatchProfileName()
	{
		// Assert
		DefaultPipelineProfiles.Batch.ShouldBe("batch");
	}

	[Fact]
	public void HaveDirectProfileName()
	{
		// Assert
		DefaultPipelineProfiles.Direct.ShouldBe("direct");
	}

	[Fact]
	public void HaveAllProfileNamesNotEmpty()
	{
		// Assert
		DefaultPipelineProfiles.Default.ShouldNotBeNullOrWhiteSpace();
		DefaultPipelineProfiles.Strict.ShouldNotBeNullOrWhiteSpace();
		DefaultPipelineProfiles.InternalEvent.ShouldNotBeNullOrWhiteSpace();
		DefaultPipelineProfiles.Batch.ShouldNotBeNullOrWhiteSpace();
		DefaultPipelineProfiles.Direct.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void HaveDistinctProfileNames()
	{
		// Arrange
		var names = new[]
		{
			DefaultPipelineProfiles.Default,
			DefaultPipelineProfiles.Strict,
			DefaultPipelineProfiles.InternalEvent,
			DefaultPipelineProfiles.Batch,
			DefaultPipelineProfiles.Direct,
		};

		// Assert
		names.Distinct().Count().ShouldBe(names.Length);
	}

	[Fact]
	public void CreateDefaultProfileWithMiddleware()
	{
		// Act
		var profile = DefaultPipelineProfiles.CreateDefaultProfile();

		// Assert
		profile.ShouldNotBeNull();
		profile.Name.ShouldBe(DefaultPipelineProfiles.Default);
		profile.MiddlewareEntries.ShouldNotBeEmpty();
	}

	[Fact]
	public void CreateStrictProfileWithSecurityMiddleware()
	{
		// Act
		var profile = DefaultPipelineProfiles.CreateStrictProfile();

		// Assert
		profile.ShouldNotBeNull();
		profile.Name.ShouldBe(DefaultPipelineProfiles.Strict);
		profile.MiddlewareEntries.ShouldNotBeEmpty();
	}

	[Fact]
	public void CreateInternalEventProfileWithLightweightMiddleware()
	{
		// Act
		var profile = DefaultPipelineProfiles.CreateInternalEventProfile();

		// Assert
		profile.ShouldNotBeNull();
		profile.Name.ShouldBe(DefaultPipelineProfiles.InternalEvent);
	}

	[Fact]
	public void CreateBatchProfileForBatchProcessing()
	{
		// Act
		var profile = DefaultPipelineProfiles.CreateBatchProfile();

		// Assert
		profile.ShouldNotBeNull();
		profile.Name.ShouldBe(DefaultPipelineProfiles.Batch);
	}

	[Fact]
	public void CreateDirectProfileWithZeroMiddleware()
	{
		// Act
		var profile = DefaultPipelineProfiles.CreateDirectProfile();

		// Assert
		profile.ShouldNotBeNull();
		profile.Name.ShouldBe(DefaultPipelineProfiles.Direct);
		profile.MiddlewareEntries.ShouldBeEmpty(); // Direct profile has zero middleware for max throughput
	}

	[Fact]
	public void RegisterDefaultProfilesWithRegistry()
	{
		// Arrange
		var registry = new PipelineProfileRegistry();

		// Act
		DefaultPipelineProfiles.RegisterDefaultProfiles(registry);

		// Assert
		registry.GetProfile(DefaultPipelineProfiles.Default).ShouldNotBeNull();
		registry.GetProfile(DefaultPipelineProfiles.Strict).ShouldNotBeNull();
		registry.GetProfile(DefaultPipelineProfiles.InternalEvent).ShouldNotBeNull();
		registry.GetProfile(DefaultPipelineProfiles.Batch).ShouldNotBeNull();
		registry.GetProfile(DefaultPipelineProfiles.Direct).ShouldNotBeNull();
	}

	[Fact]
	public void ThrowWhenRegisterWithNullRegistry()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			DefaultPipelineProfiles.RegisterDefaultProfiles(null!));
	}

	// --- Default-profile authorization bypass fix (6gfvtw) ---
	// AuthorizationMiddleware depends on consumer-supplied authorization services. Profile
	// materialization null-skips a middleware whose services are unregistered (fail-open), so
	// leaving auth in the Default profile silently no-ops when a consumer selects "Default"
	// without wiring auth — a silent authorization bypass. Auth must NOT be in Default; it is
	// opt-in via the Strict profile.

	[Fact]
	public void NotIncludeAuthorizationMiddlewareInDefaultProfile()
	{
		// Act
		var profile = DefaultPipelineProfiles.CreateDefaultProfile();

		// Assert — the pre-fix Default profile included AuthorizationMiddleware (silent-bypass risk).
		profile.MiddlewareEntries.Select(e => e.MiddlewareType).ShouldNotContain(typeof(AuthorizationMiddleware));
	}

	[Fact]
	public void IncludeContractVersionCheckMiddlewareInDefaultProfile()
	{
		// Act
		var profile = DefaultPipelineProfiles.CreateDefaultProfile();

		// Assert — the advertised contract-version control is wired into the Default pipeline
		// (backed by a default IContractVersionService so it is not silently inert).
		profile.MiddlewareEntries.Select(e => e.MiddlewareType).ShouldContain(typeof(ContractVersionCheckMiddleware));
	}

	[Fact]
	public void RelocateAuthorizationMiddlewareToStrictProfile()
	{
		// Act
		var profile = DefaultPipelineProfiles.CreateStrictProfile();

		// Assert — auth is not deleted, only moved to the opt-in Strict profile the consumer
		// deliberately selects (so removing it from Default does not drop the control entirely).
		profile.MiddlewareEntries.Select(e => e.MiddlewareType).ShouldContain(typeof(AuthorizationMiddleware));
	}
}