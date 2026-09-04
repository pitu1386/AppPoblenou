-- =========================================================================
-- BLOQUEO DEFINITIVO DE RESURRECCIÓN DE PARTIDOS MOCK EN SUPABASE
-- Copia y pega esto en: Supabase Dashboard -> SQL Editor -> New Query -> Run
-- =========================================================================

-- 1. Eliminar cualquier dato residual de match-1 y match-2
DELETE FROM public.attendance WHERE match_id IN ('match-1', 'match-2');
DELETE FROM public.match_events WHERE match_id IN ('match-1', 'match-2');
DELETE FROM public.matches WHERE id IN ('match-1', 'match-2');

-- 2. Restricción estricta en PostgreSQL: impide físicamente que cualquier cliente vuelva a guardar match-1 o match-2
ALTER TABLE public.matches DROP CONSTRAINT IF EXISTS check_no_mock_matches;
ALTER TABLE public.matches ADD CONSTRAINT check_no_mock_matches CHECK (id NOT IN ('match-1', 'match-2'));
