# 📖 DOCUMENTACIÓN TÉCNICA INTEGRAL - ATLÈTIC POBLENOU (A.P.N.)

Este documento contiene la arquitectura completa, modelos de datos, reglas de negocio, conexiones a servicios externos, soluciones a errores históricos y guías operativas del proyecto **Atlètic Poblenou PWA**. Está redactado para que cualquier desarrollador o agente de Inteligencia Artificial pueda comprender el sistema y continuar su mantenimiento y evolución de manera inmediata.

---

## 1. Visión General del Proyecto

- **Nombre:** Atlètic Poblenou (A.P.N. Veteranos)
- **Propósito:** Plataforma web y móvil (PWA) para la gestión completa del equipo de fútbol veterano (convocatorias y asistencia a partidos, cobro y seguimiento de cuotas por Bizum/efectivo, caja común de gastos, roles de la plantilla, tablón de comunicados oficiales con encuestas interactivas y estadísticas).
- **Entorno de Producción:** [https://pitu1386.github.io/AppPoblenou/](https://pitu1386.github.io/AppPoblenou/)
- **Repositorio Git:** `https://github.com/pitu1386/AppPoblenou.git`
  - Rama de código fuente: `main`
  - Rama de publicación / hosting: `gh-pages`
- **Versión Activa:** `v2.3`

---

## 2. Stack Tecnológico

| Capa | Tecnología | Notas |
|---|---|---|
| **Frontend / Lógica** | C# / Blazor WebAssembly (.NET 10) | Single Page Application cliente (WASM). |
| **Estilos / UI** | Tailwind CSS (vía CDN) + Tokens semánticos en `css/app.css` | Soporte Dark/Light Theme nativo (`apnTheme`). |
| **Backend & Base de Datos** | Supabase (PostgreSQL 15 en la nube) | Conectado vía REST API v1 (`PostgREST`). |
| **Almacenamiento Local** | `localStorage` del navegador vía JS Interop | Caché offline y arranque inmediato. |
| **PWA & Offline** | Service Worker (`service-worker.published.js`) con PWA manifest | Instalable en iOS y Android. |
| **Despliegue** | Script PowerShell automatizado (`deploy.ps1`) | Publica a GitHub Pages en la rama `gh-pages`. |

---

## 3. Conexión y Backend (Supabase Cloud)

La aplicación utiliza un backend PostgreSQL gestionado en Supabase a través de llamadas REST (`HttpClient` autenticado).

### 3.1. Credenciales y Endpoint
- **URL Base:** `https://dlajpiuuslegmoedslux.supabase.co/rest/v1`
- **Clave Pública Anon:** `sb_publishable_2jgFAT8ePAK6BJOyPDUImA_-BC8NXjq`
- **Servicio C# responsable:** [`AtleticPoblenou/Services/SupabaseClientService.cs`](file:///c:/Desarrollos/AppPoblenou/AtleticPoblenou/Services/SupabaseClientService.cs)

### 3.2. Tablas en Supabase
1. `profiles`: Datos de los jugadores (nombre, apodo, dorsal, posición, pierna hábil, rol, capitán, teléfono, email, contraseña hash/plana, estado activo).
2. `matches`: Fixture de partidos de la liga, fecha/hora, rival, local/visitante, marcador, estado (0: Por jugar, 1: Finalizado, 2: Suspendido), notas y jornada (`round`).
3. `attendance`: Asistencia a cada partido vinculada por `match_id` y `player_id` (0: Asiste, 1: No asiste, 2: Duda, notas de justificación).
4. `payments`: Registro de cuotas por jugador (concepto, importe, estado 0: Pendiente / 1: Pagado, fecha de pago, método: Bizum / Efectivo / Transferencia).
5. `team_expenses`: Gastos de la caja común (concepto, importe, fecha, categoría: canchas, árbitros, tercer tiempo, material, pagado por quién).
6. `match_events`: Goles, asistencias, tarjetas amarillas/rojas y MVP asociados a un partido y jugador.
7. `rival_teams`: Equipos rivales de la liga (nombre, colores de equipación, notas).
8. `announcements`: Comunicados del club, avisos fijados (`is_pinned`), encuestas con opciones y votos por ID de jugador.
9. `club_settings`: Configuración única del club (ID `'current'`), nombre, colores, cancha habitual, cuota por temporada (`season_fee_per_player`), código secreto de acceso (`team_secret_code`) e historial de temporadas archivadas.

### 3.3. Scripts SQL de Referencia en el Repositorio
- [`supabase_schema.sql`](file:///c:/Desarrollos/AppPoblenou/supabase_schema.sql): Esquema DDL completo para creación y estructura de tablas.
- [`habilitar_permisos_supabase.sql`](file:///c:/Desarrollos/AppPoblenou/habilitar_permisos_supabase.sql): Desactiva RLS (`DISABLE ROW LEVEL SECURITY`) y otorga permisos `GRANT ALL` para roles `anon` y `authenticated`.
- [`bloquear_partidos_mock.sql`](file:///c:/Desarrollos/AppPoblenou/bloquear_partidos_mock.sql): Purga de partidos demo antiguos y creación de restricción estricta de PostgreSQL:
  ```sql
  ALTER TABLE public.matches ADD CONSTRAINT check_no_mock_matches CHECK (id NOT IN ('match-1', 'match-2'));
  ```

---

## 4. Arquitectura de Sincronización (Dual-Layer)

La app utiliza un modelo híbrido para máxima velocidad y funcionamiento offline:

1. **Lectura en arranque ([`TeamDataService.cs -> InitializeAsync`](file:///c:/Desarrollos/AppPoblenou/AtleticPoblenou/Services/TeamDataService.cs)):**
   - Primero lee instantáneamente desde `localStorage` (`apn_profiles`, `apn_matches`, `apn_club_settings`, etc.) para pintar la pantalla en 0 milisegundos.
   - En paralelo, lanza peticiones `GET` a Supabase para obtener el estado real y más reciente de la nube.
   - Al recibir los datos de Supabase, actualiza la memoria, sobreescribe el `localStorage` con la verdad del servidor y notifica a los componentes con `NotifyStateChanged()`.

2. **Escritura y Mutaciones:**
   - Cada acción de guardado escribe inmediatamente en `localStorage` y envía un `POST` con `Prefer: resolution=merge-duplicates` (Upsert idempotente) o `DELETE` a Supabase.

---

## 5. Reglas de Negocio y Seguridad Críticas

### 5.1. Blindaje del Administrador Principal (Pitu)
- **Identificación:** Usuario con ID `user-1`, apodo `pitu1386`, nombre `Pitu` o email que contenga `pitu1386`.
- **Regla Intocable:** Pitu **SIEMPRE** debe mantener el rol `UserRole.Admin`.
  - No puede ser degradado en [`ManageRolesModal.razor`](file:///c:/Desarrollos/AppPoblenou/AtleticPoblenou/Components/ManageRolesModal.razor).
  - No puede ser dado de baja ni eliminado en [`PlayerSheetModal.razor`](file:///c:/Desarrollos/AppPoblenou/AtleticPoblenou/Components/PlayerSheetModal.razor) ni en `TeamDataService.DeleteProfileAsync`.
  - La función guardiana en el backend de servicios es `TeamService.IsOwnerAdmin(UserProfile? p)`.
- **Desacople de Capitanía:** El usuario Pitu **NO** es obligatoriamente el capitán del equipo. La propiedad `IsCaptain` es un booleano editable libremente para cualquier jugador sin afectar sus permisos de administrador.

### 5.2. Sistema de Roles de Usuario
Enum `UserRole` en [`AtleticPoblenou/Models/TeamModels.cs`](file:///c:/Desarrollos/AppPoblenou/AtleticPoblenou/Models/TeamModels.cs):
- `Admin (0)`: Acceso total al club, ajustes, finanzas, borrado de partidos, gestión de roles y base de datos.
- `Treasurer (1)`: Tesorero (cobro de cuotas de temporada, registro de pagos y control de gastos de caja).
- `FieldManager (2)`: Delegado de equipo (gestión de fixture, canchas, planillas y resultados de partidos).
- `Player (3)`: Jugador estándar (confirmación de asistencia, visualización de estadísticas y comunicados).
- `Coach (4)`: Director Técnico / DT (convocatorias tácticas, alineaciones y actas de partido).

### 5.3. Código Secreto de Equipo (`team_secret_code`)
- Almacenado en Supabase en `club_settings.team_secret_code`.
- Es el código de seguridad que un nuevo jugador debe introducir obligatoriamente para crear su cuenta (`RegisterAsync`) o para reactivar una cuenta dada de baja (`ReactivateWithCodeAsync`).
- Al hacer clic en *"Regenerar código"* en el panel de Admin, se genera un nuevo formato `APN-XXXX`, se persiste inmediatamente en Supabase con `SaveClubSettingsAsync` y se sincroniza en todos los dispositivos.

---

## 6. Problemas Históricos Resueltos y Trampas a Evitar

> [!CAUTION]
> **No alterar las siguientes defensas** para evitar regresiones críticas:

1. **La "Resurrección" de partidos borrados (`match-1` FONTETAS / `match-2` Atletic Poblenou):**
   - *Causa del bug histórico:* Antes, si Supabase tenía 0 partidos, el cliente interpretaba que la base de datos estaba vacía y re-subía los partidos mock que venían predefinidos en memoria.
   - *Defensa instalada en 4 niveles:*
     1. **`bloquear_partidos_mock.sql`:** Restricción en PostgreSQL que rechaza inserciones de esos IDs.
     2. **[`SupabaseClientService.cs`](file:///c:/Desarrollos/AppPoblenou/AtleticPoblenou/Services/SupabaseClientService.cs):** Filtro en `UpsertMatchAsync` y `UpsertMatchesBatchAsync` que descarta `match-1`, `match-2` y `FONTETAS`. Si la lista queda vacía, no hace petición HTTP.
     3. **[`TeamDataService.cs`](file:///c:/Desarrollos/AppPoblenou/AtleticPoblenou/Services/TeamDataService.cs):** `_matches` y `_attendance` arrancan vacíos (`new()`), y nunca se re-siembran datos si el servidor retorna lista vacía.
     4. **[`index.html`](file:///c:/Desarrollos/AppPoblenou/AtleticPoblenou/wwwroot/index.html):** Script de purga al inicio del `<head>` que borra del `localStorage` cualquier residuo antiguo.

2. **Caché persistente del Service Worker en navegadores:**
   - La app utiliza un Service Worker PWA con `self.skipWaiting()` y `clients.claim()`.
   - Si un usuario tiene abierta una pestaña vieja, la app cuenta con un botón en la pantalla de Login y en el encabezado: *"Actualizar app (Borrar caché)"* que ejecuta `window.forceAppUpdate()`, desregistrando los service workers y borrando las `caches` del navegador antes de recargar.

---

## 7. Control de Versiones y Visibilidad

Para comprobar en cualquier momento qué versión exacta tiene cargada el navegador del usuario:
1. **Archivo de proyecto:** `AtleticPoblenou.csproj` (`<Version>2.3.0</Version>`).
2. **Pantalla de Login:** Pie inferior en [`LoginView.razor`](file:///c:/Desarrollos/AppPoblenou/AtleticPoblenou/Components/LoginView.razor):
   `<span class="font-cond font-bold tracking-wider">v2.3 · ATLETIC POBLENOU</span>`
3. **Menú de Usuario:** Al pulsar el avatar en [`AppHeader.razor`](file:///c:/Desarrollos/AppPoblenou/AtleticPoblenou/Components/AppHeader.razor), al pie del menú desplegable:
   `<span class="text-[10px] text-muted font-cond font-bold tracking-wider">v2.3 · ATLETIC POBLENOU</span>`

---

## 8. Guía de Ejecución y Despliegue

### 8.1. Ejecutar en Local (Desarrollo)
En una terminal PowerShell desde la raíz:
```powershell
dotnet run --project .\AtleticPoblenou\AtleticPoblenou.csproj --urls "http://localhost:5091"
```
Abrir `http://localhost:5091`.

### 8.2. Desplegar en Producción (GitHub Pages)
Ejecutar el script automatizado:
```powershell
powershell -File .\deploy.ps1
```
El script realiza automáticamente:
1. `dotnet publish -c Release -o .\publish_output`
2. Genera el archivo `.nojekyll` para GitHub Pages.
3. Copia `blazor.webassembly.js` y `dotnet.js` para compatibilidad de rutas.
4. Ajusta `<base href="/AppPoblenou/" />` en `index.html` y genera `404.html` para el routing SPA de Blazor.
5. Realiza un `git push -f origin gh-pages` desde un directorio temporal aislado.

---

## 9. Mapa de Archivos Principales del Proyecto

- `AtleticPoblenou/Program.cs`: Registro de servicios de DI (`ITeamDataService`, `SupabaseClientService`, `ThemeService`).
- `AtleticPoblenou/Models/TeamModels.cs`: Todas las clases de datos (`UserProfile`, `Match`, `Attendance`, `Payment`, `ClubSettings`, enums de roles y posiciones).
- `AtleticPoblenou/Services/SupabaseClientService.cs`: Cliente HTTP directo con serialización/deserialización DTOs para Supabase.
- `AtleticPoblenou/Services/TeamDataService.cs`: Núcleo de lógica de negocio, caché local, reactividad y llamadas a Supabase.
- `AtleticPoblenou/Components/AppHeader.razor`: Barra superior con escudo, tema Dark/Light, menú de usuario, cambio de rol y versión.
- `AtleticPoblenou/Components/MatchesTab.razor`: Pestaña de partidos, botón de vaciado de fixture, detalle de partido y filtros.
- `AtleticPoblenou/Components/SquadTab.razor`: Plantilla del equipo organizada por líneas (porteros, defensas, medios, delanteros, cuerpo técnico).
- `AtleticPoblenou/Components/PaymentsTab.razor`: Balances económicos, estado de cuotas de temporada, cobros Bizum y caja común.
- `AtleticPoblenou/Components/AdminTab.razor`: Panel de administración del club, gestión de roles, código de equipo y temporadas.
- `AtleticPoblenou/Components/LoginView.razor`: Pantalla de autenticación, alta con código de equipo y forzado de actualización.
- `deploy.ps1`: Script PowerShell de build y publicación a GitHub Pages.
