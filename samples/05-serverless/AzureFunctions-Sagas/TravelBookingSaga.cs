using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Excalibur.Hosting.AzureFunctions;

namespace examples.Excalibur.Dispatch.Examples.Serverless.Azure.Sagas;

/// <summary>
/// Example of a travel booking saga using the fluent builder API.
/// Demonstrates booking flights, hotels, and car rentals with compensation.
/// </summary>
public class TravelBookingSagaFunction {
 private readonly ILogger<TravelBookingSagaFunction> _logger;

 /// <summary>
 /// Initializes a new instance of the <see cref="TravelBookingSagaFunction"/> class.
 /// </summary>
 public TravelBookingSagaFunction(ILogger<TravelBookingSagaFunction> logger)
 {
 _logger = logger;
 }

 /// <summary>
 /// Creates and runs a travel booking saga.
 /// </summary>
 [Function("TravelBookingSaga")]
 public async Task<TravelBookingResult> RunTravelBookingSaga(
 [OrchestrationTrigger] TaskOrchestrationContext context)
 {
 var input = context.GetInput<TravelBookingRequest>()!;

 // Build the saga using fluent API
 var saga = SagaBuilder<TravelBookingRequest, TravelBookingResult>
 .Create("TravelBooking")
 .WithTimeout(TimeSpan.FromMinutes(15))
 .WithAutoCompensation(true)
 .WithDefaultRetry(maxAttempts: 3, firstRetryInterval: TimeSpan.FromSeconds(2))
 .WithInputValidation(async request =>
 {
 if (request.DepartureDate <= DateTime.UtcNow)
 throw new ArgumentException("Departure date must be in the future");

 if (request.ReturnDate <= request.DepartureDate)
 throw new ArgumentException("Return date must be after departure date");

 await Task.CompletedTask;
 })
 .AddStep<FlightSearchRequest, FlightBookingResult>("SearchAndBookFlights", step => step
 .ExecuteActivity("BookFlightActivity")
 .WithInput((request, state) => new FlightSearchRequest
 {
 Origin = request.Origin,
 Destination = request.Destination,
 DepartureDate = request.DepartureDate,
 ReturnDate = request.ReturnDate,
 PassengerCount = request.Travelers.Count,
 CabinClass = request.PreferredCabinClass
 })
 .WithOutput((result, state) =>
 {
 state.CustomData["FlightBooking"] = result;
 state.CustomData["TotalCost"] = result.TotalPrice;
 })
 .WithCompensation("CancelFlightActivity")
 .WithTimeout(TimeSpan.FromMinutes(2)))
 .AddStep<HotelSearchRequest, HotelBookingResult>("SearchAndBookHotel", step => step
 .ExecuteActivity("BookHotelActivity")
 .WithInput((request, state) => new HotelSearchRequest
 {
 Location = request.Destination,
 CheckInDate = request.DepartureDate,
 CheckOutDate = request.ReturnDate,
 GuestCount = request.Travelers.Count,
 RoomType = request.PreferredRoomType
 })
 .WithOutput((result, state) =>
 {
 state.CustomData["HotelBooking"] = result;
 var currentTotal = (decimal)state.CustomData["TotalCost"];
 state.CustomData["TotalCost"] = currentTotal + result.TotalPrice;
 })
 .WithCompensation("CancelHotelActivity")
 .WithTimeout(TimeSpan.FromMinutes(2)))
 .AddConditionalStep<CarRentalRequest, CarRentalResult>(
 "BookCarRental",
 (request, state) => request.IncludeCarRental,
 step => step
 .ExecuteActivity("BookCarActivity")
 .WithInput((request, state) => new CarRentalRequest
 {
 PickupLocation = request.Destination,
 PickupDate = request.DepartureDate,
 ReturnDate = request.ReturnDate,
 CarType = request.PreferredCarType ?? "Economy"
 })
 .WithOutput((result, state) =>
 {
 state.CustomData["CarRental"] = result;
 var currentTotal = (decimal)state.CustomData["TotalCost"];
 state.CustomData["TotalCost"] = currentTotal + result.TotalPrice;
 })
 .WithCompensation("CancelCarActivity"))
 .AddParallelSteps("NotificationGroup", group => group
 .AddStep<EmailNotificationRequest, bool>("SendEmailConfirmation", step => step
 .ExecuteActivity("SendTravelEmailActivity")
 .WithInput((request, state) => new EmailNotificationRequest
 {
 To = request.ContactEmail,
 Subject = "Your Travel Booking Confirmation",
 BookingDetails = BuildBookingDetails(state)
 })
 .WithOutput((result, state) => state.CustomData["EmailSent"] = result))
 .AddStep<SmsNotificationRequest, bool>("SendSmsConfirmation", step => step
 .ExecuteActivity("SendTravelSmsActivity")
 .WithInput((request, state) => new SmsNotificationRequest
 {
 PhoneNumber = request.ContactPhone,
 Message = $"Travel booking confirmed! Total: ${state.CustomData["TotalCost"]:F2}"
 })
 .WithOutput((result, state) => state.CustomData["SmsSent"] = result)))
 .WithOutputBuilder(async (request, state) =>
 {
 var flightBooking = state.CustomData["FlightBooking"] as FlightBookingResult;
 var hotelBooking = state.CustomData["HotelBooking"] as HotelBookingResult;
 var carRental = state.CustomData.ContainsKey("CarRental")
 ? state.CustomData["CarRental"] as CarRentalResult
 : null;

 return await Task.FromResult(new TravelBookingResult
 {
 BookingId = state.SagaId,
 Status = "Confirmed",
 FlightConfirmation = flightBooking?.ConfirmationNumber,
 HotelConfirmation = hotelBooking?.ConfirmationNumber,
 CarRentalConfirmation = carRental?.ConfirmationNumber,
 TotalCost = (decimal)state.CustomData["TotalCost"],
 BookingDate = state.StartTime,
 TravelDates = new TravelDates
 {
 DepartureDate = request.DepartureDate,
 ReturnDate = request.ReturnDate
 }
 });
 })
 .WithErrorHandler((state, ex) =>
 {
 _logger.LogError(ex, "Travel booking saga failed at step {Step}",
 state.LastExecutedStep?.StepName);
 })
 .Build(_logger as ILogger<FluentSagaOrchestration<TravelBookingRequest, TravelBookingResult>>);

 return await saga.RunAsync(context, input);
 }

 private static string BuildBookingDetails(SagaState state)
 {
 var flight = state.CustomData["FlightBooking"] as FlightBookingResult;
 var hotel = state.CustomData["HotelBooking"] as HotelBookingResult;
 var car = state.CustomData.ContainsKey("CarRental")
 ? state.CustomData["CarRental"] as CarRentalResult
 : null;

 var details = $@"
Travel Booking Confirmation

Flight Details:
- Confirmation: {flight?.ConfirmationNumber}
- Airline: {flight?.Airline}
- Price: ${flight?.TotalPrice:F2}

Hotel Details:
- Confirmation: {hotel?.ConfirmationNumber}
- Hotel: {hotel?.HotelName}
- Price: ${hotel?.TotalPrice:F2}";

 if (car != null)
 {
 details += $@"

Car Rental Details:
- Confirmation: {car.ConfirmationNumber}
- Company: {car.RentalCompany}
- Price: ${car.TotalPrice:F2}";
 }

 details += $@"

Total Cost: ${state.CustomData["TotalCost"]:F2}
";

 return details;
 }
}

// Models for Travel Booking

public class TravelBookingRequest {
 public string Origin { get; set; } = string.Empty;
 public string Destination { get; set; } = string.Empty;
 public DateTime DepartureDate { get; set; }
 public DateTime ReturnDate { get; set; }
 public List<Traveler> Travelers { get; set; } = new();
 public string PreferredCabinClass { get; set; } = "Economy";
 public string PreferredRoomType { get; set; } = "Standard";
 public bool IncludeCarRental { get; set; }
 public string? PreferredCarType { get; set; }
 public string ContactEmail { get; set; } = string.Empty;
 public string ContactPhone { get; set; } = string.Empty;
}

public class TravelBookingResult {
 public string BookingId { get; set; } = string.Empty;
 public string Status { get; set; } = string.Empty;
 public string? FlightConfirmation { get; set; }
 public string? HotelConfirmation { get; set; }
 public string? CarRentalConfirmation { get; set; }
 public decimal TotalCost { get; set; }
 public DateTime BookingDate { get; set; }
 public TravelDates TravelDates { get; set; } = new();
}

public class Traveler {
 public string FirstName { get; set; } = string.Empty;
 public string LastName { get; set; } = string.Empty;
 public DateTime DateOfBirth { get; set; }
 public string PassportNumber { get; set; } = string.Empty;
}

public class TravelDates {
 public DateTime DepartureDate { get; set; }
 public DateTime ReturnDate { get; set; }
}

// Activity request/response models

public class FlightSearchRequest {
 public string Origin { get; set; } = string.Empty;
 public string Destination { get; set; } = string.Empty;
 public DateTime DepartureDate { get; set; }
 public DateTime ReturnDate { get; set; }
 public int PassengerCount { get; set; }
 public string CabinClass { get; set; } = string.Empty;
}

public class FlightBookingResult {
 public string ConfirmationNumber { get; set; } = string.Empty;
 public string Airline { get; set; } = string.Empty;
 public decimal TotalPrice { get; set; }
 public string OutboundFlightNumber { get; set; } = string.Empty;
 public string ReturnFlightNumber { get; set; } = string.Empty;
}

public class HotelSearchRequest {
 public string Location { get; set; } = string.Empty;
 public DateTime CheckInDate { get; set; }
 public DateTime CheckOutDate { get; set; }
 public int GuestCount { get; set; }
 public string RoomType { get; set; } = string.Empty;
}

public class HotelBookingResult {
 public string ConfirmationNumber { get; set; } = string.Empty;
 public string HotelName { get; set; } = string.Empty;
 public decimal TotalPrice { get; set; }
 public string RoomType { get; set; } = string.Empty;
}

public class CarRentalRequest {
 public string PickupLocation { get; set; } = string.Empty;
 public DateTime PickupDate { get; set; }
 public DateTime ReturnDate { get; set; }
 public string CarType { get; set; } = string.Empty;
}

public class CarRentalResult {
 public string ConfirmationNumber { get; set; } = string.Empty;
 public string RentalCompany { get; set; } = string.Empty;
 public decimal TotalPrice { get; set; }
 public string CarModel { get; set; } = string.Empty;
}

public class EmailNotificationRequest {
 public string To { get; set; } = string.Empty;
 public string Subject { get; set; } = string.Empty;
 public string BookingDetails { get; set; } = string.Empty;
}

public class SmsNotificationRequest {
 public string PhoneNumber { get; set; } = string.Empty;
 public string Message { get; set; } = string.Empty;
}