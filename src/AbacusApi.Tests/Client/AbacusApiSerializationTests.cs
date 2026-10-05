using System.Net;
using AwesomeAssertions;
using GeneratedAbacusApi = Pgarcia.AbacusApi.Client.V2026.AbacusApi;
using Pgarcia.AbacusApi.Client.V2026;
using Pgarcia.AbacusApi.Tests.Infrastructure;

namespace Pgarcia.AbacusApi.Tests.Client;

public class AbacusApiSerializationTests
{
    private static readonly Guid BookingId = Guid.Parse("6f1c0000-0000-0000-0000-000000000001");
    private static readonly string BookingUrl = $"https://abacus.test/api/entity/v1/mandants/7777/ProjectBookings({BookingId})";

    private readonly StubHttpMessageHandler _handler;
    private readonly GeneratedAbacusApi _uut;

    public AbacusApiSerializationTests()
    {
        _handler = new StubHttpMessageHandler()
            .Respond(BookingUrl, HttpStatusCode.OK, $$"""
            { "Id": "{{BookingId}}", "Date": "2026-10-05", "EmployeeId": 123, "ProjectId": 4711, "ServiceCodeId": 100,
              "Values": { "TimeFrom": "08:12:00", "TimeTo": "10:00:00", "InternalValue": { "Quantity": 1.75 } } }
            """);
        _uut = new GeneratedAbacusApi(new HttpClient(_handler) { BaseAddress = new Uri("https://abacus.test/api/entity/v1/mandants/7777/") });
    }

    [Fact]
    public async Task UpdateProjectBookingAsync_ShouldNotSendUnsetProperties()
    {
        await _uut.UpdateProjectBookingAsync(new ProjectBookingUpdate { Text = "Code review" }, BookingId);

        _handler.Requests[0].Body.Should().Be("""{"Text":"Code review"}""");
    }

    [Fact]
    public async Task GetProjectBookingAsync_ShouldReadAbacusFormats()
    {
        var booking = await _uut.GetProjectBookingAsync(BookingId);

        booking.Date.Should().Be(new DateOnly(2026, 10, 5));
        booking.EmployeeId.Should().Be(123);
        booking.Values!.InternalValue!.Quantity.Should().Be(1.75m);
        booking.Values.TimeFrom.Should().Be(new TimeOnly(8, 12));
    }

    [Fact]
    public async Task CreateProjectBookingAsync_ShouldWriteAbacusFormats()
    {
        _handler.Respond("https://abacus.test/api/entity/v1/mandants/7777/ProjectBookings", HttpStatusCode.Created, """{ "Id": "6f1c0000-0000-0000-0000-000000000001", "Date": "2026-10-05" }""");

        await _uut.CreateProjectBookingAsync(new ProjectBookingCreate
        {
            Id = BookingId,
            Date = new DateOnly(2026, 10, 5),
            Values = new ProjectBookingValuesCreate { TimeFrom = new TimeOnly(8, 12), InternalValue = new ProjectBookingValueCreate { Quantity = 1.75m } },
        });

        _handler.Requests[0].Body.Should().Contain("\"Date\":\"2026-10-05\"");
        _handler.Requests[0].Body.Should().Contain("\"Quantity\":1.75");
        _handler.Requests[0].Body.Should().Contain("\"TimeFrom\":\"08:12:00\"");
    }
}
