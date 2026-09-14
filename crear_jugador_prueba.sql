-- Crea una cuenta y ficha de jugador de PRUEBA (rol 3 = Player normal), completa:
-- auth.users + auth.identities + profiles. Sirve para iniciar sesión de verdad en la app
-- como un jugador cualquiera, sin tocar tu propia cuenta (user-1, que siempre es Admin).
-- Copia y pega en: Supabase Dashboard -> SQL Editor -> Run
--
-- Email y contraseña de la cuenta de prueba (cambialos aquí si querés otros):
--   Email:      prueba.jugador@atleticpoblenou.test
--   Contraseña: prueba1234
--
-- Para limpiar todo lo que crea este script, usa: limpiar_jugador_prueba.sql

do $$
declare
    v_uid uuid := gen_random_uuid();
    v_email text := 'prueba.jugador@atleticpoblenou.test';
    v_password text := 'prueba1234';
begin
    if exists (select 1 from auth.users where lower(email) = lower(v_email)) then
        raise exception 'Ya existe una cuenta con ese email. Corré primero limpiar_jugador_prueba.sql si querés recrearla.';
    end if;

    insert into auth.users (
        instance_id, id, aud, role, email, encrypted_password, email_confirmed_at,
        raw_app_meta_data, raw_user_meta_data, created_at, updated_at,
        confirmation_token, recovery_token, email_change_token_new, email_change
    ) values (
        '00000000-0000-0000-0000-000000000000', v_uid, 'authenticated', 'authenticated',
        v_email, crypt(v_password, gen_salt('bf')), now(),
        '{"provider":"email","providers":["email"]}'::jsonb, '{}'::jsonb, now(), now(),
        '', '', '', ''
    );

    insert into auth.identities (
        id, user_id, provider_id, identity_data, provider, last_sign_in_at, created_at, updated_at
    ) values (
        gen_random_uuid(), v_uid, v_uid::text,
        jsonb_build_object('sub', v_uid::text, 'email', v_email),
        'email', now(), now(), now()
    );

    insert into public.profiles (
        id, auth_uid, full_name, nickname, jersey_number, position, foot, role,
        is_captain, is_sub_captain, phone, email, is_active, created_at
    ) values (
        v_uid::text, v_uid, 'Jugador de Prueba', 'prueba', 99, 2, 0, 3,
        false, false, '', v_email, true, now()
    );

    raise notice 'Ficha de prueba creada. Email: %  Contraseña: %', v_email, v_password;
end $$;
