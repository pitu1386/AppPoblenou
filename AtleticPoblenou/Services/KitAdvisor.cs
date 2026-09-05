using AtleticPoblenou.Models;

namespace AtleticPoblenou.Services;

public record KitRecommendation(string Description, string PrimaryColorHex, string SecondaryColorHex, bool IsClash, bool UsingAwayKit);

/// <summary>Decide qué camiseta llevar comparando el color principal de ambos equipos.</summary>
public static class KitAdvisor
{
    /// <summary>Distancia RGB por debajo de la cual dos colores se consideran "el mismo" a efectos de camiseta.</summary>
    private const double ClashThreshold = 90;

    public static KitRecommendation Recommend(ClubSettings club, RivalTeam? rival)
    {
        var homeDescription = string.IsNullOrWhiteSpace(club.KitDescription) ? "titular" : club.KitDescription;
        var home = new KitRecommendation(homeDescription, club.PrimaryColorHex, club.SecondaryColorHex, false, false);

        if (rival == null) return home;

        var clash = AreSimilar(club.PrimaryColorHex, rival.PrimaryColorHex);
        if (!clash) return home;

        if (club.HasAwayKit)
        {
            return new KitRecommendation(club.AwayKitDescription, club.AwayKitPrimaryColorHex, club.AwayKitSecondaryColorHex, true, true);
        }

        // Hay coincidencia pero no tenemos segunda equipación configurada: se avisa igual con la titular.
        return home with { IsClash = true };
    }

    public static bool AreSimilar(string? hexA, string? hexB)
    {
        var a = ParseHex(hexA);
        var b = ParseHex(hexB);
        if (a == null || b == null) return false;

        var dr = a.Value.R - b.Value.R;
        var dg = a.Value.G - b.Value.G;
        var db = a.Value.B - b.Value.B;
        var distance = Math.Sqrt(dr * dr + dg * dg + db * db);
        return distance < ClashThreshold;
    }

    private static (int R, int G, int B)? ParseHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        hex = hex.Trim().TrimStart('#');
        if (hex.Length != 6) return null;
        try
        {
            var r = Convert.ToInt32(hex.Substring(0, 2), 16);
            var g = Convert.ToInt32(hex.Substring(2, 2), 16);
            var b = Convert.ToInt32(hex.Substring(4, 2), 16);
            return (r, g, b);
        }
        catch
        {
            return null;
        }
    }
}
