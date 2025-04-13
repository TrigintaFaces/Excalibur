using System.Reflection;

using Excalibur.DataAccess.DataProcessing;
using Excalibur.Tests.Shared;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess.DataProcessing;

public class ServiceCollectionExtensionsShould
{
	private readonly IServiceCollection _services = new ServiceCollection();

	private readonly IConfiguration _configuration = ConfigurationTestHelper.BuildConfiguration(new Dictionary<string, string?>
	{
		{ "DataProcessing:TableName", "DataProcessor.DataTaskRequests" }, { "DataProcessing:MaxAttempts", "3" }
	});

	[Fact]
	public void ShouldThrowArgumentNullExceptionWhenConfigurationIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			_services.AddDataProcessing<TestDb, TestDb>(null!, "DataProcessing", Assembly.GetExecutingAssembly()));
	}

	[Fact]
	public void ShouldThrowArgumentExceptionWhenConfigurationSectionIsEmpty()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			_services.AddDataProcessing<TestDb, TestDb>(_configuration, "", Assembly.GetExecutingAssembly()));
	}

	[Fact]
	public void ShouldThrowArgumentNullExceptionWhenHandlerAssembliesIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			_services.AddDataProcessing<TestDb, TestDb>(_configuration, "DataProcessing", null!));
	}

	[Fact]
	public void ShouldRegisterRequiredServices()
	{
		// Act
		_ = _services.AddDataProcessing<TestDb, TestDb>(_configuration, "DataProcessing", Assembly.GetExecutingAssembly());

		// Assert
		_services.ShouldContain(descriptor => descriptor.ServiceType == typeof(IDataProcessorDb));
		_services.ShouldContain(descriptor => descriptor.ServiceType == typeof(IDataToProcessDb));
		_services.ShouldContain(descriptor => descriptor.ServiceType == typeof(IDataProcessorRegistry));
		_services.ShouldContain(descriptor => descriptor.ServiceType == typeof(IDataOrchestrationManager));
	}

	[Fact]
	public void ShouldRegisterDataProcessors()
	{
		// Act
		_ = _services.AddDataProcessing<TestDb, TestDb>(_configuration, "DataProcessing", Assembly.GetExecutingAssembly());

		// Assert
		_services.ShouldContain(descriptor => typeof(IDataProcessor).IsAssignableFrom(descriptor.ServiceType));
	}

	[Fact]
	public void ShouldRegisterRecordHandlers()
	{
		// Act
		_ = _services.AddDataProcessing<TestDb, TestDb>(_configuration, "DataProcessing", Assembly.GetExecutingAssembly());

		// Assert
		_services.ShouldContain(descriptor => descriptor.ServiceType.IsGenericType &&
											  descriptor.ServiceType.GetGenericTypeDefinition() == typeof(IRecordHandler<>));
	}
}
