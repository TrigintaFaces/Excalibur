// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Excalibur.A3.AspNetCore.Tests;

/// <summary>
/// An MVC controller gated by grant policy names written as attribute literals, which is the only form
/// an attribute argument can take.
/// </summary>
[ApiController]
public sealed class OrdersController : ControllerBase
{
	// Strongly-typed form — the recommended way to apply a grant policy.
	[HttpGet("/mvc/orders/{id}")]
	[RequireGrant("Read", "Order", "id")]
	public IActionResult GetById(string id) => Ok(id);

	[HttpGet("/mvc/orders")]
	[Authorize(Policy = "grant:Read:Order")]
	public IActionResult GetAll() => Ok("all");

	[HttpGet("/mvc/reports")]
	[Authorize(Policy = "grant:Read:Order:{id}")]
	public IActionResult Reports() => Ok("reports");
}
