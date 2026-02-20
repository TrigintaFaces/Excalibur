using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Excalibur.Dispatch.ServiceMesh;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Abstractions.Pipeline;

// Manual integration test for Service Mesh
var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices((context, services) =>
{
 // Add Dispatch messaging
 services.AddDispatch(dispatch =>
 {
 dispatch.UseInMemoryBus();
 });

 // Add Service Mesh integration
 services.AddDispatchServiceMesh(options =>
 {
 options.ServiceName = "test-service";
 options.InstanceId = "test-001";
 options.EnableEnvoySidecar = true;
 options.EnableCircuitBreaker = true;
 options.EnableMutualTls = true;
 options.EnableServiceDiscovery = true;
 options.EnableTrafficManagement = true;
 });
});

var host = builder.Build();

// Test basic functionality
var dispatcher = host.Services.GetRequiredService<IDispatcher>();
var serviceMesh = host.Services.GetRequiredService<ServiceMeshOptions>();

Console.WriteLine($"Service Mesh configured for: {serviceMesh.ServiceName}");
Console.WriteLine($"Instance ID: {serviceMesh.InstanceId}");
Console.WriteLine($"Envoy Sidecar: {serviceMesh.EnableEnvoySidecar}");
Console.WriteLine($"Circuit Breaker: {serviceMesh.EnableCircuitBreaker}");
Console.WriteLine($"Mutual TLS: {serviceMesh.EnableMutualTls}");

// If we get here without exceptions, basic wiring is correct
Console.WriteLine("\n✅ Service Mesh integration appears to be working!");
