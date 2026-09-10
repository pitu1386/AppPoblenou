-- Vuelve a tomar la cancha de local desde la ficha del club (Admin -> Club y Liga)
-- para los partidos de local que se cargaron con la importación de AEC84.
-- Copia y pega en: Supabase Dashboard -> SQL Editor -> Run

update public.matches m
   set location_name = cs.home_venue_name,
       location_url = cs.home_venue_maps_url
  from public.club_settings cs
 where cs.id = 'current'
   and m.is_home = true
   and m.id like 'aec84-%';
