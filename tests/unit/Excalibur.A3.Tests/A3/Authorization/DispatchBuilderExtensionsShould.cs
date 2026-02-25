// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authorization;
using Excalibur.Dispatch.Abstractions.Configuration;

namespace Excalibur.Tests.A3.Authorization;

/// <summary>
/// Unit tests for <see cref="DispatchBuilderExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class DispatchBuilderExtensionsShould
{
	[Fact]
	public void AddDispatchAuthorization_ThrowsOnNullBuilder()
	{
		IDispatchBuilder? builder = null;

		Should.Throw<ArgumentNullException>(() =>
			builder!.AddDispatchAuthorization());
	}

	[Fact]
	public void AddDispatchAuthorization_ReturnsSameBuilder()
	{
		// Arrange
		var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		var result = builder.AddDispatchAuthorization();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void AddDispatchAuthorization_RegistersServicesViaBuilder()
	{
		// Arrange
		var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Act
		builder.AddDispatchAuthorization();

		// Assert — services should be registered via the builder's Services
		services.ShouldContain(sd => sd.ServiceType == typeof(IDispatchAuthorizationService));
	}
}
