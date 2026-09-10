-- Marca todos los partidos como "A confirmar" (hora provisional, sujeta a cambios).
-- Copia y pega en: Supabase Dashboard -> SQL Editor -> Run
update public.matches set is_time_confirmed = false;
