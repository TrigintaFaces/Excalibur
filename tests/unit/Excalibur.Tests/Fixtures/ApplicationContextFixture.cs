// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Application;
using Excalibur.Domain.Concurrency;

namespace Excalibur.Tests.Fixtures;

/// <summary>
///     Test fixture for ApplicationContext setup and cleanup.
/// </summary>
public sealed class ApplicationContextFixture : IDisposable
{
	/// <summary>
	///     Initializes a new instance of the <see cref="ApplicationContextFixture" /> class.
	/// </summary>
	public ApplicationContextFixture()
	{
		// Initialize the activity context for testing with fake dependencies
		var tenantId = A.Fake<ITenantId>();
		var correlationId = A.Fake<ICorrelationId>();
		var eTag = A.Fake<IETag>();
		var configuration = A.Fake<IConfiguration>();
		var clientAddress = A.Fake<IClientAddress>();
		var serviceProvider = A.Fake<IServiceProvider>();

		ActivityContext = new ActivityContext(tenantId, correlationId, eTag, configuration, clientAddress, serviceProvider);
		ActivityContext.SetValue("TestActivity", "TestActivityValue");
	}

	/// <summary>
	///     Gets the activity context for testing.
	/// </summary>
	public ActivityContext ActivityContext { get; }

	/// <summary>
	///     Cleans up the ApplicationContext.
	/// </summary>
	public void Dispose() =>
		// Clean up any resources Since this is a sealed class and we don't have unmanaged resources, we can use a simple dispose pattern
		GC.SuppressFinalize(this);
}
