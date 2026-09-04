-- =========================================================================
-- HABILITAR PERMISOS TOTALES PARA LA APP (ATLETIC POBLENOU) EN SUPABASE
-- Copia y pega esto en: Supabase Dashboard -> SQL Editor -> New Query -> Run
-- =========================================================================

-- 1. Desactivar RLS (Row Level Security) en todas las tablas
ALTER TABLE public.profiles DISABLE ROW LEVEL SECURITY;
ALTER TABLE public.matches DISABLE ROW LEVEL SECURITY;
ALTER TABLE public.attendance DISABLE ROW LEVEL SECURITY;
ALTER TABLE public.payments DISABLE ROW LEVEL SECURITY;
ALTER TABLE public.team_expenses DISABLE ROW LEVEL SECURITY;
ALTER TABLE public.match_events DISABLE ROW LEVEL SECURITY;
ALTER TABLE public.rival_teams DISABLE ROW LEVEL SECURITY;
ALTER TABLE public.announcements DISABLE ROW LEVEL SECURITY;
ALTER TABLE public.club_settings DISABLE ROW LEVEL SECURITY;

-- 1b. Asegurar compatibilidad de columnas adicionales
ALTER TABLE public.matches ADD COLUMN IF NOT EXISTS round INTEGER DEFAULT 1;
ALTER TABLE public.team_expenses ALTER COLUMN category TYPE TEXT;
ALTER TABLE public.team_expenses ADD COLUMN IF NOT EXISTS paid_by TEXT;

-- 2. Conceder permisos de lectura, escritura y modificación
GRANT ALL ON ALL TABLES IN SCHEMA public TO anon, authenticated, service_role;
GRANT ALL ON ALL SEQUENCES IN SCHEMA public TO anon, authenticated, service_role;

-- 3. Políticas abiertas de respaldo para la clave pública
DROP POLICY IF EXISTS "Permiso Total Anon" ON public.profiles;
CREATE POLICY "Permiso Total Anon" ON public.profiles FOR ALL TO anon, authenticated USING (true) WITH CHECK (true);

DROP POLICY IF EXISTS "Permiso Total Anon" ON public.matches;
CREATE POLICY "Permiso Total Anon" ON public.matches FOR ALL TO anon, authenticated USING (true) WITH CHECK (true);

DROP POLICY IF EXISTS "Permiso Total Anon" ON public.attendance;
CREATE POLICY "Permiso Total Anon" ON public.attendance FOR ALL TO anon, authenticated USING (true) WITH CHECK (true);

DROP POLICY IF EXISTS "Permiso Total Anon" ON public.payments;
CREATE POLICY "Permiso Total Anon" ON public.payments FOR ALL TO anon, authenticated USING (true) WITH CHECK (true);

DROP POLICY IF EXISTS "Permiso Total Anon" ON public.team_expenses;
CREATE POLICY "Permiso Total Anon" ON public.team_expenses FOR ALL TO anon, authenticated USING (true) WITH CHECK (true);

DROP POLICY IF EXISTS "Permiso Total Anon" ON public.match_events;
CREATE POLICY "Permiso Total Anon" ON public.match_events FOR ALL TO anon, authenticated USING (true) WITH CHECK (true);

DROP POLICY IF EXISTS "Permiso Total Anon" ON public.rival_teams;
CREATE POLICY "Permiso Total Anon" ON public.rival_teams FOR ALL TO anon, authenticated USING (true) WITH CHECK (true);

DROP POLICY IF EXISTS "Permiso Total Anon" ON public.announcements;
CREATE POLICY "Permiso Total Anon" ON public.announcements FOR ALL TO anon, authenticated USING (true) WITH CHECK (true);

DROP POLICY IF EXISTS "Permiso Total Anon" ON public.club_settings;
CREATE POLICY "Permiso Total Anon" ON public.club_settings FOR ALL TO anon, authenticated USING (true) WITH CHECK (true);
