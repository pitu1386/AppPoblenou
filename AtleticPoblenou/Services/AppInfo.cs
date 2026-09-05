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

    /// <summary>
    /// Historial de novedades por versión, más nueva primero. <see cref="Components.WhatsNewModal"/> lo usa para
    /// avisar solo de lo que cambió desde la última vez que ese navegador abrió la app.
    /// </summary>
    public static readonly IReadOnlyList<(string Version, string[] Changes)> ReleaseNotes = new (string, string[])[]
    {
        ("2.5", new[]
        {
            "🟢 Pizarra táctica: ahora guarda la alineación en la nube (ya no se pierde al cerrarla) y arma el once automático solo con quienes confirmaron asistencia, respetando la posición real de cada uno.",
            "👕 Segunda equipación: se puede cargar en Admin y la app avisa sola cuándo toca usarla si el rival tiene colores parecidos a los nuestros.",
            "⏱️ Cuenta atrás para el próximo partido en la pantalla principal.",
        }),
        ("2.4", new[]
        {
            "🔒 Login con cuentas reales y contraseña propia de cada uno; se acabó la contraseña única para todos.",
            "☁️ Los cambios se sincronizan solos entre los celulares de todo el equipo.",
        }),
    };
}
