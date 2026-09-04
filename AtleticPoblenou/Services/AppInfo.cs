using System.Reflection;

namespace AtleticPoblenou.Services;

/// <summary>Única fuente de la versión visible: se lee del &lt;Version&gt; del .csproj.</summary>
public static class AppInfo
{
    public static string Version { get; } = ComputeVersion();

    private static string ComputeVersion()
    {
        var v = typeof(AppInfo).Assembly.GetName().Version;
        return v == null ? "dev" : $"{v.Major}.{v.Minor}";
    }

    public const string SupabaseUrl = "https://dlajpiuuslegmoedslux.supabase.co";
    /// <summary>Clave pública (anon). Solo da acceso a lo que permitan las políticas RLS.</summary>
    public const string SupabaseAnonKey = "sb_publishable_2jgFAT8ePAK6BJOyPDUImA_-BC8NXjq";
}
