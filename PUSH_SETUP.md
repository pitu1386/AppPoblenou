# Notificaciones push — puesta en marcha

Todo el código ya está. Falta configurar Supabase (una vez). Pasos en orden:

## 1. Generá un secreto interno

Cualquier string random largo. Por ejemplo, en una terminal:

```
npx --yes uuid            # o: openssl rand -hex 32
```

Guardалo — lo vas a usar en los pasos 3 y 4 (tiene que ser **el mismo** en los dos).

## 2. Desplegá la Edge Function

Dashboard de Supabase → **Edge Functions** → **Deploy a new function**

- Nombre: `send-push`
- **Borrá toda la plantilla que trae** (la de `withSupabase`) y pegá el contenido completo de
  [`supabase/functions/send-push/index.ts`](supabase/functions/send-push/index.ts).
  Usa el patrón clásico `Deno.serve`, que funciona perfecto.
- **Verify JWT: OFF** (la función hace su propia autenticación: chequea el `x-internal-secret`
  o el rol del usuario). Si lo dejás en ON, el cron y el trigger de comunicados van a fallar con 401.
- Deploy

## 3. Cargá los secretos de la función

Dashboard → **Edge Functions** → `send-push` → **Secrets** (o Project Settings → Edge Functions):

| Nombre | Valor |
|---|---|
| `VAPID_PUBLIC_KEY` | `BHW224Pd_Vk2yX7O4WDyF3BCOQTaVwjNRITaYL1fsQ4wkvO6rn-I35d8yxlpk6TSl99KWhpRqK7TqvFbK4Ybe0s` |
| `VAPID_PRIVATE_KEY` | `tkgDg4pzev38V3aSfAFirpWtqpMqXAsmbAx5DBKg11c` |
| `VAPID_SUBJECT` | `mailto:tu-email@ejemplo.com` (tu email real) |
| `INTERNAL_SECRET` | el secreto del paso 1 |

> La pública ya está en el código de la app ([`AppInfo.cs`](AtleticPoblenou/Services/AppInfo.cs)). Si querés cambiar el par de claves: `npx web-push generate-vapid-keys` y reemplazás la pública en `AppInfo.cs` y las dos acá.

## 4. Extensiones + SQL

Dashboard → **Database → Extensions**: activá `pg_cron` y `pg_net` (si no están).

Después, en **SQL Editor**:
- Abrí [`push_setup.sql`](push_setup.sql), reemplazá **las 2 apariciones** de `PONE_UN_VALOR_RANDOM_LARGO_ACA` por el secreto del paso 1.
- Run.

Esto crea la tabla `push_subscriptions`, el trigger de comunicados y el cron del recordatorio de partido (corre cada hora).

## 5. Deploy del frontend

```
cd C:\Desarrollos\AppPoblenou
powershell -File .\deploy.ps1
```

## 6. Probar

1. Abrí la app publicada, entrá con tu usuario.
2. Menú del perfil → **Activar notificaciones** → aceptá el permiso del navegador.
3. Admin → Comunicados → **Enviar notificación al equipo** → mandate una de prueba.
4. Deberías recibir el aviso del teléfono.

### iPhone
Solo funciona con la app **instalada en la pantalla de inicio** (Safari → Compartir → Añadir a pantalla de inicio) y iOS 16.4+.

### Si la Edge Function falla al desplegar o ejecutar
Puede ser incompatibilidad de `npm:web-push` con el runtime. Avisame y lo cambio por una librería nativa de Deno (`jsr:@negrel/webpush`), es un rato.
