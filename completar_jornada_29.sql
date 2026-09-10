-- Los 2 partidos de la Jornada 29 (01-05-2027) que faltaban en el PDF original.
-- Copia y pega en: Supabase Dashboard -> SQL Editor -> Run

-- ATº CELESTE (local) vs V LA PLANADA A (visitante) — 16:30
insert into public.matches (id, round, match_date, opponent, rival_team_id, competition, match_kind, location_name, location_url, is_home, status, is_time_confirmed, notes)
values ('aec84-j29-celeste-vs-planada-a', 29, '2027-05-01 16:30:00+02'::timestamptz,
        'Atlètic Celeste vs UE Planadeu-Roureda A', 'rt-celeste', 'DIV.HONOR SABADO', 'liga',
        'Camp de l''Atlètic Vallès', 'https://maps.google.com/?q=Camp%20de%20l''Atl%C3%A8tic%20Vall%C3%A8s%20Barcelona',
        false, 0, false, 'LM|rt-celeste|Atlètic Celeste|rt-planada-a|UE Planadeu-Roureda A')
on conflict (id) do update set match_date = excluded.match_date, opponent = excluded.opponent, notes = excluded.notes;

-- V.LA PLANADA B (local) vs Atlètic Poblenou (visitante) — 16:00  [partido nuestro]
insert into public.matches (id, round, match_date, opponent, rival_team_id, competition, match_kind, location_name, location_url, is_home, status, is_time_confirmed)
values ('aec84-j29-apn', 29, '2027-05-01 16:00:00+02'::timestamptz,
        'UE Planadeu-Roureda B', 'rt-planada-b', 'DIV.HONOR SABADO', 'liga',
        'Camp Municipal de la Planada', 'https://maps.google.com/?q=Camp%20Municipal%20de%20la%20Planada%20Barcelona',
        false, 0, false)
on conflict (id) do update set match_date = excluded.match_date, opponent = excluded.opponent, rival_team_id = excluded.rival_team_id, location_name = excluded.location_name, location_url = excluded.location_url, is_home = excluded.is_home;
