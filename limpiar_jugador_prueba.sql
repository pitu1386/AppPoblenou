-- Borra por completo la ficha y cuenta de PRUEBA creadas por crear_jugador_prueba.sql
-- (profiles + auth.identities + auth.users). Es seguro re-ejecutarlo aunque ya esté borrado.
-- Copia y pega en: Supabase Dashboard -> SQL Editor -> Run

do $$
declare
    v_email text := 'prueba.jugador@atleticpoblenou.test';
    v_uid uuid;
begin
    select id into v_uid from auth.users where lower(email) = lower(v_email);

    delete from public.profiles where email = v_email or (v_uid is not null and auth_uid = v_uid);

    if v_uid is not null then
        delete from auth.identities where user_id = v_uid;
        delete from auth.users where id = v_uid;
        raise notice 'Cuenta y ficha de prueba (%), eliminadas.', v_email;
    else
        raise notice 'No había ninguna cuenta de prueba con ese email.';
    end if;
end $$;
