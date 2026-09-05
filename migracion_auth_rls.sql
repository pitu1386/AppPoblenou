-- =========================================================================
-- ATLÈTIC POBLENOU · MIGRACIÓN A SUPABASE AUTH + ROW LEVEL SECURITY (v2.4)
-- Copia y pega TODO este archivo en: Supabase Dashboard -> SQL Editor -> Run
--
-- Es idempotente: se puede ejecutar varias veces sin romper nada.
-- Requisito previo en el Dashboard: Authentication -> Providers -> Email
--   -> "Confirm email" DESACTIVADO (si no, el alta pide confirmar el correo).
--
-- Qué hace:
--   1. Crea usuarios de Supabase Auth para los perfiles existentes usando la
--      contraseña que tenían en texto plano, y elimina esa columna.
--   2. Enlaza profiles.auth_uid con auth.users.id.
--   3. Normaliza columnas (category TEXT, paid_by, round).
--   4. Activa RLS en todas las tablas con políticas por rol.
--   5. Blinda al administrador principal (user-1) desde la base de datos.
--   6. Expone funciones RPC para login por apodo, alta con código, reactivación,
--      votar encuestas, vaciar fixture y cerrar temporada.
--   7. Añade las tablas a la publicación de Realtime.
-- =========================================================================

create extension if not exists pgcrypto;

-- -------------------------------------------------------------------------
-- 1. ESTRUCTURA
-- -------------------------------------------------------------------------
alter table public.profiles add column if not exists auth_uid uuid;
create unique index if not exists profiles_auth_uid_key on public.profiles (auth_uid);

alter table public.matches add column if not exists round integer default 1;

-- Limpieza de restos de partidos de prueba (antes de crear la restricción que los prohíbe)
delete from public.attendance where match_id in ('match-1', 'match-2');
delete from public.match_events where match_id in ('match-1', 'match-2');
delete from public.matches where id in ('match-1', 'match-2');

alter table public.matches drop constraint if exists check_no_mock_matches;
alter table public.matches add constraint check_no_mock_matches check (id not in ('match-1', 'match-2'));

alter table public.club_settings add column if not exists away_kit_primary_color_hex text default '#141210';
alter table public.club_settings add column if not exists away_kit_secondary_color_hex text default '#FFFFFF';
alter table public.club_settings add column if not exists away_kit_description text default '';

alter table public.team_expenses alter column category type text using category::text;
alter table public.team_expenses alter column category set default 'Otros';
alter table public.team_expenses add column if not exists paid_by text;

-- -------------------------------------------------------------------------
-- 2. MIGRAR USUARIOS EXISTENTES A auth.users
--    Solo se migran perfiles con email y sin auth_uid. La contraseña se
--    hashea con bcrypt. Si el perfil no tenía contraseña se usa '1234' y
--    el jugador deberá cambiarla desde su ficha.
-- -------------------------------------------------------------------------

-- Trigger heredado de un intento anterior: creaba perfiles con rol en texto al
-- registrar cuentas y rompe cualquier alta. La ficha ahora la crea register_profile().
do $$
declare r record;
begin
    for r in
        select t.tgname
        from pg_trigger t
        join pg_class c on c.oid = t.tgrelid
        join pg_namespace n on n.oid = c.relnamespace
        join pg_proc p on p.oid = t.tgfoid
        where n.nspname = 'auth' and c.relname = 'users' and p.proname = 'handle_new_user' and not t.tgisinternal
    loop
        execute format('drop trigger if exists %I on auth.users', r.tgname);
    end loop;
end $$;
drop function if exists public.handle_new_user() cascade;

do $$
declare
    r record;
    v_uid uuid;
    v_has_password boolean;
    v_password text;
begin
    select exists (
        select 1 from information_schema.columns
        where table_schema = 'public' and table_name = 'profiles' and column_name = 'password'
    ) into v_has_password;

    for r in select * from public.profiles where auth_uid is null and coalesce(email, '') <> '' loop
        select id into v_uid from auth.users where lower(email) = lower(trim(r.email)) limit 1;

        if v_uid is null then
            v_uid := gen_random_uuid();
            if v_has_password then
                execute format('select coalesce(nullif(password, ''''), ''1234'') from public.profiles where id = %L', r.id) into v_password;
            else
                v_password := '1234';
            end if;

            insert into auth.users (
                instance_id, id, aud, role, email, encrypted_password, email_confirmed_at,
                raw_app_meta_data, raw_user_meta_data, created_at, updated_at,
                confirmation_token, recovery_token, email_change_token_new, email_change
            ) values (
                '00000000-0000-0000-0000-000000000000', v_uid, 'authenticated', 'authenticated',
                lower(trim(r.email)), crypt(v_password, gen_salt('bf')), now(),
                '{"provider":"email","providers":["email"]}'::jsonb, '{}'::jsonb, now(), now(),
                '', '', '', ''
            );

            insert into auth.identities (
                id, user_id, provider_id, identity_data, provider, last_sign_in_at, created_at, updated_at
            ) values (
                gen_random_uuid(), v_uid, v_uid::text,
                jsonb_build_object('sub', v_uid::text, 'email', lower(trim(r.email))),
                'email', now(), now(), now()
            );
        end if;

        update public.profiles set auth_uid = v_uid where id = r.id;
    end loop;
end $$;

alter table public.profiles drop column if exists password;

-- -------------------------------------------------------------------------
-- 3. FUNCIONES DE APOYO PARA LAS POLÍTICAS
-- -------------------------------------------------------------------------
create or replace function public.my_profile_id()
returns text language sql stable security definer set search_path = public as $$
    select id from public.profiles where auth_uid = auth.uid() limit 1
$$;

create or replace function public.my_role()
returns integer language sql stable security definer set search_path = public as $$
    select role from public.profiles where auth_uid = auth.uid() and is_active limit 1
$$;

create or replace function public.is_member()
returns boolean language sql stable security definer set search_path = public as $$
    select exists (select 1 from public.profiles where auth_uid = auth.uid() and is_active)
$$;

-- Admin = 0
create or replace function public.is_admin()
returns boolean language sql stable security definer set search_path = public as $$
    select coalesce(public.my_role() = 0, false)
$$;

-- Admin, Delegado (2) y DT (4): gestionan partidos, rivales, actas y comunicados
create or replace function public.is_staff()
returns boolean language sql stable security definer set search_path = public as $$
    select coalesce(public.my_role() in (0, 2, 4), false)
$$;

-- Admin y Tesorero (1): gestionan cobros y caja
create or replace function public.is_treasury()
returns boolean language sql stable security definer set search_path = public as $$
    select coalesce(public.my_role() in (0, 1), false)
$$;

create or replace function public.normalize_code(p text)
returns text language sql immutable as $$
    select upper(replace(trim(coalesce(p, '')), '-', ''))
$$;

-- -------------------------------------------------------------------------
-- 4. TRIGGERS DE PROTECCIÓN EN profiles
-- -------------------------------------------------------------------------
create or replace function public.protect_owner_admin()
returns trigger language plpgsql as $$
begin
    if tg_op = 'DELETE' then
        if old.id = 'user-1' then
            raise exception 'El administrador principal no puede eliminarse';
        end if;
        return old;
    end if;

    if new.id = 'user-1' then
        new.role := 0;
        new.is_active := true;
    end if;
    return new;
end $$;

drop trigger if exists trg_protect_owner_admin on public.profiles;
create trigger trg_protect_owner_admin
before update or delete on public.profiles
for each row execute function public.protect_owner_admin();


-- Un jugador solo puede tocar sus datos de ficha; rol, capitanía, estado y
-- vínculo de cuenta solo los cambia un admin.
create or replace function public.prevent_privilege_escalation()
returns trigger language plpgsql as $$
begin
    -- Sin JWT (SQL Editor, service_role) o con el bypass activado por una función RPC, no se aplica.
    if auth.uid() is null or current_setting('apn.bypass_guard', true) = '1' then
        return new;
    end if;
    if not public.is_admin() then
        if new.role is distinct from old.role
           or new.is_active is distinct from old.is_active
           or new.is_captain is distinct from old.is_captain
           or new.is_sub_captain is distinct from old.is_sub_captain
           or new.auth_uid is distinct from old.auth_uid
           or new.email is distinct from old.email then
            raise exception 'No tienes permiso para cambiar rol, capitanía, estado o cuenta';
        end if;
    end if;
    return new;
end $$;

drop trigger if exists trg_prevent_privilege_escalation on public.profiles;
create trigger trg_prevent_privilege_escalation
before update on public.profiles
for each row execute function public.prevent_privilege_escalation();

-- El blindaje también se aplica a los datos ya guardados: el cliente antiguo forzaba
-- Admin en memoria, pero la fila real podía tener otro rol o estar de baja. Va después
-- de recrear el trigger para que no lo bloquee una versión anterior del mismo.
update public.profiles set role = 0, is_active = true where id = 'user-1' and (role <> 0 or not is_active);

-- -------------------------------------------------------------------------
-- 5. ROW LEVEL SECURITY
-- -------------------------------------------------------------------------
revoke all on all tables in schema public from anon;
revoke all on all sequences in schema public from anon;
grant usage on schema public to anon, authenticated;
grant select, insert, update, delete on all tables in schema public to authenticated;

do $$
declare t text;
begin
    foreach t in array array['profiles','matches','attendance','payments','team_expenses','match_events','rival_teams','announcements','club_settings'] loop
        execute format('alter table public.%I enable row level security', t);
        execute format('alter table public.%I force row level security', t);
        execute format('drop policy if exists "Permiso Total Anon" on public.%I', t);
        execute format('drop policy if exists "apn_select" on public.%I', t);
        execute format('drop policy if exists "apn_insert" on public.%I', t);
        execute format('drop policy if exists "apn_update" on public.%I', t);
        execute format('drop policy if exists "apn_delete" on public.%I', t);
    end loop;
end $$;

-- profiles: todos los miembros leen la plantilla; cada uno edita su ficha; admin todo.
create policy "apn_select" on public.profiles for select to authenticated
    using (public.is_member() or auth_uid = auth.uid());
create policy "apn_update" on public.profiles for update to authenticated
    using (public.is_admin() or auth_uid = auth.uid())
    with check (public.is_admin() or auth_uid = auth.uid());
create policy "apn_delete" on public.profiles for delete to authenticated
    using (public.is_admin() and id <> 'user-1');
-- La app guarda fichas con upsert (INSERT ... ON CONFLICT), y Postgres evalúa la política
-- de INSERT aunque la fila exista. El alta real la hace register_profile(); el índice único
-- de auth_uid impide que un jugador se cree una segunda ficha.
create policy "apn_insert" on public.profiles for insert to authenticated
    with check (public.is_admin() or auth_uid = auth.uid());

-- matches / rival_teams / match_events: lectura miembros, escritura staff.
create policy "apn_select" on public.matches for select to authenticated using (public.is_member());
create policy "apn_insert" on public.matches for insert to authenticated with check (public.is_staff());
create policy "apn_update" on public.matches for update to authenticated using (public.is_staff()) with check (public.is_staff());
create policy "apn_delete" on public.matches for delete to authenticated using (public.is_staff());

create policy "apn_select" on public.rival_teams for select to authenticated using (public.is_member());
create policy "apn_insert" on public.rival_teams for insert to authenticated with check (public.is_staff());
create policy "apn_update" on public.rival_teams for update to authenticated using (public.is_staff()) with check (public.is_staff());
create policy "apn_delete" on public.rival_teams for delete to authenticated using (public.is_staff());

create policy "apn_select" on public.match_events for select to authenticated using (public.is_member());
create policy "apn_insert" on public.match_events for insert to authenticated with check (public.is_staff());
create policy "apn_update" on public.match_events for update to authenticated using (public.is_staff()) with check (public.is_staff());
create policy "apn_delete" on public.match_events for delete to authenticated using (public.is_staff());

-- attendance: cada jugador su propia asistencia; staff cualquiera.
create policy "apn_select" on public.attendance for select to authenticated using (public.is_member());
create policy "apn_insert" on public.attendance for insert to authenticated
    with check (public.is_staff() or player_id = public.my_profile_id());
create policy "apn_update" on public.attendance for update to authenticated
    using (public.is_staff() or player_id = public.my_profile_id())
    with check (public.is_staff() or player_id = public.my_profile_id());
create policy "apn_delete" on public.attendance for delete to authenticated
    using (public.is_staff() or player_id = public.my_profile_id());

-- payments / team_expenses: lectura miembros, escritura tesorería.
create policy "apn_select" on public.payments for select to authenticated using (public.is_member());
create policy "apn_insert" on public.payments for insert to authenticated with check (public.is_treasury());
create policy "apn_update" on public.payments for update to authenticated using (public.is_treasury()) with check (public.is_treasury());
create policy "apn_delete" on public.payments for delete to authenticated using (public.is_treasury());

create policy "apn_select" on public.team_expenses for select to authenticated using (public.is_member());
create policy "apn_insert" on public.team_expenses for insert to authenticated with check (public.is_treasury());
create policy "apn_update" on public.team_expenses for update to authenticated using (public.is_treasury()) with check (public.is_treasury());
create policy "apn_delete" on public.team_expenses for delete to authenticated using (public.is_treasury());

-- announcements: lectura miembros, publicar/archivar/borrar staff. Votar vía vote_poll().
create policy "apn_select" on public.announcements for select to authenticated using (public.is_member());
create policy "apn_insert" on public.announcements for insert to authenticated with check (public.is_staff());
create policy "apn_update" on public.announcements for update to authenticated using (public.is_staff()) with check (public.is_staff());
create policy "apn_delete" on public.announcements for delete to authenticated using (public.is_staff());

-- club_settings: lectura miembros, escritura admin.
create policy "apn_select" on public.club_settings for select to authenticated using (public.is_member());
create policy "apn_insert" on public.club_settings for insert to authenticated with check (public.is_admin());
create policy "apn_update" on public.club_settings for update to authenticated using (public.is_admin()) with check (public.is_admin());

-- -------------------------------------------------------------------------
-- 6. FUNCIONES RPC
-- -------------------------------------------------------------------------

-- Login por apodo o nombre: devuelve el email de la cuenta (anon).
create or replace function public.lookup_login_email(identifier text)
returns text language sql stable security definer set search_path = public as $$
    select email from public.profiles
    where lower(trim(nickname)) = lower(trim(identifier))
       or lower(trim(full_name)) = lower(trim(identifier))
       or lower(trim(email)) = lower(trim(identifier))
    order by (lower(trim(email)) = lower(trim(identifier))) desc
    limit 1
$$;

-- Comprueba el código de equipo antes de crear la cuenta (anon).
create or replace function public.validate_team_code(p_code text)
returns boolean language sql stable security definer set search_path = public as $$
    select exists (
        select 1 from public.club_settings
        where id = 'current' and public.normalize_code(team_secret_code) = public.normalize_code(p_code)
    )
$$;

-- Crea la ficha del usuario autenticado tras el alta en Auth.
create or replace function public.register_profile(
    p_team_code text,
    p_full_name text,
    p_nickname text,
    p_jersey_number integer default null,
    p_position integer default 2,
    p_foot integer default 0,
    p_phone text default '',
    p_birth_date date default null
) returns text language plpgsql security definer set search_path = public as $$
declare
    v_uid uuid := auth.uid();
    v_email text := lower(coalesce(auth.jwt() ->> 'email', ''));
    v_id text;
    v_role integer := 3;
begin
    if v_uid is null then
        raise exception 'No autenticado';
    end if;
    if not public.validate_team_code(p_team_code) then
        raise exception 'Código de equipo incorrecto';
    end if;
    if exists (select 1 from public.profiles where auth_uid = v_uid) then
        select id into v_id from public.profiles where auth_uid = v_uid;
        return v_id;
    end if;
    if coalesce(trim(p_full_name), '') = '' then
        raise exception 'El nombre es obligatorio';
    end if;
    if exists (select 1 from public.profiles where lower(trim(nickname)) = lower(trim(p_nickname)) and coalesce(trim(p_nickname), '') <> '') then
        raise exception 'Ese apodo ya está en uso';
    end if;

    -- Si no existe ningún admin activo, el primero en registrarse lo es.
    if not exists (select 1 from public.profiles where role = 0 and is_active) then
        v_role := 0;
    end if;

    v_id := v_uid::text;
    insert into public.profiles (id, auth_uid, full_name, nickname, jersey_number, position, foot, role,
                                 is_captain, is_sub_captain, phone, email, birth_date, is_active, created_at)
    values (v_id, v_uid, trim(p_full_name), coalesce(nullif(trim(p_nickname), ''), split_part(trim(p_full_name), ' ', 1)),
            p_jersey_number, p_position, p_foot, v_role, false, false, coalesce(p_phone, ''), v_email, p_birth_date, true, now());
    return v_id;
end $$;

-- Reactiva la ficha del usuario autenticado con el código de equipo.
create or replace function public.reactivate_with_code(p_team_code text)
returns boolean language plpgsql security definer set search_path = public as $$
begin
    if auth.uid() is null then
        raise exception 'No autenticado';
    end if;
    if not public.validate_team_code(p_team_code) then
        raise exception 'Código de seguridad incorrecto';
    end if;
    perform set_config('apn.bypass_guard', '1', true);
    update public.profiles set is_active = true where auth_uid = auth.uid();
    return found;
end $$;

-- Cambia la contraseña de otro miembro (solo admin), útil si alguien la olvida.
create or replace function public.admin_set_password(p_profile_id text, p_new_password text)
returns boolean language plpgsql security definer set search_path = public, auth as $$
declare v_uid uuid;
begin
    if not public.is_admin() then
        raise exception 'Solo un administrador puede hacerlo';
    end if;
    if length(coalesce(p_new_password, '')) < 4 then
        raise exception 'La contraseña debe tener al menos 4 caracteres';
    end if;
    select auth_uid into v_uid from public.profiles where id = p_profile_id;
    if v_uid is null then
        raise exception 'Ese perfil no tiene cuenta vinculada';
    end if;
    update auth.users set encrypted_password = crypt(p_new_password, gen_salt('bf')), updated_at = now() where id = v_uid;
    return true;
end $$;

-- Voto en encuesta: solo modifica la entrada propia dentro del JSON de votos.
create or replace function public.vote_poll(p_announcement_id text, p_option integer)
returns boolean language plpgsql security definer set search_path = public as $$
declare v_pid text := public.my_profile_id();
begin
    if v_pid is null or not public.is_member() then
        raise exception 'No autorizado';
    end if;
    update public.announcements
       set votes = coalesce(votes, '{}'::jsonb) || jsonb_build_object(v_pid, p_option)
     where id = p_announcement_id and is_active;
    return found;
end $$;

-- Vaciar fixture completo (staff). Las asistencias y eventos caen por cascada.
create or replace function public.clear_all_matches()
returns integer language plpgsql security definer set search_path = public as $$
declare n integer;
begin
    if not public.is_staff() then
        raise exception 'No autorizado';
    end if;
    delete from public.match_events;
    delete from public.attendance;
    delete from public.matches;
    get diagnostics n = row_count;
    return n;
end $$;

-- Cierre de temporada (admin): archiva el resumen en club_settings y limpia
-- partidos, asistencias, eventos y cobros en una sola transacción.
create or replace function public.close_season(p_archive jsonb, p_new_season_name text, p_new_fee numeric)
returns boolean language plpgsql security definer set search_path = public as $$
begin
    if not public.is_admin() then
        raise exception 'Solo un administrador puede cerrar la temporada';
    end if;
    update public.club_settings
       set season_history = coalesce(season_history, '[]'::jsonb) || jsonb_build_array(p_archive),
           season_name = p_new_season_name,
           season_fee_per_player = p_new_fee
     where id = 'current';
    delete from public.match_events;
    delete from public.attendance;
    delete from public.matches;
    delete from public.payments;
    return true;
end $$;

revoke all on function public.lookup_login_email(text) from public;
revoke all on function public.validate_team_code(text) from public;
grant execute on function public.lookup_login_email(text) to anon, authenticated;
grant execute on function public.validate_team_code(text) to anon, authenticated;
grant execute on function public.register_profile(text, text, text, integer, integer, integer, text, date) to authenticated;
grant execute on function public.reactivate_with_code(text) to authenticated;
grant execute on function public.admin_set_password(text, text) to authenticated;
grant execute on function public.vote_poll(text, integer) to authenticated;
grant execute on function public.clear_all_matches() to authenticated;
grant execute on function public.close_season(jsonb, text, numeric) to authenticated;

-- -------------------------------------------------------------------------
-- 7. REALTIME: publicar cambios de todas las tablas
-- -------------------------------------------------------------------------
do $$
declare t text;
begin
    foreach t in array array['profiles','matches','attendance','payments','team_expenses','match_events','rival_teams','announcements','club_settings'] loop
        if not exists (
            select 1 from pg_publication_tables where pubname = 'supabase_realtime' and schemaname = 'public' and tablename = t
        ) then
            execute format('alter publication supabase_realtime add table public.%I', t);
        end if;
    end loop;
end $$;

-- Realtime necesita identidad completa para filtrar por RLS en UPDATE/DELETE.
alter table public.profiles replica identity full;
alter table public.matches replica identity full;
alter table public.attendance replica identity full;
alter table public.payments replica identity full;
alter table public.team_expenses replica identity full;
alter table public.match_events replica identity full;
alter table public.rival_teams replica identity full;
alter table public.announcements replica identity full;
alter table public.club_settings replica identity full;
