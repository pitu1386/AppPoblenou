-- ==========================================================
-- NOTIFICACIONES PUSH · Setup de base de datos
-- Ejecutar en: Supabase Dashboard -> SQL Editor -> Run
-- (idempotente: se puede correr varias veces)
--
-- ANTES de correr esto:
--   1. Desplegá la Edge Function `send-push` (carpeta supabase/functions/send-push).
--   2. En Edge Functions -> Manage secrets, cargá:
--        VAPID_PUBLIC_KEY   = BHW224Pd_Vk2yX7O4WDyF3BCOQTaVwjNRITaYL1fsQ4wkvO6rn-I35d8yxlpk6TSl99KWhpRqK7TqvFbK4Ybe0s
--        VAPID_PRIVATE_KEY  = (el privado que te paso aparte / regenerás con: npx web-push generate-vapid-keys)
--        VAPID_SUBJECT      = mailto:tu-email@ejemplo.com
--        INTERNAL_SECRET    = (un valor random largo, el mismo que ponés abajo en el vault)
-- ==========================================================

create extension if not exists pg_net;
create extension if not exists pg_cron;

-- 1. Suscripciones push (una por navegador/dispositivo)
create table if not exists public.push_subscriptions (
    id          uuid primary key default gen_random_uuid(),
    profile_id  text not null references public.profiles(id) on delete cascade,
    endpoint    text not null unique,
    p256dh      text not null,
    auth        text not null,
    user_agent  text,
    created_at  timestamptz not null default now()
);
create index if not exists push_subs_profile_idx on public.push_subscriptions(profile_id);

alter table public.push_subscriptions enable row level security;

drop policy if exists "push_own_select" on public.push_subscriptions;
drop policy if exists "push_own_insert" on public.push_subscriptions;
drop policy if exists "push_own_delete" on public.push_subscriptions;

create policy "push_own_select" on public.push_subscriptions for select to authenticated
    using (profile_id in (select id from public.profiles where auth_uid = auth.uid()));
create policy "push_own_insert" on public.push_subscriptions for insert to authenticated
    with check (profile_id in (select id from public.profiles where auth_uid = auth.uid()));
create policy "push_own_delete" on public.push_subscriptions for delete to authenticated
    using (profile_id in (select id from public.profiles where auth_uid = auth.uid()));

-- 2. Marca de recordatorio ya enviado, en cada partido
alter table public.matches add column if not exists reminder_sent_at timestamptz;

-- 3. Secreto interno para que triggers/cron llamen a la Edge Function.
--    >>> REEMPLAZÁ 'PONE_UN_VALOR_RANDOM_LARGO_ACA' por tu valor (el mismo que INTERNAL_SECRET de la función).
--    Si ya lo habías creado antes, primero borralo con:
--      select vault.delete_secret((select id from vault.secrets where name = 'push_internal_secret'));
select vault.create_secret('PONE_UN_VALOR_RANDOM_LARGO_ACA', 'push_internal_secret');

-- 4. Helper: invoca la Edge Function con el secreto interno
create or replace function public.invoke_send_push(payload jsonb)
returns void
language plpgsql
security definer
set search_path = public
as $$
declare
    secret text;
begin
    select decrypted_secret into secret from vault.decrypted_secrets where name = 'push_internal_secret';
    perform net.http_post(
        url     := 'https://dlajpiuuslegmoedslux.supabase.co/functions/v1/send-push',
        headers := jsonb_build_object(
            'Content-Type', 'application/json',
            'apikey', 'sb_publishable_2jgFAT8ePAK6BJOyPDUImA_-BC8NXjq',
            'x-internal-secret', secret
        ),
        body    := payload
    );
end;
$$;

-- 5. Trigger: comunicado nuevo -> notifica a todos
create or replace function public.on_announcement_push()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
    if new.is_active then
        perform public.invoke_send_push(jsonb_build_object(
            'kind', 'announcement',
            'record', jsonb_build_object('title', new.title, 'content', new.content)
        ));
    end if;
    return new;
end;
$$;

drop trigger if exists trg_announcement_push on public.announcements;
create trigger trg_announcement_push
    after insert on public.announcements
    for each row execute function public.on_announcement_push();

-- 6. Cron: recordatorio de partidos (cada hora, a los :05)
do $$
begin
    if exists (select 1 from cron.job where jobname = 'match-reminder') then
        perform cron.unschedule('match-reminder');
    end if;
end $$;

select cron.schedule('match-reminder', '5 * * * *', $$
    select public.invoke_send_push(jsonb_build_object('kind', 'match_reminder'));
$$);
