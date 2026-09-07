// Edge Function: send-push
// Envía notificaciones Web Push a los suscriptores.
//
// Autenticación (una de las dos):
//   - Header  x-internal-secret: <INTERNAL_SECRET>   -> lo usan los triggers / pg_cron
//   - Header  Authorization: Bearer <jwt de usuario>  -> el usuario debe ser staff (admin/delegado/DT)
//
// Body (jsonb):
//   { kind: "manual",         title, body, url? }                     -> a todos
//   { kind: "custom",         title, body, url?, profileIds: [...] }   -> a esos jugadores
//   { kind: "announcement",   record: { title, content } }            -> a todos (desde el trigger)
//   { kind: "match_reminder" }                                        -> busca partidos próximos y avisa
//
// Secrets requeridos (Edge Functions -> Manage secrets):
//   VAPID_PUBLIC_KEY, VAPID_PRIVATE_KEY, VAPID_SUBJECT, INTERNAL_SECRET
//   (SUPABASE_URL y SUPABASE_SERVICE_ROLE_KEY ya vienen dados)

import { createClient } from "https://esm.sh/@supabase/supabase-js@2.49.4";
import webpush from "npm:web-push@3.6.7";

const SUPABASE_URL = Deno.env.get("SUPABASE_URL")!;
const SERVICE_ROLE = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
const VAPID_PUBLIC = Deno.env.get("VAPID_PUBLIC_KEY")!;
const VAPID_PRIVATE = Deno.env.get("VAPID_PRIVATE_KEY")!;
const VAPID_SUBJECT = Deno.env.get("VAPID_SUBJECT") ?? "mailto:admin@atleticpoblenou.cat";
const INTERNAL_SECRET = Deno.env.get("INTERNAL_SECRET")!;

webpush.setVapidDetails(VAPID_SUBJECT, VAPID_PUBLIC, VAPID_PRIVATE);
const admin = createClient(SUPABASE_URL, SERVICE_ROLE);

const CORS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers":
    "authorization, x-client-info, apikey, content-type, x-internal-secret",
};

// Roles que pueden mandar notificaciones desde la app (UserRole: 0 Admin, 2 Delegado, 4 DT)
const STAFF_ROLES = [0, 2, 4];

interface Notif {
  title: string;
  body: string;
  url?: string;
  tag?: string;
  profileIds?: string[];
}

Deno.serve(async (req) => {
  if (req.method === "OPTIONS") return new Response("ok", { headers: CORS });

  try {
    const payload = await req.json().catch(() => ({}));
    const isInternal = req.headers.get("x-internal-secret") === INTERNAL_SECRET;

    if (!isInternal) {
      const token = (req.headers.get("Authorization") ?? "").replace("Bearer ", "");
      const { data: { user } } = await admin.auth.getUser(token);
      if (!user) return json({ error: "sin autenticación" }, 401);
      const { data: prof } = await admin
        .from("profiles").select("id, role").eq("auth_uid", user.id).maybeSingle();
      if (!prof || !STAFF_ROLES.includes(prof.role)) {
        return json({ error: "solo el staff puede enviar notificaciones" }, 403);
      }
    }

    const kind = payload.kind ?? "manual";
    const notifs: Notif[] = [];

    if (kind === "announcement") {
      const a = payload.record ?? {};
      notifs.push({
        title: `📢 ${a.title ?? "Nuevo comunicado"}`,
        body: String(a.content ?? "").slice(0, 140),
        url: "/",
        tag: "announcement",
      });
    } else if (kind === "match_reminder") {
      const now = new Date();
      const until = new Date(now.getTime() + 22 * 3600 * 1000);
      const { data: matches } = await admin
        .from("matches").select("*")
        .eq("status", 0)
        .is("reminder_sent_at", null)
        .gte("match_date", now.toISOString())
        .lte("match_date", until.toISOString());

      for (const m of matches ?? []) {
        const hh = new Date(m.match_date).toLocaleTimeString("es-ES", {
          hour: "2-digit", minute: "2-digit", timeZone: "Europe/Madrid",
        });
        notifs.push({
          title: `⚽ Mañana ${m.is_home ? "de local" : "de visitante"} vs ${m.opponent}`,
          body: `${hh} h · ${m.location_name}. ¿Confirmaste tu asistencia?`,
          url: "/",
          tag: `match-${m.id}`,
        });
        await admin.from("matches").update({ reminder_sent_at: now.toISOString() }).eq("id", m.id);
      }
      if (notifs.length === 0) return json({ ok: true, sent: 0, note: "sin partidos próximos" });
    } else {
      notifs.push({
        title: payload.title ?? "Atlètic Poblenou",
        body: payload.body ?? "",
        url: payload.url ?? "/",
        tag: payload.tag,
        profileIds: Array.isArray(payload.profileIds) ? payload.profileIds : undefined,
      });
    }

    let sent = 0, removed = 0;
    for (const n of notifs) {
      let q = admin.from("push_subscriptions").select("endpoint, p256dh, auth");
      if (n.profileIds?.length) q = q.in("profile_id", n.profileIds);
      const { data: subs } = await q;
      const body = JSON.stringify({ title: n.title, body: n.body, url: n.url, tag: n.tag });

      await Promise.all((subs ?? []).map(async (s) => {
        try {
          await webpush.sendNotification(
            { endpoint: s.endpoint, keys: { p256dh: s.p256dh, auth: s.auth } },
            body,
          );
          sent++;
        } catch (err) {
          const code = (err as { statusCode?: number })?.statusCode;
          if (code === 404 || code === 410) {
            await admin.from("push_subscriptions").delete().eq("endpoint", s.endpoint);
            removed++;
          }
        }
      }));
    }

    return json({ ok: true, sent, removed });
  } catch (e) {
    return json({ error: String(e) }, 500);
  }
});

function json(obj: unknown, status = 200) {
  return new Response(JSON.stringify(obj), {
    status,
    headers: { ...CORS, "Content-Type": "application/json" },
  });
}
