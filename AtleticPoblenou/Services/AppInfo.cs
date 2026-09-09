using System.Reflection;

namespace AtleticPoblenou.Services;

/// <summary>Única fuente de la versión visible: se lee del &lt;Version&gt; del .csproj.</summary>
public static class AppInfo
{
    public static string Version { get; } = ComputeVersion();

    private static string ComputeVersion()
    {
        var v = typeof(AppInfo).Assembly.GetName().Version;
        if (v == null) return "dev";
        // Con parche (2.7.2) se muestra completo; en un "2.7.0" redondo se muestra corto (2.7),
        // igual que antes. Sin esto, un bugfix de parche (2.7.0 -> 2.7.1) era invisible: la app
        // seguía mostrando "v2.7" y WhatsNewModal nunca se enteraba de que había algo nuevo.
        return v.Build > 0 ? $"{v.Major}.{v.Minor}.{v.Build}" : $"{v.Major}.{v.Minor}";
    }

    public const string SupabaseUrl = "https://dlajpiuuslegmoedslux.supabase.co";
    /// <summary>Clave pública (anon). Solo da acceso a lo que permitan las políticas RLS.</summary>
    public const string SupabaseAnonKey = "sb_publishable_2jgFAT8ePAK6BJOyPDUImA_-BC8NXjq";

    /// <summary>Clave pública VAPID para Web Push (la privada vive como secreto en la Edge Function).</summary>
    public const string VapidPublicKey = "BHW224Pd_Vk2yX7O4WDyF3BCOQTaVwjNRITaYL1fsQ4wkvO6rn-I35d8yxlpk6TSl99KWhpRqK7TqvFbK4Ybe0s";

    public const string SendPushUrl = SupabaseUrl + "/functions/v1/send-push";

    /// <summary>
    /// Historial de novedades por versión, más nueva primero. <see cref="Components.WhatsNewModal"/> lo usa para
    /// avisar solo de lo que cambió desde la última vez que ese navegador abrió la app.
    /// </summary>
    public static readonly IReadOnlyList<(string Version, string[] Changes)> ReleaseNotes = new (string, string[])[]
    {
        ("2.7.2", new[]
        {
            "📝 Al editar el acta de un partido ya cargado, ahora vuelven a aparecer los goleadores, asistencias, tarjetas y el MVP (antes arrancaba en blanco).",
            "⚽ En el acta, cada gol tiene un campo de cantidad: si un jugador hizo varios, se pone el número en vez de repetir la fila.",
            "🔄 El resultado se actualiza solo en la grilla del fixture apenas lo guardás.",
        }),
        ("2.7", new[]
        {
            "🔔 Notificaciones al teléfono: activalas desde el menú de tu perfil. Te avisan de comunicados nuevos y del recordatorio del partido el día antes.",
            "📲 En iPhone hay que tener la app instalada en la pantalla de inicio para recibirlas.",
            "⚙️ El staff puede mandar una notificación directa a todo el equipo (Admin → Comunicados) y dar el toque por notificación a los que no confirmaron la convocatoria.",
        }),
        ("2.6", new[]
        {
            "🏆 Los partidos ahora se marcan como Liga, Copa o Amistoso. El amistoso se ve en el fixture pero ya no cuenta para la tabla.",
            "🥇 La copa tiene su propio fixture y su propia tabla de posiciones (pestaña Liga / Copa en Posiciones).",
            "✏️ Se pueden editar los equipos rivales (nombre y colores de camiseta), no solo crearlos y borrarlos.",
        }),
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
