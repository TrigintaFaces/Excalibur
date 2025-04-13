using Excalibur.Tests.Fixtures;

namespace Excalibur.Tests;

/// <summary>
///     Base class for tests that use ApplicationContext to ensure proper cleanup.
/// </summary>
public abstract class SharedApplicationContextTestBase : IDisposable
{
	/// <summary>
	///     The ApplicationContext fixture.
	/// </summary>
	private readonly ApplicationContextFixture _fixture;

	/// <summary>
	///     Initializes a new instance of the <see cref="SharedApplicationContextTestBase" /> class.
	/// </summary>
	protected SharedApplicationContextTestBase()
	{
		_fixture = new ApplicationContextFixture();
	}

	/// <summary>
	///     Disposes the test class and cleans up the ApplicationContext.
	/// </summary>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	///     Disposes the test class resources.
	/// </summary>
	/// <param name="disposing"> Whether the method is being called from Dispose(). </param>
	protected virtual void Dispose(bool disposing)
	{
		if (disposing)
		{
			_fixture.Dispose();
		}
	}
}
