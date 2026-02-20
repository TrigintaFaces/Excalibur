// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Patterns.ClaimCheck;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

/// <summary>
/// Verifies that <c>AddClaimCheck()</c> properly registers
/// <see cref="ClaimCheckOptionsValidator"/> as an <see cref="IValidateOptions{TOptions}"/>
/// and that ValidateOnStart is wired up.
/// Sprint 563 S563.55: ValidateOnStart registration tests.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "ClaimCheck")]
public sealed class ClaimCheckValidateOnStartRegistrationShould
{
	[Fact]
	public void RegisterClaimCheckOptionsValidator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddClaimCheck<FakeClaimCheckProvider>();

		// Assert
		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<ClaimCheckOptions>>();
		validators.ShouldNotBeEmpty("AddClaimCheck should register IValidateOptions<ClaimCheckOptions>");
		validators.ShouldContain(v => v is ClaimCheckOptionsValidator);
	}

	[Fact]
	public void ValidOptions_ResolveSuccessfully()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddClaimCheck<FakeClaimCheckProvider>(options =>
		{
			options.PayloadThreshold = 256 * 1024;
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<ClaimCheckOptions>>();
		var value = optionsMonitor.CurrentValue;

		// Assert
		value.PayloadThreshold.ShouldBe(256 * 1024);
	}

	[Fact]
	public void InvalidOptions_ThrowsOnResolve()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddClaimCheck<FakeClaimCheckProvider>(options =>
		{
			options.PayloadThreshold = 0; // Invalid: must be > 0
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<ClaimCheckOptions>>();

		// Assert - accessing the option triggers validation and throws
		_ = Should.Throw<OptionsValidationException>(() => optionsMonitor.CurrentValue);
	}

	[Fact]
	public void DuplicateRegistrations_DoNotDuplicateValidator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act - register twice
		services.AddClaimCheck<FakeClaimCheckProvider>();
		services.AddClaimCheck<FakeClaimCheckProvider>();

		// Assert - TryAddEnumerable prevents duplicate validators
		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<ClaimCheckOptions>>()
			.Where(v => v is ClaimCheckOptionsValidator)
			.ToList();
		validators.Count.ShouldBe(1, "TryAddEnumerable should prevent duplicate ClaimCheckOptionsValidator registrations");
	}

	private sealed class FakeClaimCheckProvider : IClaimCheckProvider
	{
		public Task<ClaimCheckReference> StoreAsync(byte[] payload, CancellationToken cancellationToken, ClaimCheckMetadata? metadata = null)
			=> Task.FromResult(new ClaimCheckReference { Id = "fake-claim" });

		public Task<byte[]> RetrieveAsync(ClaimCheckReference reference, CancellationToken cancellationToken = default)
			=> Task.FromResult(Array.Empty<byte>());

		public Task<bool> DeleteAsync(ClaimCheckReference reference, CancellationToken cancellationToken = default)
			=> Task.FromResult(true);

		public bool ShouldUseClaimCheck(byte[] payload) => payload.Length > 256 * 1024;
	}
}
