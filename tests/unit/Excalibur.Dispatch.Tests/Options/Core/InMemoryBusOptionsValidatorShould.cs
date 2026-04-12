// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Core;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Options.Core;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Configuration)]
public sealed class InMemoryBusOptionsValidatorShould
{
	private readonly InMemoryBusOptionsValidator _sut = new();

	#region Happy Path

	[Fact]
	public void Succeed_with_default_options()
	{
		var result = _sut.Validate(null, new InMemoryBusOptions());
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Succeed_with_valid_custom_options()
	{
		var result = _sut.Validate(null, new InMemoryBusOptions
		{
			MaxQueueLength = 5000,
			ProcessingDelay = TimeSpan.FromMilliseconds(100),
			PreserveOrder = false
		});
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Succeed_when_max_queue_length_is_one()
	{
		var result = _sut.Validate(null, new InMemoryBusOptions { MaxQueueLength = 1 });
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Succeed_when_processing_delay_is_zero()
	{
		var result = _sut.Validate(null, new InMemoryBusOptions { ProcessingDelay = TimeSpan.Zero });
		result.Succeeded.ShouldBeTrue();
	}

	#endregion

	#region Failure Paths

	[Fact]
	public void Fail_when_max_queue_length_is_zero()
	{
		var result = _sut.Validate(null, new InMemoryBusOptions { MaxQueueLength = 0 });
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(InMemoryBusOptions.MaxQueueLength));
	}

	[Fact]
	public void Fail_when_max_queue_length_is_negative()
	{
		var result = _sut.Validate(null, new InMemoryBusOptions { MaxQueueLength = -1 });
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(InMemoryBusOptions.MaxQueueLength));
	}

	[Fact]
	public void Fail_when_processing_delay_is_negative()
	{
		var result = _sut.Validate(null, new InMemoryBusOptions
		{
			ProcessingDelay = TimeSpan.FromMilliseconds(-1)
		});
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(InMemoryBusOptions.ProcessingDelay));
	}

	[Fact]
	public void Fail_with_multiple_errors_when_both_invalid()
	{
		var result = _sut.Validate(null, new InMemoryBusOptions
		{
			MaxQueueLength = 0,
			ProcessingDelay = TimeSpan.FromMilliseconds(-100)
		});
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(InMemoryBusOptions.MaxQueueLength));
		result.FailureMessage.ShouldContain(nameof(InMemoryBusOptions.ProcessingDelay));
	}

	#endregion

	#region Null Input

	[Fact]
	public void Throw_when_options_is_null()
	{
		Should.Throw<ArgumentNullException>(() => _sut.Validate(null, null!));
	}

	#endregion
}
