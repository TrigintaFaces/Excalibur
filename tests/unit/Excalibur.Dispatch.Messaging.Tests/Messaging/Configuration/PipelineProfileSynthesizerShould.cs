// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Options.Configuration;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Configuration;

/// <summary>
///     Tests for the <see cref="PipelineProfileSynthesizer" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PipelineProfileSynthesizerShould
{
	[Fact]
	public void ThrowForNullLogger() =>
		Should.Throw<ArgumentNullException>(() =>
			new PipelineProfileSynthesizer(
				null!,
				Microsoft.Extensions.Options.Options.Create(new DispatchOptions()),
				A.Fake<IMiddlewareApplicabilityStrategy>()));

	[Fact]
	public void ThrowForNullOptions() =>
		Should.Throw<ArgumentNullException>(() =>
			new PipelineProfileSynthesizer(
				NullLogger<PipelineProfileSynthesizer>.Instance,
				null!,
				A.Fake<IMiddlewareApplicabilityStrategy>()));

	[Fact]
	public void CreateSuccessfully()
	{
		var sut = new PipelineProfileSynthesizer(
			NullLogger<PipelineProfileSynthesizer>.Instance,
			Microsoft.Extensions.Options.Options.Create(new DispatchOptions()),
			A.Fake<IMiddlewareApplicabilityStrategy>());

		sut.ShouldNotBeNull();
	}

	[Fact]
	public void SynthesizeDefaultProfile()
	{
		var sut = new PipelineProfileSynthesizer(
			NullLogger<PipelineProfileSynthesizer>.Instance,
			Microsoft.Extensions.Options.Options.Create(new DispatchOptions()),
			A.Fake<IMiddlewareApplicabilityStrategy>());

		var profile = sut.SynthesizeDefaultProfile();
		profile.ShouldNotBeNull();
		profile.ShouldBeAssignableTo<IPipelineProfile>();
	}

	[Fact]
	public void SynthesizeNamedProfile()
	{
		var sut = new PipelineProfileSynthesizer(
			NullLogger<PipelineProfileSynthesizer>.Instance,
			Microsoft.Extensions.Options.Options.Create(new DispatchOptions()),
			A.Fake<IMiddlewareApplicabilityStrategy>());

		var profile = sut.SynthesizeDefaultProfile("custom-profile");
		profile.ShouldNotBeNull();
	}
}
