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
        ("2.15", new[]
        {
            "🏃 Nueva sección 'Entrenamientos' al final de la portada: marcá si vas o no vas al entrenamiento de la semana y mirá quiénes van.",
            "🔁 Es automático: pasada una hora del inicio, la tarjeta salta sola al mismo día de la semana siguiente y las respuestas arrancan de cero.",
            "⚙️ El Admin configura el día y la hora en Admin → Club y Liga → Días de Entrenamiento (o desde el botón Configurar de la portada). Se pueden cargar varios días y activar o desactivar cada uno.",
            "👕 En el próximo partido, al lado del nombre de cada equipo se ven los colores de su camiseta (la nuestra, la que toca ese día).",
            "✅ En el Fixture, el staff puede tildar 'Jugamos con la segunda equipación' en cada partido por jugar. La portada y la convocatoria de WhatsApp lo respetan.",
        }),
        ("2.14", new[]
        {
            "⚽ La tabla de posiciones ahora muestra los goles a favor y en contra de cada equipo (GF y GC), y la diferencia de gol vuelve a verse en todas las pantallas.",
            "📱 En el móvil, para que la tabla entre en una línea, ganados/empatados/perdidos y goles se muestran compactos como G/E/P (8/2/2) y GF-GC (27-12).",
        }),
        ("2.13", new[]
        {
            "⚖️ El mensaje de WhatsApp 'Caja' ahora muestra, por cada persona, cuánto recibió y cuánto pagó de su bolsillo, con el saldo (Debe/Haber) entre los dos.",
            "🧑‍🤝‍🧑 Nueva pestaña 'Por Persona' en Pagos: el saldo de cada quien maneja caja, con el detalle de lo que cobró y lo que pagó al tocarlo.",
            "🏦 Los gastos pagados con fondos del club (sin una persona responsable, como el remanente de años anteriores) ahora se ven desglosados aparte.",
        }),
        ("2.12", new[]
        {
            "💰 En Pagos → Aportes se pueden cargar ingresos del club no asociados a un jugador (remanente, patrocinio, rifa...), con su propio resumen 'Otros Ingresos'.",
            "🧑‍💼 En la ficha de cada jugador se puede marcar quién está habilitado para 'recibir cobros', y elegirlo al registrar un pago o un gasto.",
            "✏️ Los cobros y los gastos ya cargados ahora se pueden editar, no solo borrar y volver a cargar.",
            "🧾 En Gastos: nuevas categorías Sanciones y Fichas, y un campo de observación libre cuando se elige 'Otros'.",
            "📊 Nueva pestaña Presupuesto en Pagos: presupuesto anual estimado por categoría comparado con lo gastado, y qué % representa cada rubro sobre el total.",
            "💬 Nuevos mensajes de WhatsApp (solo Admin/Tesorero) para compartir la Caja del año y la ejecución del presupuesto.",
            "🔐 El Tesorero ya puede editar el presupuesto anual él mismo (antes esa parte quedaba reservada solo al Admin).",
        }),
        ("2.11.1", new[]
        {
            "🐛 Al 'Cargar pago a este jugador' desde Pagos → Aportes ahora sí abre con ese jugador ya elegido (antes no lo traía).",
        }),
        ("2.11", new[]
        {
            "🔄 En 'Próximo partido' el orden de local/visitante ahora coincide con el del Fixture (antes siempre aparecíamos arriba).",
            "📋 Nuevo botón 'Ver Acta' en los partidos finalizados: resumen de goleadores, asistencias, MVP y tarjetas sin entrar a editar.",
            "📊 En Estadísticas (Goleadores, Asistencias, Tarjetas, MVP), tocá un jugador para ver el detalle partido por partido.",
        }),
        ("2.10", new[]
        {
            "📅 Fixture completo de la liga cargado (las 30 jornadas), con los escudos de cada club y su cancha.",
            "🔎 En el Fixture hay un filtro nuevo: Próximos / Finalizados / Todos, para no scrollear los partidos ya jugados.",
            "🛡️ Los escudos de los rivales se ven en la tarjeta del próximo partido, en la lista de Equipos y en la tabla de Posiciones.",
            "💶 En la ficha de cada jugador se puede marcar si cuenta o no para la cuota de temporada (cartera de pagos).",
            "📊 Las encuestas del equipo muestran qué porcentaje de la plantilla respondió.",
            "✏️ Al editar un equipo rival, el nombre y la cancha se actualizan solos en todos sus partidos.",
        }),
        ("2.9", new[]
        {
            "🕐 Los partidos pueden marcarse como 'A confirmar' o 'Confirmada' junto al horario, para no confundir una hora provisional con la definitiva.",
            "🟢 Pizarra táctica: ahora solo el DT (o el Admin) puede armarla, y queda marcada como borrador hasta que él la confirma.",
            "🔑 Corregido un error que a veces mostraba 'no tienes permiso' al guardar cambios estando logueado.",
        }),
        ("2.8", new[]
        {
            "📍 Los equipos rivales ahora tienen su propia cancha cargada (Equipos → Editar). Al armar un partido de visitante, la ubicación aparece sola.",
            "🧹 Se sacaron los atajos de canchas fijas al crear un partido: ahora propone la nuestra de local o la del rival de visitante.",
        }),
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
