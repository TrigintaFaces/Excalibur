using Excalibur.Tests.Mothers.Core;

namespace Excalibur.Tests.Fixtures;

/// <summary>
/// A fixture for managing the ApplicationContext's lifecycle in tests.
/// </summary>
public sealed class ApplicationContextFixture : IDisposable
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ApplicationContextFixture"/> class.
	/// </summary>
	public ApplicationContextFixture()
	{
		// Reset before test to ensure clean state
		ApplicationContextMother.Reset();
		
		// Initialize with default values
		ApplicationContextMother.InitializeDefault();
	}

	/// <summary>
	/// Disposes the fixture and resets the ApplicationContext.
	/// </summary>
	public void Dispose()
	{
		// Clean up after test
		ApplicationContextMother.Reset();
	}
}
