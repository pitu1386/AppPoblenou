# 🔴⚪ Atlètic Poblenou - App Oficial (PWA)

Plataforma oficial del **Atlètic Poblenou (A.P.N. Veteranos)** desarrollada con **Blazor WebAssembly (.NET 10)**, **Tailwind CSS** y **Supabase (PostgreSQL en la nube)**.

🚀 **Acceso a la App en Producción:** [https://pitu1386.github.io/AppPoblenou/](https://pitu1386.github.io/AppPoblenou/)

---

## 📚 Documentación Técnica Completa

Toda la arquitectura técnica, reglas de negocio, esquemas de bases de datos, credenciales y soluciones a problemas críticos se encuentran detallados en:

👉 **[DOCUMENTACION.md](./DOCUMENTACION.md)**

---

## ⚡ Comandos Rápidos

### Desarrollo Local
```powershell
dotnet run --project .\AtleticPoblenou\AtleticPoblenou.csproj --urls "http://localhost:5091"
```
Acceder a `http://localhost:5091`.

### Despliegue en Producción (GitHub Pages)
```powershell
powershell -File .\deploy.ps1
```

---

## 🛡️ Reglas Fundamentales del Club
- **Administrador Principal:** El usuario Pitu (`pitu1386` / `user-1`) tiene rol `Admin` blindado de manera permanente. No puede ser degradado ni eliminado.
- **Capitanía:** Desacoplada del rol de administrador y libremente configurable para cualquier miembro del equipo.
- **Fixture:** Los partidos se sincronizan en tiempo real con Supabase. No se re-insertan partidos de prueba automáticamente si la base de datos está vacía.
- **Código de Equipo:** Administrado y sincronizado en tiempo real en la nube a través de la tabla `club_settings`.
