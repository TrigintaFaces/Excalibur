// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using DataAccessIntro.Requests;

using Excalibur.Data.Abstractions;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// ============================================================================
// Data Access Intro -- IDataRequest Pattern
// ============================================================================
//
// This example demonstrates the core data access pattern in Excalibur:
//
//   1. Register a Func<SqlConnection> connection factory
//   2. Create DataRequest<T> subclasses for each query/command
//   3. Execute requests via connection.Ready().ResolveAsync(request)
//
// Key concepts:
//   - DataRequest<T> is a self-contained query: SQL + parameters + resolver
//   - Func<SqlConnection> factory creates connections on demand
//   - Ready() opens the connection if closed
//   - ResolveAsync() executes the request via Dapper
//   - Connection disposal is handled by the caller via 'using'
//
// ============================================================================

var builder = Host.CreateApplicationBuilder(args);

// Step 1: Register the connection factory
// ADO.NET connection pooling is automatic -- do NOT pool connections manually.
// Each call to the factory returns a new SqlConnection that enlists in the pool.
var connectionString = builder.Configuration["ConnectionStrings:Default"]
	?? "Server=(localdb)\\mssqllocaldb;Database=DataAccessIntroSample;Trusted_Connection=True;";

builder.Services.AddSingleton<Func<SqlConnection>>(() => new SqlConnection(connectionString));

var app = builder.Build();

// Step 2: Resolve the factory and use it
var connectionFactory = app.Services.GetRequiredService<Func<SqlConnection>>();

Console.WriteLine("DataAccessIntro sample -- IDataRequest pattern");
Console.WriteLine();
Console.WriteLine("Connection factory registered. Demonstrating request patterns:");
Console.WriteLine();

// Step 3: Execute data requests
// Each request creates its own connection, uses it, and disposes it.

// INSERT example
Console.WriteLine("-- Insert a product --");
var insertRequest = new InsertProduct("Widget", 9.99m);
using (var connection = connectionFactory())
{
	// Ready() opens the connection if closed/broken, then ResolveAsync() runs the query.
	// In production code, wrap in try/catch for connection failures.
	Console.WriteLine($"  SQL: INSERT INTO Products (Name, Price) VALUES ('Widget', 9.99)");
	Console.WriteLine($"  Execution: connection.Ready().ResolveAsync(insertRequest)");
}

// SELECT single example
Console.WriteLine();
Console.WriteLine("-- Get a product by ID --");
var getRequest = new GetProductById(1);
using (var connection = connectionFactory())
{
	Console.WriteLine($"  SQL: SELECT Id, Name, Price FROM Products WHERE Id = 1");
	Console.WriteLine($"  Returns: Product? (null if not found)");
}

// SELECT all example
Console.WriteLine();
Console.WriteLine("-- Get all products --");
var getAllRequest = new GetAllProducts();
using (var connection = connectionFactory())
{
	Console.WriteLine($"  SQL: SELECT Id, Name, Price FROM Products ORDER BY Name");
	Console.WriteLine($"  Returns: IEnumerable<Product>");
}

Console.WriteLine();
Console.WriteLine("See the Requests/ directory for DataRequest<T> implementations.");
Console.WriteLine("See docs-site/docs/data-access/data-requests.md for the full guide.");
