// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Options.Core;
using Excalibur.Dispatch.Options.Validation;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class CoreOptionsShould
{
	// --- PipelineOptions ---

	[Fact]
	public void PipelineOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new PipelineOptions();

		// Assert
		options.MaxConcurrency.ShouldBe(Environment.ProcessorCount * 2);
		options.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.EnableParallelProcessing.ShouldBeTrue();
		options.StopOnFirstError.ShouldBeFalse();
		options.BufferSize.ShouldBe(1000);
		options.ApplicableMessageKinds.ShouldBe(MessageKinds.Action | MessageKinds.Event | MessageKinds.Document);
	}

	[Fact]
	public void PipelineOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new PipelineOptions
		{
			MaxConcurrency = 16,
			DefaultTimeout = TimeSpan.FromMinutes(2),
			EnableParallelProcessing = false,
			StopOnFirstError = true,
			BufferSize = 500,
			ApplicableMessageKinds = MessageKinds.Action,
		};

		// Assert
		options.MaxConcurrency.ShouldBe(16);
		options.DefaultTimeout.ShouldBe(TimeSpan.FromMinutes(2));
		options.EnableParallelProcessing.ShouldBeFalse();
		options.StopOnFirstError.ShouldBeTrue();
		options.BufferSize.ShouldBe(500);
		options.ApplicableMessageKinds.ShouldBe(MessageKinds.Action);
	}

	// --- VersioningOptions ---

	[Fact]
	public void VersioningOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new VersioningOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		options.RequireContractVersion.ShouldBeTrue();
	}

	[Fact]
	public void VersioningOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new VersioningOptions
		{
			Enabled = false,
			RequireContractVersion = false,
		};

		// Assert
		options.Enabled.ShouldBeFalse();
		options.RequireContractVersion.ShouldBeFalse();
	}
}
