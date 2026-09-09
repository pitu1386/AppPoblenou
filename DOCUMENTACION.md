# 📖 DOCUMENTACIÓN TÉCNICA INTEGRAL - ATLÈTIC POBLENOU (A.P.N.)

Este documento contiene la arquitectura, modelos de datos, reglas de negocio, seguridad, sincronización y guías operativas del proyecto **Atlètic Poblenou PWA**. Está redactado para que cualquier desarrollador o agente de IA pueda continuar el mantenimiento de inmediato.

---

## 1. Visión General del Proyecto

- **Nombre:** Atlètic Poblenou - App de Veteranos
- **Propósito:** PWA para la gestión del equipo de fútbol veterano: convocatorias y asistencia, cuotas y caja común, roles de plantilla, tablón con encuestas, resultados, clasificación y estadísticas.
- **Producción:** [https://pitu1386.github.io/AppPoblenou/](https://pitu1386.github.io/AppPoblenou/)
- **Repositorio:** `https://github.com/pitu1386/AppPoblenou.git` (código en `main`, hosting en `gh-pages`)
- **Versión activa:** `v2.7.1` (única fuente: `<Version>` en `AtleticPoblenou.csproj`, expuesta por `AppInfo.Version`)
- **Regla:** cualquier cambio que se despliegue a producción sube el número de versión (al menos el minor, `2.X.0`) en `AtleticPoblenou.csproj` y en `AtleticPoblenou/package.json`, y añade una entrada nueva al principio de `AppInfo.ReleaseNotes`. Sin esto, `WhatsNewModal` no tiene nada que avisar y el club no distingue una versión de otra.

---

## 2. Stack Tecnológico

| Capa | Tecnología | Notas |
|---|---|---|
| Frontend / Lógica | C# / Blazor WebAssembly (.NET 10) | SPA cliente (WASM). |
| Estilos | Tailwind CSS 3 compilado con su CLI + tokens semánticos en `wwwroot/css/app.css` | Tema claro/oscuro con clase `dark` en `<html>` (`apnTheme`). |
| Autenticación | Supabase Auth (GoTrue) vía REST | Contraseñas hasheadas en el servidor. Sesión en `localStorage` (`apn_session`). |
| Base de datos | Supabase (PostgreSQL) vía PostgREST | Row Level Security por rol. |
| Tiempo real | Supabase Realtime (`supabase-js` por CDN, solo para el canal) | Avisa a C# de cambios por tabla. |
| Caché local | `localStorage` (`apn2_*`) | Pintado instantáneo al arrancar; la verdad está en la nube. |
| PWA | Service Worker (`service-worker.published.js`) + manifest | Instalable en iOS y Android; área segura respetada (notch / Dynamic Island). |
| Notificaciones | Web Push (VAPID) vía Edge Function `send-push` | `PushService.cs` + puente `apnPush` en `index.html`. En iPhone requiere la app instalada en pantalla de inicio (iOS 16.4+). |
| Despliegue | `deploy.ps1` | Compila Tailwind, publica y hace push a `gh-pages`. |

---

## 3. Backend: Supabase

### 3.1. Proyecto y claves
- **URL:** `https://dlajpiuuslegmoedslux.supabase.co` (constante `AppInfo.SupabaseUrl`)
- **Clave pública (anon):** en `AppInfo.SupabaseAnonKey`. Es pública por diseño: viaja en el WASM. Solo permite lo que autorizan las políticas RLS y las funciones RPC expuestas a `anon` (buscar email por apodo y validar el código de equipo).

### 3.2. Scripts SQL
- `supabase_schema.sql`: crea las tablas desde cero (borra las existentes) con el perfil del admin principal, los rivales y la configuración del club.
- `migracion_auth_rls.sql`: **idempotente**. Migra usuarios a Supabase Auth, enlaza `profiles.auth_uid`, elimina la columna `password`, activa RLS con políticas por rol, crea triggers de protección, funciones RPC y añade las tablas a Realtime. Se ejecuta tanto en una instalación nueva (después del esquema) como sobre una base v2.3. Incluye `matches.match_kind` (v2.6).
- `push_setup.sql`: **opcional, setup único** (no es idempotente para re-ejecutar con datos reales adentro). Crea `push_subscriptions`, el trigger de comunicados y el cron del recordatorio de partido. Guía completa en [`PUSH_SETUP.md`](./PUSH_SETUP.md). **Nunca commitear este archivo con el secreto real relleno** — se edita localmente, se ejecuta en el SQL Editor, y antes de cualquier commit se revierte a `PONE_UN_VALOR_RANDOM_LARGO_ACA`.

Requisito en el Dashboard: **Authentication → Providers → Email → "Confirm email" desactivado**. Si está activado, el alta crea la cuenta pero no inicia sesión; la app guarda la ficha pendiente y la completa en el primer login.

### 3.3. Tablas
1. `profiles`: ficha de jugador. `id` texto (para cuentas nuevas coincide con el UID de Auth; el admin principal es `user-1`), `auth_uid` UUID único, datos deportivos y personales, `role`, `is_captain`, `is_active`. Sin contraseña.
2. `matches`: fixture (`round`, fecha, rival, local/visitante, marcador, `status` 0 Por jugar / 1 Finalizado / 2 Suspendido, notas, `match_kind` 'liga' / 'copa' / 'amistoso', `reminder_sent_at` para el cron de recordatorio push). Los partidos ajenos de la liga se guardan con `notes = 'LM|homeId|homeName|awayId|awayName'`.
3. `attendance`: asistencia por `match_id` + `player_id` (único). 0 Asiste / 1 No asiste / 2 Duda.
4. `payments`: cuotas por jugador (0 Pendiente / 1 Pagado; método 0 Bizum / 1 Efectivo / 2 Transferencia).
5. `team_expenses`: gastos de caja (`category` texto, `paid_by`).
6. `match_events`: goles, asistencias, tarjetas y MVP.
7. `rival_teams`: rivales de la liga.
8. `announcements`: comunicados, fijados y encuestas (`votes` JSONB `{playerId: opción}`).
9. `club_settings`: fila única `'current'` con identidad del club, cuota, `team_secret_code`, la segunda equipación (`away_kit_*`) e historial de temporadas.
10. `match_lineups`: alineación de la pizarra táctica por partido (`match_id` es la clave primaria). `formation` y `starting_player_ids` (JSONB, 11 huecos en orden; `null` = hueco vacío). El banquillo no se guarda: se recalcula cada vez a partir de la asistencia confirmada menos quien ya está en la XI.
11. `push_subscriptions` *(script separado `push_setup.sql`, v2.7)*: una fila por navegador/dispositivo suscripto (`profile_id`, `endpoint`, `p256dh`, `auth`). RLS propia: cada jugador solo ve/crea/borra sus propias suscripciones (no sigue el patrón staff/tesorería del resto).

### 3.4. Permisos (RLS)
Funciones de apoyo: `my_profile_id()`, `my_role()`, `is_member()`, `is_admin()` (rol 0), `is_staff()` (0, 2 Delegado, 4 DT), `is_treasury()` (0, 1 Tesorero).

| Tabla | Lectura | Escritura |
|---|---|---|
| profiles | miembros activos (y la propia ficha aunque esté de baja) | propia ficha o admin; alta solo vía `register_profile()`; borrado admin |
| matches, rival_teams, match_events | miembros | staff |
| attendance | miembros | propia fila o staff |
| payments, team_expenses | miembros | tesorería |
| announcements | miembros | staff (votos vía `vote_poll()`) |
| club_settings | miembros | admin |
| match_lineups | miembros | staff |
| push_subscriptions | propia fila | propia fila (insert/select/delete; sin update) |

Triggers en `profiles`: `protect_owner_admin` (fuerza Admin y activo en `user-1`, impide su borrado) y `prevent_privilege_escalation` (un no-admin no puede cambiar rol, capitanía, estado, email ni `auth_uid`).

### 3.5. Funciones RPC
| Función | Quién | Para qué |
|---|---|---|
| `lookup_login_email(identifier)` | anon | Login por apodo o nombre: devuelve el email de la cuenta. |
| `validate_team_code(p_code)` | anon | Comprueba el código antes de crear la cuenta. |
| `register_profile(...)` | autenticado | Crea la ficha del usuario logueado validando el código. Si no hay ningún admin activo, el primero lo es. |
| `reactivate_with_code(p_team_code)` | autenticado | Reactiva la propia ficha dada de baja. |
| `admin_set_password(p_profile_id, p_new_password)` | admin | Restablece la contraseña de otro jugador. |
| `vote_poll(p_announcement_id, p_option)` | autenticado | Guarda solo el voto propio en el JSON. |
| `clear_all_matches()` | staff | Vacía fixture, asistencias y eventos. |
| `close_season(p_archive, p_new_season_name, p_new_fee)` | admin | Archiva la temporada y limpia partidos, asistencias, eventos y cobros en una transacción. |

---

## 4. Arquitectura del Cliente

### 4.1. Servicios (`AtleticPoblenou/Services`)
- `AppInfo`: versión y constantes de Supabase.
- `SupabaseAuthService`: signup, login con contraseña, refresh automático del token, cambio de contraseña, logout. Persiste la sesión en `localStorage`.
- `SupabaseClientService`: cliente PostgREST genérico (`GetAsync<T>`, `UpsertAsync<T>`, `DeleteAsync`, `RpcAsync`) con envoltorios tipados por tabla. Lanza `SupabaseException` con mensaje apto para el usuario.
- `SupabaseDtos`: DTOs con nombres de columna y mapeadores modelo ↔ DTO.
- `TeamDataService`: estado en memoria + caché, autenticación de alto nivel, mutaciones y sincronización. Expone `OnChange` (datos) y `OnError` (fallos de nube, mostrados como aviso en `Home.razor`).
- `WeatherService`: previsión de Open-Meteo. Si no hay datos, `IsAvailable = false` y la UI lo indica; nunca inventa valores.
- `ThemeService`: tema claro/oscuro.
- `PushService`: suscribe el navegador a Web Push, guarda la suscripción en `push_subscriptions`, expone `IsSupported`/`Permission`/`IsEnabled`. El envío real lo hace la Edge Function `send-push` (firma VAPID), invocada por trigger (comunicado nuevo), por el staff a mano, o por `pg_cron` (recordatorio de partido).

### 4.2. Ciclo de sincronización
1. **Arranque:** se carga la sesión; si existe, se pinta desde la caché `apn2_*` y en paralelo se leen las diez tablas de `TeamDataService.Tables` (`push_subscriptions` queda afuera: la gestiona `PushService` directo). Cada tabla que llega sustituye su copia local y su caché.
2. **Mutación:** cambio optimista en memoria → escritura en la nube de **solo la fila afectada** (o RPC) → relectura de la(s) tabla(s) implicada(s). Si la nube rechaza, `OnError` muestra el motivo y la relectura deshace el cambio local. No se hacen upserts de listas completas.
3. **Realtime:** `window.apnRealtime` (index.html) se suscribe a `postgres_changes` de todo el esquema `public` con el token del usuario y llama a `TeamDataService.OnCloudChange(tabla)`, que relee esa tabla. Al volver la pestaña a primer plano o recuperar la conexión se relee todo.
4. **Sin conexión:** la app muestra la caché y avisa de cada escritura fallida. No hay cola de escrituras offline.

### 4.3. Estados de sesión (`ITeamDataService`)
- `IsAuthenticated`: sesión + ficha + ficha activa. Muestra la app.
- `NeedsProfile`: sesión sin ficha (alta a medias). `LoginView` muestra el formulario para completarla.
- `IsDeactivated`: sesión con ficha dada de baja. `LoginView` pide el código de equipo.

---

## 5. Reglas de Negocio

### 5.1. Administrador principal
- Es el perfil con `id = 'user-1'`. En cliente `IsOwnerAdmin(p)` compara **solo el ID**; en servidor lo blinda el trigger `protect_owner_admin`.
- La capitanía (`IsCaptain`) es independiente del rol.

### 5.2. Roles (`UserRole`)
`Admin (0)` todo · `Treasurer (1)` cobros y caja · `FieldManager (2)` fixture, canchas y actas · `Player (3)` asistencia, estadísticas y comunicados · `Coach (4)` convocatorias, alineaciones y actas.

### 5.3. Código de equipo
- Vive en `club_settings.team_secret_code`. Se regenera desde el panel Admin con formato `APN-XXXX` y se guarda en la nube.
- Lo valida el servidor (`validate_team_code`, `register_profile`, `reactivate_with_code`). Se compara sin guiones y sin distinguir mayúsculas.

### 5.4. Altas y bajas
- Alta: el jugador crea cuenta con email y contraseña (mínimo 6 caracteres) y código de equipo. La ficha se crea con `register_profile`.
- Baja: el admin marca `is_active = false`. El jugador sigue pudiendo iniciar sesión pero solo ve la pantalla de reactivación.
- Contraseña olvidada: un admin la restablece desde la ficha del jugador (`admin_set_password`).

### 5.5. Temporadas
`CloseSeasonAndStartNewAsync` calcula el resumen en cliente y llama a `close_season`, que archiva en `club_settings.season_history` y borra partidos, asistencias, eventos y cobros en una sola transacción. Los perfiles y rivales se conservan.

### 5.6. Tipo de competición (`Match.Kind`, v2.6)
Cada partido es `Liga`, `Copa` o `Amistoso` (`matches.match_kind`). El amistoso aparece en el fixture pero **no** computa en ninguna tabla de posiciones (`CountsForStandings => Kind != Amistoso`). Copa tiene su propio fixture (filtro por competición) y su propia tabla, armada solo con los equipos que la jugaron. El tipo se puede cambiar desde el fixture aunque el partido ya esté jugado, sin perder el marcador ni el acta. "Cargar Jornada" (carga rápida de resultados) queda restringido a partidos de Liga.

### 5.7. Notificaciones push (v2.7)
Disparadores: comunicado nuevo (trigger automático en `announcements`), aviso manual del staff (Admin → Comunicados → "Enviar notificación"), recordatorio de partido el día antes (`pg_cron` cada hora, marca `matches.reminder_sent_at` para no repetir), y un toque directo a quien no confirmó asistencia (filtro "Sin responder"). Cada jugador activa/desactiva las suyas desde el menú de su perfil; en iPhone requiere tener la app instalada en la pantalla de inicio (iOS 16.4+). Puesta en marcha completa (Edge Function, secretos, extensiones) en [`PUSH_SETUP.md`](./PUSH_SETUP.md).

---

## 6. Historial de problemas resueltos

1. **Resurrección de partidos de prueba (v2.2):** el cliente re-subía datos por defecto cuando la nube devolvía vacío y hacía upserts de listas completas desde cachés viejas. En v2.4 no existen datos por defecto en el cliente, las escrituras son por fila y las lecturas siempre sustituyen la copia local. La restricción `check_no_mock_matches` se mantiene en el esquema.
2. **Cierre de temporada que no limpiaba la nube:** ahora lo hace `close_season` en el servidor.
3. **Rival FONTETAS inmortal / perfiles ficticios:** eliminados los datos semilla del cliente. Los rivales iniciales solo están en `supabase_schema.sql`.
4. **Seguridad (v2.3 y anteriores):** base abierta con `anon`, contraseñas en claro y contraseña maestra `1234`. Sustituido por Supabase Auth + RLS. La migración conserva las contraseñas antiguas hasheándolas; quien no tuviera contraseña recibe `1234` y debe cambiarla.
5. **Caché del Service Worker:** botón *Actualizar app (Borrar caché)* en login y cabecera (`window.forceAppUpdate`).
6. **Clasificación vacía:** `GetStandings` no añadía filas a la tabla. Ahora parte de nuestro equipo y todos los rivales.
7. **Pizarra táctica sin guardar, ciega a las posiciones, con gente que no confirmó y sin forma de revisarla después:** `TacticalBoardModal` no persistía nada (se perdía al cerrar el modal), el reparto automático tomaba los primeros 11 jugadores sin mirar su posición real, si faltaba gente de una posición completaba con cualquiera de la plantilla aunque no hubiera confirmado que va, y el único botón para abrirla apuntaba siempre a `NextMatch` (imposible volver a ver la alineación de un partido ya jugado). Ahora la alineación se guarda en `match_lineups` en cada cambio y se recarga al reabrir; el pool de titulares y banquillo se arma solo con quienes tienen asistencia confirmada (`AttendanceStatus.Going`, sin cuerpo técnico); el reparto automático (al abrir el partido o al cambiar de dibujo) asigna cada hueco según la posición real del jugador dentro de ese pool (`AutoAssignByPosition`), dejando el hueco vacío si no hay nadie confirmado de esa posición; y cada partido del fixture (`MatchesTab`) tiene su propio botón "Ver Alineación" que abre la pizarra para ESE partido, no solo el próximo. Un partido `Finished`, o quien no es staff (`CanEdit="false"`), la ve de solo lectura; si el partido ya se jugó y nunca se guardó nada, se muestra un estado vacío explícito en vez de inventar una sugerencia sobre un partido que ya pasó.
8. **Acta de partido que no releía lo guardado, con un campo de goles por jugador poco práctico:** `RecordResultModal` (nunca tocado desde el scaffold original) no recargaba goleadores, asistencias, tarjetas ni MVP al editar un acta ya guardada — arrancaba siempre en blanco. Además, cargar un hat-trick exigía repetir una fila entera por cada gol. Ahora `LoadFromMatch` reconstruye todo desde `GetMatchEvents()` al abrir el modal, y cada fila de gol tiene un campo de **cantidad** (`GoalEntry.Goals`): con más de un gol en la fila no se pide minuto ni asistencia individual (no tiene sentido para varios goles distintos). Para emparejar una asistencia con su gol al recargar, se usa `MatchEvent.Minute` como una clave interna de correlación (índice de fila al guardar) — nunca fue ni es un minuto real, no hay ningún campo de minuto visible en la UI.
9. **Fixture que no se refrescaba solo al editar un resultado:** `MatchesTab` era el único de los componentes principales que no se suscribía directamente a `TeamService.OnChange` (sí lo hacen `BottomNav` y el resto de las pestañas); dependía por completo de que el padre (`Home.razor`) volviera a dibujar por otro motivo. Se agregó la suscripción propia (`OnInitialized`/`Dispose`) y un `@key="match.Id"` en el `@foreach` del fixture para que Blazor nunca confunda una fila con otra al redibujar.

---

## 7. Guía de Ejecución y Despliegue

### 7.0. Aviso de novedades (`WhatsNewModal`)
Cada navegador guarda en `localStorage` (`apn_last_seen_version`) la última versión que vio. Al entrar, si `AppInfo.Version` subió desde entonces, `WhatsNewModal.razor` muestra un modal con las entradas de `AppInfo.ReleaseNotes` posteriores a esa versión (puede mostrar varias de golpe si el jugador estuvo días sin abrir la app) y luego actualiza el valor guardado. Si nunca hubo un valor guardado (primer arranque de esta función en ese navegador), se asume `BaselineVersion = "2.4"` para no repetir novedades ya vividas y sí mostrar las de esta tanda de cambios.

### 7.1. Preparar la base de datos
1. Supabase Dashboard → SQL Editor.
2. Base nueva: ejecutar `supabase_schema.sql` y luego `migracion_auth_rls.sql`. Base existente: solo `migracion_auth_rls.sql`.
3. Authentication → Providers → Email → desactivar "Confirm email".
4. Entrar con `pitu1386` y la contraseña que tenía el perfil (o `1234` si no tenía) y cambiarla desde *Ver mi ficha → Editar*.

### 7.2. Desarrollo local
```powershell
cd .\AtleticPoblenou
npm install          # solo la primera vez
npm run build:css    # o npm run watch:css mientras editas .razor
cd ..
dotnet run --project .\AtleticPoblenou\AtleticPoblenou.csproj --urls "http://localhost:5091"
```
Con `node_modules` presente, `dotnet build` regenera `wwwroot/css/tailwind.css` automáticamente (target `TailwindBuild` en el .csproj). El archivo generado va en git para que compile sin Node.

### 7.3. Producción
```powershell
powershell -File .\deploy.ps1
```
Compila Tailwind (si hay npm), publica en Release, ajusta `<base href="/AppPoblenou/">`, genera `404.html` y `.nojekyll`, y hace `git push -f` a `gh-pages` desde un directorio temporal.

---

## 8. Mapa de Archivos

- `AtleticPoblenou/Program.cs`: registro de servicios.
- `AtleticPoblenou/Models/TeamModels.cs`: modelos y enums.
- `AtleticPoblenou/Services/*`: ver 4.1.
- `AtleticPoblenou/Pages/Home.razor`: puerta de entrada (login / app), pestañas, modales y aviso de errores de nube.
- `AtleticPoblenou/Components/LoginView.razor`: login, alta, completar ficha y reactivación.
- `AtleticPoblenou/Components/AppHeader.razor`: cabecera, tema, menú de usuario con versión y estado de nube.
- `AtleticPoblenou/Components/MatchesTab.razor`: próximo partido (con contador y aviso de camiseta), fixture (Liga/Copa/Amistoso), tabla de posiciones y rivales.
- `AtleticPoblenou/Components/RecordResultModal.razor`: acta del partido — goles (con cantidad), asistencias, tarjetas, MVP.
- `AtleticPoblenou/Components/RoundResultsModal.razor`: carga rápida de resultados de una jornada de Liga.
- `AtleticPoblenou/Components/TacticalBoardModal.razor`: pizarra táctica — arma y guarda el once, o lo muestra de solo lectura.
- `AtleticPoblenou/Components/CreateMatchModal.razor`: alta/edición de partido; avisa si la camiseta elegida choca con la del rival.
- `AtleticPoblenou/Components/PaymentsTab.razor`: cuotas y caja.
- `AtleticPoblenou/Components/AdminTab.razor`: roles, bajas, código de equipo, club, temporadas.
- `AtleticPoblenou/Components/ClubSettingsModal.razor`: identidad del club, equipación titular y alternativa.
- `AtleticPoblenou/Components/PlayerSheetModal.razor`: ficha de jugador, edición y cambio/restablecimiento de contraseña.
- `AtleticPoblenou/Components/DbConfigModal.razor`: estado de conexión y sincronización manual.
- `AtleticPoblenou/Components/WhatsNewModal.razor`: aviso de novedades por versión (ver 7.0).
- `AtleticPoblenou/Components/MatchCountdown.razor`: cuenta atrás hasta el próximo partido.
- `AtleticPoblenou/Services/KitAdvisor.cs`: compara colores propios/rivales y recomienda titular o alternativa.
- `AtleticPoblenou/Services/PushService.cs`: notificaciones push (ver 5.7).
- `AtleticPoblenou/wwwroot/index.html`: tema, service worker, puente Realtime, puente `apnPush` y helpers JS.
- `AtleticPoblenou/tailwind.config.js`, `Styles/tailwind.input.css`, `package.json`: compilación de Tailwind.
- `supabase_schema.sql`, `migracion_auth_rls.sql`: base de datos (esquema completo + migración idempotente).
- `push_setup.sql`, `PUSH_SETUP.md`, `supabase/functions/send-push/index.ts`: notificaciones push, setup separado y opcional.
- `deploy.ps1`: publicación.
