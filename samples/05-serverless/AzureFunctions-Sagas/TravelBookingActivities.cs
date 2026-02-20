using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Excalibur.Hosting.AzureFunctions;

namespace examples.Excalibur.Dispatch.Examples.Serverless.Azure.Sagas;

/// <summary>
/// Activity functions for the travel booking saga.
/// </summary>
public class TravelBookingActivities {
 private readonly ILogger<TravelBookingActivities> _logger;
 private static readonly Random _random = new();

 /// <summary>
 /// Initializes a new instance of the <see cref="TravelBookingActivities"/> class.
 /// </summary>
 public TravelBookingActivities(ILogger<TravelBookingActivities> logger)
 {
 _logger = logger;
 }

 /// <summary>
 /// Books a flight.
 /// </summary>
 [Function("BookFlightActivity")]
 public async Task<FlightBookingResult> BookFlight(
 [ActivityTrigger] FlightSearchRequest request,
 FunctionContext context)
 {
 _logger.LogInformation("Booking flight from {Origin} to {Destination}",
 request.Origin, request.Destination);

 await Task.Delay(1000); // Simulate API call

 // Simulate flight booking
 var basePrice = _random.Next(200, 800);
 var totalPrice = basePrice * request.PassengerCount;

 return new FlightBookingResult
 {
 ConfirmationNumber = $"FL-{Guid.NewGuid():N}".Substring(0, 10).ToUpper(),
 Airline = "Example Airlines",
 TotalPrice = totalPrice,
 OutboundFlightNumber = $"EX{_random.Next(100, 999)}",
 ReturnFlightNumber = $"EX{_random.Next(100, 999)}"
 };
 }

 /// <summary>
 /// Cancels a flight booking.
 /// </summary>
 [Function("CancelFlightActivity")]
 public async Task CancelFlight(
 [ActivityTrigger] CompensationInput input,
 FunctionContext context)
 {
 var result = input.OriginalOutput as FlightBookingResult;

 _logger.LogInformation("Cancelling flight booking {ConfirmationNumber}",
 result?.ConfirmationNumber);

 await Task.Delay(500); // Simulate cancellation

 _logger.LogInformation("Flight booking cancelled successfully");
 }

 /// <summary>
 /// Books a hotel.
 /// </summary>
 [Function("BookHotelActivity")]
 public async Task<HotelBookingResult> BookHotel(
 [ActivityTrigger] HotelSearchRequest request,
 FunctionContext context)
 {
 _logger.LogInformation("Booking hotel in {Location} for {Nights} nights",
 request.Location, (request.CheckOutDate - request.CheckInDate).Days);

 await Task.Delay(800); // Simulate API call

 // Simulate hotel booking
 var nightlyRate = request.RoomType switch
 {
 "Suite" => _random.Next(200, 400),
 "Deluxe" => _random.Next(150, 250),
 _ => _random.Next(80, 150)
 };

 var nights = (request.CheckOutDate - request.CheckInDate).Days;
 var totalPrice = nightlyRate * nights;

 return new HotelBookingResult
 {
 ConfirmationNumber = $"HT-{Guid.NewGuid():N}".Substring(0, 10).ToUpper(),
 HotelName = "Example Hotel & Suites",
 TotalPrice = totalPrice,
 RoomType = request.RoomType
 };
 }

 /// <summary>
 /// Cancels a hotel booking.
 /// </summary>
 [Function("CancelHotelActivity")]
 public async Task CancelHotel(
 [ActivityTrigger] CompensationInput input,
 FunctionContext context)
 {
 var result = input.OriginalOutput as HotelBookingResult;

 _logger.LogInformation("Cancelling hotel booking {ConfirmationNumber}",
 result?.ConfirmationNumber);

 await Task.Delay(400); // Simulate cancellation

 _logger.LogInformation("Hotel booking cancelled successfully");
 }

 /// <summary>
 /// Books a car rental.
 /// </summary>
 [Function("BookCarActivity")]
 public async Task<CarRentalResult> BookCar(
 [ActivityTrigger] CarRentalRequest request,
 FunctionContext context)
 {
 _logger.LogInformation("Booking {CarType} car in {Location}",
 request.CarType, request.PickupLocation);

 await Task.Delay(600); // Simulate API call

 // Simulate car rental booking
 var dailyRate = request.CarType switch
 {
 "Luxury" => _random.Next(100, 200),
 "SUV" => _random.Next(70, 120),
 "Standard" => _random.Next(40, 70),
 _ => _random.Next(30, 50)
 };

 var days = (request.ReturnDate - request.PickupDate).Days;
 var totalPrice = dailyRate * days;

 return new CarRentalResult
 {
 ConfirmationNumber = $"CR-{Guid.NewGuid():N}".Substring(0, 10).ToUpper(),
 RentalCompany = "Example Car Rentals",
 TotalPrice = totalPrice,
 CarModel = request.CarType switch
 {
 "Luxury" => "Mercedes S-Class or similar",
 "SUV" => "Toyota Highlander or similar",
 "Standard" => "Toyota Camry or similar",
 _ => "Nissan Versa or similar"
 }
 };
 }

 /// <summary>
 /// Cancels a car rental booking.
 /// </summary>
 [Function("CancelCarActivity")]
 public async Task CancelCar(
 [ActivityTrigger] CompensationInput input,
 FunctionContext context)
 {
 var result = input.OriginalOutput as CarRentalResult;

 _logger.LogInformation("Cancelling car rental booking {ConfirmationNumber}",
 result?.ConfirmationNumber);

 await Task.Delay(300); // Simulate cancellation

 _logger.LogInformation("Car rental booking cancelled successfully");
 }

 /// <summary>
 /// Sends travel confirmation email.
 /// </summary>
 [Function("SendTravelEmailActivity")]
 public async Task<bool> SendTravelEmail(
 [ActivityTrigger] EmailNotificationRequest request,
 FunctionContext context)
 {
 _logger.LogInformation("Sending travel confirmation email to {To}", request.To);

 await Task.Delay(200); // Simulate email sending

 _logger.LogInformation("Email sent successfully");
 return true;
 }

 /// <summary>
 /// Sends travel confirmation SMS.
 /// </summary>
 [Function("SendTravelSmsActivity")]
 public async Task<bool> SendTravelSms(
 [ActivityTrigger] SmsNotificationRequest request,
 FunctionContext context)
 {
 _logger.LogInformation("Sending travel confirmation SMS to {PhoneNumber}",
 request.PhoneNumber);

 await Task.Delay(150); // Simulate SMS sending

 _logger.LogInformation("SMS sent successfully");
 return true;
 }
}