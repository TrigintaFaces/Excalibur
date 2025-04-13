using Excalibur.Tests.Shared.Fixtures;

namespace Excalibur.Tests.Shared;

/// <summary>
/// Base class for tests that use ApplicationContext to ensure proper cleanup.
/// </summary>
public abstract class SharedApplicationContextTestBase : IDisposable
{
	/// <summary>
	/// The ApplicationContext fixture.
	/// </summary>
	protected readonly ApplicationContextFixture Fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="SharedApplicationContextTestBase"/> class.
	/// </summary>
	protected SharedApplicationContextTestBase()
	{
		Fixture = new ApplicationContextFixture();
	}

	/// <summary>
	/// Disposes the test class and cleans up the ApplicationContext.
	/// </summary>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Disposes the test class resources.
	/// </summary>
	/// <param name="disposing">Whether the method is being called from Dispose().</param>
	protected virtual void Dispose(bool disposing)
	{
		if (disposing)
		{
			Fixture.Dispose();
		}
	}
}
