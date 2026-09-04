# 🔴⚪ Atlètic Poblenou - App de Veteranos (PWA)

App de Veteranos del **Atlètic Poblenou** desarrollada con **Blazor WebAssembly (.NET 10)**, **Tailwind CSS** y **Supabase** (PostgreSQL + Auth + Realtime).

🚀 **Acceso a la App en Producción:** [https://pitu1386.github.io/AppPoblenou/](https://pitu1386.github.io/AppPoblenou/)

---

## 📚 Documentación Técnica Completa

Arquitectura, reglas de negocio, esquema de base de datos, permisos por rol y guía de migración:

👉 **[DOCUMENTACION.md](./DOCUMENTACION.md)**

---

## ⚡ Comandos Rápidos

### Primera vez (Tailwind CLI)
```powershell
cd .\AtleticPoblenou
npm install
npm run build:css
```
`wwwroot/css/tailwind.css` va versionado, así que la app compila aunque no tengas Node. Si `node_modules` existe, el CSS se regenera solo en cada `dotnet build`.

### Desarrollo Local
```powershell
dotnet run --project .\AtleticPoblenou\AtleticPoblenou.csproj --urls "http://localhost:5091"
```
Acceder a `http://localhost:5091`.

### Base de datos (Supabase)
Desde una base vacía: ejecutar `supabase_schema.sql` y después `migracion_auth_rls.sql` en el SQL Editor.
Sobre una base ya existente (v2.3 o anterior): ejecutar solo `migracion_auth_rls.sql`. Es idempotente.
En el Dashboard: **Authentication → Providers → Email → "Confirm email" desactivado**.

### Despliegue en Producción (GitHub Pages)
```powershell
powershell -File .\deploy.ps1
```

---

## 🛡️ Reglas Fundamentales del Club
- **Administrador Principal:** El perfil con ID `user-1` (Pitu) tiene rol `Admin` blindado desde la base de datos: no puede ser degradado, dado de baja ni eliminado.
- **Seguridad:** Autenticación con Supabase Auth (contraseñas hasheadas) y permisos por rol aplicados en el servidor con Row Level Security. La clave pública de la app solo permite lo que las políticas autorizan.
- **Capitanía:** Desacoplada del rol de administrador y configurable para cualquier miembro.
- **Sincronización:** Cada cambio se escribe en Supabase por fila y se vuelve a leer la tabla. Los cambios de otros dispositivos llegan por Realtime. Si la nube rechaza un cambio, la app lo avisa y lo deshace.
- **Código de Equipo:** Vive en `club_settings.team_secret_code` y lo valida el servidor en el alta y en la reactivación.
