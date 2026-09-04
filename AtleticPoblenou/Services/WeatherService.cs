using System.Globalization;
using System.Text.Json;
using AtleticPoblenou.Models;

namespace AtleticPoblenou.Services;

/// <summary>Previsión horaria de Open-Meteo para la hora y cancha del partido. Si falla, IsAvailable = false (nunca inventa datos).</summary>
public class WeatherService
{
    private readonly HttpClient _http;

    public WeatherService(HttpClient http)
    {
        _http = http;
    }

    public async Task<MatchWeatherInfo> GetMatchWeatherAsync(DateTime matchDate, string locationName)
    {
        var (lat, lon) = ResolveCoordinates(locationName);
        var result = new MatchWeatherInfo
        {
            LocationName = !string.IsNullOrEmpty(locationName) ? locationName : "Camp Municipal Agapito Fernández"
        };

        // Open-Meteo solo cubre 16 días; para partidos más lejanos no hay previsión.
        if (matchDate < DateTime.Now.AddDays(-1) || matchDate > DateTime.Now.AddDays(15))
        {
            return result;
        }

        try
        {
            var url = $"https://api.open-meteo.com/v1/forecast?latitude={lat.ToString("F4", CultureInfo.InvariantCulture)}&longitude={lon.ToString("F4", CultureInfo.InvariantCulture)}&hourly=temperature_2m,relative_humidity_2m,precipitation_probability,weather_code,wind_speed_10m&timezone=auto&forecast_days=16";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await _http.GetAsync(url, cts.Token);
            if (!response.IsSuccessStatusCode) return result;

            var json = await response.Content.ReadAsStringAsync(cts.Token);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("hourly", out var hourly)) return result;

            var times = hourly.GetProperty("time").EnumerateArray().Select(x => x.GetString() ?? "").ToList();
            var matchIsoHour = matchDate.ToString("yyyy-MM-ddTHH:00");
            var index = times.FindIndex(t => t.StartsWith(matchIsoHour));
            if (index < 0) return result;

            var temps = hourly.GetProperty("temperature_2m").EnumerateArray().ToList();
            var rains = hourly.GetProperty("precipitation_probability").EnumerateArray().ToList();
            var winds = hourly.GetProperty("wind_speed_10m").EnumerateArray().ToList();
            var hums = hourly.GetProperty("relative_humidity_2m").EnumerateArray().ToList();
            var codes = hourly.GetProperty("weather_code").EnumerateArray().ToList();

            if (index >= temps.Count || temps[index].ValueKind == JsonValueKind.Null) return result;

            result.Temperature = Math.Round(temps[index].GetDouble(), 1);
            result.PrecipitationProbability = index < rains.Count && rains[index].ValueKind != JsonValueKind.Null ? rains[index].GetInt32() : 0;
            result.WindSpeed = index < winds.Count && winds[index].ValueKind != JsonValueKind.Null ? Math.Round(winds[index].GetDouble(), 1) : 0;
            result.Humidity = index < hums.Count && hums[index].ValueKind != JsonValueKind.Null ? hums[index].GetInt32() : 0;
            var code = index < codes.Count && codes[index].ValueKind != JsonValueKind.Null ? codes[index].GetInt32() : 0;

            var (cond, icon, optimal) = MapWeatherCode(code, result.PrecipitationProbability, result.Temperature);
            result.ConditionText = cond;
            result.Icon = icon;
            result.IsOptimal = optimal;
            result.IsAvailable = true;
        }
        catch
        {
            result.IsAvailable = false;
        }

        return result;
    }

    private static (double Lat, double Lon) ResolveCoordinates(string? locationName)
    {
        var loc = locationName?.ToLowerInvariant() ?? "";
        if (loc.Contains("sabadell") || loc.Contains("planada")) return (41.5433, 2.1094);
        if (loc.Contains("badia")) return (41.5085, 2.1481);
        if (loc.Contains("cerdanyola") || loc.Contains("fontetas")) return (41.4925, 2.1415);
        if (loc.Contains("terrassa") || loc.Contains("roca") || loc.Contains("pueblo nuevo")) return (41.5632, 2.0089);
        if (loc.Contains("perpetua")) return (41.5348, 2.1812);
        if (loc.Contains("lliça") || loc.Contains("llica")) return (41.5936, 2.2356);
        return (41.3985, 2.2032); // Poblenou, Barcelona
    }

    private static (string Condition, string Icon, bool IsOptimal) MapWeatherCode(int code, int rainPct, double temp)
    {
        if (rainPct > 60 || code >= 61 && code <= 67 || code >= 80 && code <= 82)
            return ("Lluvia prevista", "🌧️", false);
        if (code >= 95)
            return ("Tormenta eléctrica", "⛈️", false);
        if (code >= 71 && code <= 77)
            return ("Nieve", "❄️", false);
        if (code == 45 || code == 48)
            return ("Niebla en cancha", "🌫️", true);
        if (code == 1 || code == 2)
            return ("Parcialmente nublado", "⛅", true);
        if (code == 3)
            return ("Nublado", "☁️", true);
        if (temp > 32)
            return ("Calor intenso", "☀️", false);
        return ("Cielo despejado", "☀️", true);
    }
}
