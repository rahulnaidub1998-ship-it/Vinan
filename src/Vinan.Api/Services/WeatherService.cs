using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Vinan.Api.Services;

public sealed class WeatherService
{
    private readonly HttpClient _httpClient;

    public WeatherService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<WeatherReport?> GetAsync(string location, CancellationToken cancellationToken = default)
    {
        var geocodingUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(location)}&count=1&language=en&format=json";
        var places = await _httpClient.GetFromJsonAsync<GeocodingResponse>(geocodingUrl, cancellationToken);
        var place = places?.Results?.FirstOrDefault();
        if (place is null)
        {
            return null;
        }

        var latitude = place.Latitude.ToString(CultureInfo.InvariantCulture);
        var longitude = place.Longitude.ToString(CultureInfo.InvariantCulture);
        var forecastUrl = "https://api.open-meteo.com/v1/forecast"
            + $"?latitude={latitude}&longitude={longitude}"
            + "&current=temperature_2m,apparent_temperature,weather_code,wind_speed_10m"
            + "&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max"
            + "&temperature_unit=fahrenheit&wind_speed_unit=mph&timezone=auto&forecast_days=3";
        var forecast = await _httpClient.GetFromJsonAsync<ForecastResponse>(forecastUrl, cancellationToken);
        if (forecast?.Current is null)
        {
            return null;
        }

        var days = new List<WeatherDay>();
        var count = new[]
        {
            forecast.Daily?.Dates?.Count ?? 0,
            forecast.Daily?.WeatherCodes?.Count ?? 0,
            forecast.Daily?.MaximumTemperatures?.Count ?? 0,
            forecast.Daily?.MinimumTemperatures?.Count ?? 0,
            forecast.Daily?.PrecipitationProbabilities?.Count ?? 0,
        }.Min();
        for (var index = 0; index < count; index++)
        {
            days.Add(new WeatherDay(
                forecast.Daily!.Dates![index],
                Describe(forecast.Daily.WeatherCodes![index]),
                forecast.Daily.MaximumTemperatures![index],
                forecast.Daily.MinimumTemperatures![index],
                forecast.Daily.PrecipitationProbabilities![index]));
        }

        var region = new[] { place.Admin1, place.Country }
            .Where(value => !string.IsNullOrWhiteSpace(value));
        return new WeatherReport(
            $"{place.Name}, {string.Join(", ", region)}",
            Describe(forecast.Current.WeatherCode),
            forecast.Current.Temperature,
            forecast.Current.ApparentTemperature,
            forecast.Current.WindSpeed,
            days);
    }

    public static string Format(WeatherReport report)
    {
        var forecast = string.Join("; ", report.Forecast.Select(day =>
            $"{day.Date:ddd}: {day.Description}, {day.LowTemperature:0}-{day.HighTemperature:0}°F, {day.PrecipitationProbability}% rain"));
        return $"In {report.Location}, it is {report.Temperature:0}°F and {report.Description.ToLowerInvariant()} "
            + $"(feels like {report.ApparentTemperature:0}°F), with wind at {report.WindSpeed:0} mph. "
            + $"Forecast: {forecast}.";
    }

    private static string Describe(int code) => code switch
    {
        0 => "Clear",
        1 or 2 => "Partly cloudy",
        3 => "Cloudy",
        45 or 48 => "Foggy",
        51 or 53 or 55 or 56 or 57 => "Drizzle",
        61 or 63 or 65 or 66 or 67 => "Rain",
        71 or 73 or 75 or 77 => "Snow",
        80 or 81 or 82 => "Rain showers",
        85 or 86 => "Snow showers",
        95 or 96 or 99 => "Thunderstorms",
        _ => "Mixed conditions",
    };

    private sealed record GeocodingResponse([property: JsonPropertyName("results")] List<GeocodingPlace>? Results);
    private sealed record GeocodingPlace(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("latitude")] double Latitude,
        [property: JsonPropertyName("longitude")] double Longitude,
        [property: JsonPropertyName("admin1")] string? Admin1,
        [property: JsonPropertyName("country")] string? Country);
    private sealed record ForecastResponse(
        [property: JsonPropertyName("current")] CurrentWeather? Current,
        [property: JsonPropertyName("daily")] DailyWeather? Daily);
    private sealed record CurrentWeather(
        [property: JsonPropertyName("temperature_2m")] double Temperature,
        [property: JsonPropertyName("apparent_temperature")] double ApparentTemperature,
        [property: JsonPropertyName("weather_code")] int WeatherCode,
        [property: JsonPropertyName("wind_speed_10m")] double WindSpeed);
    private sealed record DailyWeather(
        [property: JsonPropertyName("time")] List<DateOnly>? Dates,
        [property: JsonPropertyName("weather_code")] List<int>? WeatherCodes,
        [property: JsonPropertyName("temperature_2m_max")] List<double>? MaximumTemperatures,
        [property: JsonPropertyName("temperature_2m_min")] List<double>? MinimumTemperatures,
        [property: JsonPropertyName("precipitation_probability_max")] List<int>? PrecipitationProbabilities);
}

public sealed record WeatherReport(
    string Location,
    string Description,
    double Temperature,
    double ApparentTemperature,
    double WindSpeed,
    IReadOnlyList<WeatherDay> Forecast);

public sealed record WeatherDay(
    DateOnly Date,
    string Description,
    double HighTemperature,
    double LowTemperature,
    int PrecipitationProbability);
