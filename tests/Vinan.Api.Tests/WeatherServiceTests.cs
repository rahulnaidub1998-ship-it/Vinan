using System.Net;
using System.Text;
using Vinan.Api.Services;

namespace Vinan.Api.Tests;

public sealed class WeatherServiceTests
{
    [Fact]
    public async Task ReadsLiveWeatherProviderResponses()
    {
        var client = new HttpClient(new WeatherHandler());
        var service = new WeatherService(client);

        var report = await service.GetAsync("San Diego");

        Assert.NotNull(report);
        Assert.Equal("San Diego, California, United States", report.Location);
        Assert.Equal(72, report.Temperature);
        Assert.Equal(3, report.Forecast.Count);
        Assert.Contains("72", WeatherService.Format(report));
    }

    private sealed class WeatherHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var json = request.RequestUri!.Host.StartsWith("geocoding", StringComparison.Ordinal)
                ? """{"results":[{"name":"San Diego","latitude":32.7157,"longitude":-117.1611,"admin1":"California","country":"United States"}]}"""
                : """{"current":{"temperature_2m":72,"apparent_temperature":71,"weather_code":0,"wind_speed_10m":6},"daily":{"time":["2026-07-12","2026-07-13","2026-07-14"],"weather_code":[0,1,2],"temperature_2m_max":[75,76,74],"temperature_2m_min":[64,65,63],"precipitation_probability_max":[0,5,10]}}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }
}
