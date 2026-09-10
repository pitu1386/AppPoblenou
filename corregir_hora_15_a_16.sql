-- Pasa de las 15:xx a las 16:xx (hora local Europe/Madrid) todos los partidos que
-- tengan ese horario cargado. Copia y pega en: Supabase Dashboard -> SQL Editor -> Run
update public.matches
   set match_date = match_date + interval '1 hour'
 where extract(hour from match_date at time zone 'Europe/Madrid') = 15;
