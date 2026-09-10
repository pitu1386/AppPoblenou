-- =========================================================================
-- Re-sincroniza NOMBRE y CANCHA de los rivales en el fixture ya cargado con
-- lo que hoy tiene cada equipo en su ficha (tabla rival_teams).
-- Solo toca los partidos de la importación AEC84 (id que empieza con 'aec84-').
-- Copia y pega en: Supabase Dashboard -> SQL Editor -> Run
-- =========================================================================

-- 1) Partidos nuestros (guardan el nombre del rival en 'opponent')
update public.matches m
   set opponent = rt.name
  from public.rival_teams rt
 where m.rival_team_id = rt.id
   and m.id like 'aec84-%'
   and (m.notes is null or m.notes not like 'LM|%');

-- 1b) ...y la cancha, cuando jugamos de visitante (la cancha es la del rival)
update public.matches m
   set location_name = rt.venue_name,
       location_url  = rt.venue_maps_url
  from public.rival_teams rt
 where m.rival_team_id = rt.id
   and m.id like 'aec84-%'
   and m.is_home = false
   and (m.notes is null or m.notes not like 'LM|%')
   and coalesce(rt.venue_name, '') <> '';

-- 2) Partidos entre dos rivales (formato 'LM|homeId|homeName|awayId|awayName')
update public.matches m
   set opponent = home_rt.name || ' vs ' || away_rt.name,
       notes = 'LM|' || split_part(m.notes, '|', 2) || '|' || home_rt.name
             || '|' || split_part(m.notes, '|', 4) || '|' || away_rt.name,
       location_name = coalesce(nullif(home_rt.venue_name, ''), m.location_name),
       location_url  = coalesce(nullif(home_rt.venue_maps_url, ''), m.location_url)
  from public.rival_teams home_rt, public.rival_teams away_rt
 where m.id like 'aec84-%'
   and m.notes like 'LM|%'
   and home_rt.id = split_part(m.notes, '|', 2)
   and away_rt.id = split_part(m.notes, '|', 4);
