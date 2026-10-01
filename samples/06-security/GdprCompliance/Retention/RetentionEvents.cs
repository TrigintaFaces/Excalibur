// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch;

namespace GdprCompliance.Retention;

/// <summary>
/// Raised when a customer profile is opened. The profile aggregate is the one that holds the
/// person's identifying data, and it is the one an erasure destroys.
/// </summary>
[MessageName("Contoso.Gdpr.CustomerProfileRegistered")]
public sealed record CustomerProfileRegistered(
	Guid CustomerId,
	string FullName,
	string EmailAddress) : DomainEvent;

/// <summary>
/// Raised when a vehicle is sold.
/// </summary>
/// <remarks>
/// The buyer's name and address are carried <b>on this event</b>, not reached by following a reference
/// into the customer profile. That is what lets the sales record stay readable after the buyer's profile
/// is erased, and it is the modelling discipline a declared retention depends on.
/// </remarks>
[MessageName("Contoso.Gdpr.VehicleSold")]
public sealed record VehicleSold(
	Guid SaleId,
	Guid BuyerId,
	string BuyerName,
	string BuyerAddress,
	string VehicleIdentificationNumber,
	decimal SalePrice) : DomainEvent;
