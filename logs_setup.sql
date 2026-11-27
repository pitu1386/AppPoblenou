-- =========================================================================
-- ATLÈTIC POBLENOU · REGISTRO DE ACTIVIDAD Y ACCESOS (LOGS)
-- Copia y pega TODO este archivo en: Supabase Dashboard -> SQL Editor -> Run
--
-- Es idempotente: se puede ejecutar varias veces sin romper nada.
-- Solo pitu1386@gmail.com puede leer o limpiar los registros.
-- Cualquier miembro autenticado registra automáticamente su entrada/acciones
-- mediante la función RPC log_app_event().
-- =========================================================================

create table if not exists public.app_logs (
    id uuid primary key default gen_random_uuid(),
    created_at timestamptz not null default now(),
    profile_id text,
    user_name text not null default '',
    user_email text not null default '',
    event_type text not null,
    detail text not null default '',
    device_info text not null default ''
);

create index if not exists app_logs_created_at_idx on public.app_logs (created_at desc);
create index if not exists app_logs_event_type_idx on public.app_logs (event_type);

alter table public.app_logs enable row level security;
alter table public.app_logs force row level security;

revoke all on table public.app_logs from anon;
grant select, delete on table public.app_logs to authenticated;

create or replace function public.is_logs_viewer()
returns boolean language sql stable security definer set search_path = public as $$
    select lower(coalesce(auth.jwt() ->> 'email', '')) = 'pitu1386@gmail.com'
$$;

drop policy if exists "apn_logs_select" on public.app_logs;
create policy "apn_logs_select" on public.app_logs for select to authenticated
    using (public.is_logs_viewer());

drop policy if exists "apn_logs_delete" on public.app_logs;
create policy "apn_logs_delete" on public.app_logs for delete to authenticated
    using (public.is_logs_viewer());

create or replace function public.log_app_event(
    p_event_type text,
    p_detail text default '',
    p_device_info text default ''
) returns void language plpgsql security definer set search_path = public as $$
declare
    v_uid uuid := auth.uid();
    v_email text := lower(coalesce(auth.jwt() ->> 'email', ''));
    v_profile_id text;
    v_name text;
begin
    if v_uid is null then
        return;
    end if;

    select id, coalesce(nullif(trim(nickname), ''), full_name, email)
      into v_profile_id, v_name
      from public.profiles
     where auth_uid = v_uid
     limit 1;

    if v_profile_id is null and v_email = '' then
        return;
    end if;

    insert into public.app_logs (profile_id, user_name, user_email, event_type, detail, device_info)
    values (
        v_profile_id,
        coalesce(v_name, v_email),
        v_email,
        left(coalesce(nullif(trim(p_event_type), ''), 'unknown'), 60),
        left(coalesce(p_detail, ''), 400),
        left(coalesce(p_device_info, ''), 120)
    );
end $$;

create or replace function public.cleanup_old_app_logs(p_days integer default 30)
returns integer language plpgsql security definer set search_path = public as $$
declare n integer;
begin
    if not public.is_logs_viewer() then
        raise exception 'No autorizado';
    end if;
    delete from public.app_logs
     where created_at < now() - make_interval(days => greatest(1, coalesce(p_days, 30)));
    get diagnostics n = row_count;
    return n;
end $$;

revoke all on function public.log_app_event(text, text, text) from public;
grant execute on function public.log_app_event(text, text, text) to authenticated;

revoke all on function public.cleanup_old_app_logs(integer) from public;
grant execute on function public.cleanup_old_app_logs(integer) to authenticated;
