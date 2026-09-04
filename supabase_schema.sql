-- ==========================================================
-- CLUB ATLÈTIC POBLENOU - SUPABASE DATABASE SCHEMA
-- ==========================================================

-- 1. EXTENSIONS
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- 2. ENUMS & DOMAINS
-- Role helper function to check if current user is admin
CREATE OR REPLACE FUNCTION is_admin() 
RETURNS BOOLEAN AS $$
BEGIN
  RETURN EXISTS (
    SELECT 1 FROM public.profiles 
    WHERE id = auth.uid() AND role = 'admin'
  );
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

-- 3. PROFILES TABLE
CREATE TABLE IF NOT EXISTS public.profiles (
    id UUID PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
    full_name TEXT NOT NULL,
    nickname TEXT,
    jersey_number INTEGER,
    position TEXT DEFAULT 'Jugador', -- 'Portero', 'Defensa', 'Medio', 'Delantero'
    role TEXT NOT NULL DEFAULT 'player' CHECK (role IN ('admin', 'treasurer', 'field_manager', 'player')),
    is_captain BOOLEAN DEFAULT FALSE,
    phone TEXT,
    avatar_url TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- 4. MATCHES TABLE
CREATE TABLE IF NOT EXISTS public.matches (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    match_date TIMESTAMPTZ NOT NULL,
    opponent TEXT NOT NULL,
    competition TEXT DEFAULT 'Liga Veteranos Barcelona',
    location_name TEXT NOT NULL DEFAULT 'Camp Municipal Agapito Fernández (Poblenou)',
    location_url TEXT DEFAULT 'https://maps.google.com/?q=Camp+Municipal+de+Futbol+Agapito+Fernandez+Barcelona',
    is_home BOOLEAN DEFAULT TRUE,
    our_score INTEGER,
    rival_score INTEGER,
    status TEXT NOT NULL DEFAULT 'upcoming' CHECK (status IN ('upcoming', 'finished', 'cancelled')),
    notes TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- 5. ATTENDANCE TABLE (RSVP)
CREATE TABLE IF NOT EXISTS public.attendance (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    match_id UUID NOT NULL REFERENCES public.matches(id) ON DELETE CASCADE,
    player_id UUID NOT NULL REFERENCES public.profiles(id) ON DELETE CASCADE,
    status TEXT NOT NULL CHECK (status IN ('going', 'not_going', 'maybe')),
    note TEXT,
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    CONSTRAINT unique_match_player UNIQUE (match_id, player_id)
);

-- 6. PAYMENTS TABLE
CREATE TABLE IF NOT EXISTS public.payments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    player_id UUID NOT NULL REFERENCES public.profiles(id) ON DELETE CASCADE,
    concept TEXT NOT NULL, -- ej: 'Cuota Mensual Octubre', 'Ficha Liga'
    amount NUMERIC(10,2) NOT NULL DEFAULT 15.00,
    status TEXT NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'paid')),
    due_date DATE,
    paid_at TIMESTAMPTZ,
    payment_method TEXT CHECK (payment_method IN ('bizum', 'cash', 'transfer')),
    notes TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- 7. TEAM EXPENSES TABLE (Caja común)
CREATE TABLE IF NOT EXISTS public.team_expenses (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    concept TEXT NOT NULL, -- ej: 'Arbitraje Jornada 3', 'Alquiler pista'
    amount NUMERIC(10,2) NOT NULL,
    expense_date DATE NOT NULL DEFAULT CURRENT_DATE,
    category TEXT DEFAULT 'Árbitros' CHECK (category IN ('Árbitros', 'Pistas', 'Material', 'Tercer Tiempo', 'Inscripción', 'Otros')),
    paid_by UUID REFERENCES public.profiles(id) ON DELETE SET NULL,
    receipt_url TEXT,
    notes TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- 8. MATCH EVENTS TABLE (Goles, Tarjetas, MVP)
CREATE TABLE IF NOT EXISTS public.match_events (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    match_id UUID NOT NULL REFERENCES public.matches(id) ON DELETE CASCADE,
    player_id UUID NOT NULL REFERENCES public.profiles(id) ON DELETE CASCADE,
    event_type TEXT NOT NULL CHECK (event_type IN ('goal', 'assist', 'yellow_card', 'red_card', 'mvp')),
    minute INTEGER,
    notes TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- ==========================================================
-- ROW LEVEL SECURITY (RLS) POLICIES
-- ==========================================================
ALTER TABLE public.profiles ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.matches ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.attendance ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.payments ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.team_expenses ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.match_events ENABLE ROW LEVEL SECURITY;

-- Profiles: Anyone authenticated can read, users can update own profile, admins can update any
CREATE POLICY "Public profiles are viewable by everyone authenticated"
ON public.profiles FOR SELECT TO authenticated USING (true);

CREATE POLICY "Users can update own profile"
ON public.profiles FOR UPDATE TO authenticated USING (auth.uid() = id);

CREATE POLICY "Admins can update any profile"
ON public.profiles FOR UPDATE TO authenticated USING (is_admin());

CREATE POLICY "Admins can insert profiles"
ON public.profiles FOR INSERT TO authenticated WITH CHECK (true);

-- Matches: Everyone can view, only admins can insert/update/delete
CREATE POLICY "Matches viewable by authenticated users"
ON public.matches FOR SELECT TO authenticated USING (true);

CREATE POLICY "Admins can manage matches"
ON public.matches FOR ALL TO authenticated USING (is_admin());

-- Attendance: Everyone can view, users can update their own attendance, admins can manage all
CREATE POLICY "Attendance viewable by authenticated users"
ON public.attendance FOR SELECT TO authenticated USING (true);

CREATE POLICY "Users can insert/update own attendance"
ON public.attendance FOR INSERT TO authenticated 
WITH CHECK (auth.uid() = player_id);

CREATE POLICY "Users can update own attendance record"
ON public.attendance FOR UPDATE TO authenticated 
USING (auth.uid() = player_id);

CREATE POLICY "Admins can manage all attendance"
ON public.attendance FOR ALL TO authenticated USING (is_admin());

-- Payments: Everyone can view (transparency), only admins can insert/update/delete
CREATE POLICY "Payments viewable by authenticated users"
ON public.payments FOR SELECT TO authenticated USING (true);

CREATE POLICY "Admins can manage payments"
ON public.payments FOR ALL TO authenticated USING (is_admin());

-- Team Expenses: Everyone can view, only admins can manage
CREATE POLICY "Expenses viewable by authenticated users"
ON public.team_expenses FOR SELECT TO authenticated USING (true);

CREATE POLICY "Admins can manage expenses"
ON public.team_expenses FOR ALL TO authenticated USING (is_admin());

-- Match Events: Everyone can view, only admins can manage
CREATE POLICY "Match events viewable by authenticated users"
ON public.match_events FOR SELECT TO authenticated USING (true);

CREATE POLICY "Admins can manage match events"
ON public.match_events FOR ALL TO authenticated USING (is_admin());

-- ==========================================================
-- AUTH TRIGGER: AUTO-CREATE PROFILE ON SIGNUP
-- ==========================================================
CREATE OR REPLACE FUNCTION public.handle_new_user()
RETURNS TRIGGER AS $$
DECLARE
  first_user BOOLEAN;
BEGIN
  -- Check if this is the first user registered; if so, make them admin automatically!
  SELECT NOT EXISTS (SELECT 1 FROM public.profiles) INTO first_user;

  INSERT INTO public.profiles (id, full_name, nickname, role, avatar_url)
  VALUES (
    NEW.id,
    COALESCE(NEW.raw_user_meta_data->>'full_name', split_part(NEW.email, '@', 1)),
    COALESCE(NEW.raw_user_meta_data->>'nickname', split_part(NEW.email, '@', 1)),
    CASE WHEN first_user THEN 'admin' ELSE 'player' END,
    NEW.raw_user_meta_data->>'avatar_url'
  );
  RETURN NEW;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

DROP TRIGGER IF EXISTS on_auth_user_created ON auth.users;
CREATE TRIGGER on_auth_user_created
  AFTER INSERT ON auth.users
  FOR EACH ROW EXECUTE FUNCTION public.handle_new_user();

-- ==========================================================
-- SEED DATA (Optional fixtures to get started immediately)
-- ==========================================================
INSERT INTO public.matches (match_date, opponent, competition, location_name, location_url, is_home, status, notes)
VALUES 
(
  NOW() + INTERVAL '3 days 20 hours', 
  'FONTETAS', 
  'Sábados División Honor (Temp. 26/27) - Jornada 1',
  'Camp Municipal Agapito Fernández (Poblenou)',
  'https://maps.google.com/?q=Camp+Municipal+de+Futbol+Agapito+Fernandez+Barcelona',
  true,
  'upcoming',
  'Llevar camiseta rojiblanca y estar 30 min antes para calentar.'
),
(
  NOW() + INTERVAL '10 days 19 hours', 
  'LA PEÑA', 
  'Sábados División Honor (Temp. 26/27) - Jornada 2',
  'CEM Poblenou - Can Felipa',
  'https://maps.google.com/?q=Can+Felipa+Poblenou+Barcelona',
  false,
  'upcoming',
  'Llevar camiseta suplente por si coinciden colores. Confirmar asistencia.'
)
ON CONFLICT DO NOTHING;

