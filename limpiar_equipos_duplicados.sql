-- =========================================================================
-- LIMPIEZA: equipos rivales duplicados de una carga anterior (team-N) que
-- quedaron pisados por los nuevos, con cancha y escudo (rt-*).
-- Copia y pega TODO este archivo en: Supabase Dashboard -> SQL Editor -> Run
-- =========================================================================

-- Corrige el nombre de "UE Planadeu-Roureda B", que quedó mal puesto como
-- "V LA PLANADA A" (duplicando el nombre de rt-planada-a).
update public.rival_teams
   set name = 'UE Planadeu-Roureda B'
 where id = 'rt-planada-b';

-- Borra los 14 equipos viejos (team-N), todos sin cancha cargada y ya
-- reemplazados por su versión rt-* con cancha y escudo.
delete from public.rival_teams
 where id in (
    'team-1',  -- FONTETAS            -> rt-fontetas
    'team-2',  -- LA PEÑA             -> rt-la-pena
    'team-3',  -- ARISTOI B           -> rt-aristol-b
    'team-4',  -- LA PLANADA A        -> rt-planada-a
    'team-6',  -- LLANO               -> rt-llano
    'team-7',  -- CAN ROCA74          -> rt-can-roca
    'team-8',  -- LA PLANADA B        -> rt-planada-b
    'team-9',  -- LLIÇA D'AVALL       -> rt-lliça-avall
    'team-10', -- ATºBADIENSE         -> rt-tl-badiense
    'team-11', -- CDPV BADIA          -> rt-cdpv-badia
    'team-12', -- ATºLA CELESTE       -> rt-celeste
    'team-13', -- STA PERPETUA        -> rt-sta-perpetua
    'team-14', -- PUEBLO NUEVO 2002   -> rt-pueblo-nuevo-2002
    'team-15'  -- ARISTOI A           -> rt-aristol-a
 );
