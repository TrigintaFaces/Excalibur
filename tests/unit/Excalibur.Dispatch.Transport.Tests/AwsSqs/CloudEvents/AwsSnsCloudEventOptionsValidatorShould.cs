// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.CloudEvents;

/// <summary>
/// 6nyyj6 (S868) — independent (author≠impl, TestsDeveloper) lock for
/// <see cref="AwsSnsCloudEventOptionsValidator"/>. Non-vacuous: RED on the pre-wire no-op, GREEN on the
/// shipped rules (SNS Subject length limit; FIFO topics require a message group id when content-based
/// deduplication is disabled).
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class AwsSnsCloudEventOptionsValidatorShould
{
	private readonly AwsSnsCloudEventOptionsValidator _validator = new();

	[Fact]
	public void SucceedForDefaultOptions() =>
		_validator.Validate(null, new AwsSnsCloudEventOptions()).Succeeded.ShouldBeTrue();

	[Fact]
	public void FailWhenOptionsIsNull() =>
		_validator.Validate(null, null!).Failed.ShouldBeTrue();

	[Fact]
	public void FailWhenDefaultSubjectExceedsLimit()
	{
		var result = _validator.Validate(null, new AwsSnsCloudEventOptions
		{
			DefaultSubject = new string('x', 101), // SNS Subject limit is 100
		});

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(AwsSnsCloudEventOptions.DefaultSubject));
	}

	[Fact]
	public void FailWhenFifoWithoutContentDedupAndNoMessageGroupId()
	{
		var result = _validator.Validate(null, new AwsSnsCloudEventOptions
		{
			UseFifoFeatures = true,
			EnableContentBasedDeduplication = false,
			DefaultMessageGroupId = null,
		});

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(AwsSnsCloudEventOptions.DefaultMessageGroupId));
	}

	[Fact]
	public void SucceedForFifoWithContentDeduplication()
	{
		var result = _validator.Validate(null, new AwsSnsCloudEventOptions
		{
			UseFifoFeatures = true,
			EnableContentBasedDeduplication = true,
		});

		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void SucceedForFifoWithExplicitMessageGroupId()
	{
		var result = _validator.Validate(null, new AwsSnsCloudEventOptions
		{
			UseFifoFeatures = true,
			EnableContentBasedDeduplication = false,
			DefaultMessageGroupId = "group-1",
		});

		result.Succeeded.ShouldBeTrue();
	}
}
