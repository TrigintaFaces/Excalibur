// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Options;

/// <summary>
/// Unit tests for <see cref="GooglePubSubOptions"/> internal class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "Options")]
public sealed class GooglePubSubOptionsShould
{
	[Fact]
	public void BeInternalAndSealed()
	{
		// Assert
		typeof(GooglePubSubOptions).IsNotPublic.ShouldBeTrue();
		typeof(GooglePubSubOptions).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void BeInstantiable()
	{
		// Act
		var options = new GooglePubSubOptions();

		// Assert
		options.ShouldNotBeNull();
	}
}
