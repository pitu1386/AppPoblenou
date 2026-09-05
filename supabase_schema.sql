-- ==========================================================
-- CLUB ATLÈTIC POBLENOU - ESQUEMA BASE (SUPABASE) · v2.4
-- Solo para una instalación desde cero. Borra y recrea todo.
-- Después de ejecutarlo, ejecuta migracion_auth_rls.sql para
-- activar Supabase Auth, RLS, funciones RPC y Realtime.
-- ==========================================================

DROP TABLE IF EXISTS public.match_events CASCADE;
DROP TABLE IF EXISTS public.match_lineups CASCADE;
DROP TABLE IF EXISTS public.attendance CASCADE;
DROP TABLE IF EXISTS public.payments CASCADE;
DROP TABLE IF EXISTS public.team_expenses CASCADE;
DROP TABLE IF EXISTS public.matches CASCADE;
DROP TABLE IF EXISTS public.profiles CASCADE;
DROP TABLE IF EXISTS public.rival_teams CASCADE;
DROP TABLE IF EXISTS public.announcements CASCADE;
DROP TABLE IF EXISTS public.club_settings CASCADE;

-- 1. PROFILES (Jugadores y Cuerpo Técnico). La contraseña vive en Supabase Auth.
CREATE TABLE public.profiles (
    id TEXT PRIMARY KEY,
    auth_uid UUID UNIQUE,
    full_name TEXT NOT NULL,
    nickname TEXT,
    jersey_number INTEGER,
    position INTEGER DEFAULT 2, -- 0: Portero, 1: Defensa, 2: Centrocampista, 3: Delantero, 4: Cuerpo Técnico
    foot INTEGER DEFAULT 0,     -- 0: Diestro, 1: Zurdo, 2: Ambidiestro
    role INTEGER DEFAULT 3,     -- 0: Admin, 1: Treasurer, 2: FieldManager, 3: Player, 4: Coach
    is_captain BOOLEAN DEFAULT FALSE,
    is_sub_captain BOOLEAN DEFAULT FALSE,
    phone TEXT,
    email TEXT,
    birth_date DATE,
    dni TEXT,
    medical_notes TEXT,
    avatar_url TEXT,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- 2. MATCHES (Partidos y Resultados)
CREATE TABLE public.matches (
    id TEXT PRIMARY KEY,
    round INTEGER DEFAULT 1,
    match_date TIMESTAMPTZ NOT NULL,
    opponent TEXT NOT NULL,
    rival_team_id TEXT,
    competition TEXT DEFAULT 'Sábados División Honor (Temp. 26/27)',
    location_name TEXT NOT NULL DEFAULT 'Camp Municipal Agapito Fernández',
    location_url TEXT DEFAULT 'https://maps.google.com/?q=Camp+Municipal+de+Futbol+Agapito+Fernandez+Barcelona',
    is_home BOOLEAN DEFAULT TRUE,
    our_score INTEGER,
    rival_score INTEGER,
    status INTEGER DEFAULT 0, -- 0: Upcoming, 1: Finished, 2: Cancelled
    notes TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    CONSTRAINT check_no_mock_matches CHECK (id NOT IN ('match-1', 'match-2'))
);

-- 3. ATTENDANCE (Convocatorias y Asistencia)
CREATE TABLE public.attendance (
    id TEXT PRIMARY KEY,
    match_id TEXT NOT NULL REFERENCES public.matches(id) ON DELETE CASCADE,
    player_id TEXT NOT NULL REFERENCES public.profiles(id) ON DELETE CASCADE,
    status INTEGER NOT NULL DEFAULT 0, -- 0: Going, 1: NotGoing, 2: Maybe
    note TEXT,
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    CONSTRAINT unique_match_player UNIQUE (match_id, player_id)
);

-- 4. PAYMENTS (Cuotas y Cobros)
CREATE TABLE public.payments (
    id TEXT PRIMARY KEY,
    player_id TEXT NOT NULL REFERENCES public.profiles(id) ON DELETE CASCADE,
    concept TEXT NOT NULL,
    amount NUMERIC(10,2) NOT NULL DEFAULT 15.00,
    status INTEGER NOT NULL DEFAULT 0, -- 0: Pending, 1: Paid
    due_date DATE,
    paid_at TIMESTAMPTZ,
    method INTEGER DEFAULT 0,         -- 0: Bizum, 1: Cash, 2: Transfer
    notes TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- 5. TEAM EXPENSES (Gastos de Caja Común)
CREATE TABLE public.team_expenses (
    id TEXT PRIMARY KEY,
    concept TEXT NOT NULL,
    amount NUMERIC(10,2) NOT NULL,
    expense_date DATE NOT NULL DEFAULT CURRENT_DATE,
    category TEXT DEFAULT 'Otros',    -- Árbitros, Campos, Material, Tercer Tiempo, Inscripción, Otros
    paid_by TEXT,
    paid_by_player_id TEXT REFERENCES public.profiles(id) ON DELETE SET NULL,
    receipt_url TEXT,
    notes TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- 6. MATCH EVENTS (Goles, Asistencias, Tarjetas, MVP)
CREATE TABLE public.match_events (
    id TEXT PRIMARY KEY,
    match_id TEXT NOT NULL REFERENCES public.matches(id) ON DELETE CASCADE,
    player_id TEXT NOT NULL REFERENCES public.profiles(id) ON DELETE CASCADE,
    event_type INTEGER NOT NULL,      -- 0: Goal, 1: Assist, 2: YellowCard, 3: RedCard, 4: Mvp
    minute INTEGER,
    notes TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- 6b. MATCH LINEUPS (Alineación guardada de la pizarra táctica de cada partido)
CREATE TABLE public.match_lineups (
    match_id TEXT PRIMARY KEY REFERENCES public.matches(id) ON DELETE CASCADE,
    formation TEXT NOT NULL DEFAULT '4-3-3',
    starting_player_ids JSONB NOT NULL DEFAULT '[]'::jsonb, -- 11 huecos en orden; null = hueco vacío
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- 7. RIVAL TEAMS (Equipos Rivales)
CREATE TABLE public.rival_teams (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    primary_color_hex TEXT DEFAULT '#1E3A8A',
    secondary_color_hex TEXT DEFAULT '#FFFFFF',
    kit_description TEXT,
    notes TEXT
);

-- 8. ANNOUNCEMENTS (Tablón de Anuncios y Encuestas)
CREATE TABLE public.announcements (
    id TEXT PRIMARY KEY,
    title TEXT NOT NULL,
    content TEXT NOT NULL,
    author_name TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    has_poll BOOLEAN DEFAULT FALSE,
    poll_options JSONB DEFAULT '[]'::jsonb,
    votes JSONB DEFAULT '{}'::jsonb,
    is_pinned BOOLEAN DEFAULT FALSE,
    is_active BOOLEAN DEFAULT TRUE
);

-- 9. CLUB SETTINGS & SEASONS
CREATE TABLE public.club_settings (
    id TEXT PRIMARY KEY DEFAULT 'current',
    club_name TEXT NOT NULL DEFAULT 'Atletic Poblenou',
    short_name TEXT DEFAULT 'ATºPOBLENOU',
    league_name TEXT DEFAULT 'Sábados División Honor (Temp. 26/27)',
    season_name TEXT DEFAULT 'TEMP 26/27',
    primary_color_hex TEXT DEFAULT '#E53935',
    secondary_color_hex TEXT DEFAULT '#FFFFFF',
    kit_description TEXT DEFAULT 'Rojiblanca a rayas verticales',
    away_kit_primary_color_hex TEXT DEFAULT '#141210',
    away_kit_secondary_color_hex TEXT DEFAULT '#FFFFFF',
    away_kit_description TEXT DEFAULT '',
    home_venue_name TEXT DEFAULT 'Camp Municipal Agapito Fernández',
    home_venue_maps_url TEXT DEFAULT 'https://maps.google.com/?q=Camp+Municipal+de+Futbol+Agapito+Fernandez+Barcelona',
    season_fee_per_player NUMERIC(10,2) DEFAULT 200.00,
    team_secret_code TEXT DEFAULT 'APN1929',
    season_history JSONB DEFAULT '[]'::jsonb
);

-- ==========================================================
-- DATOS INICIALES
-- ==========================================================

-- Administrador principal. Su cuenta de Auth la crea migracion_auth_rls.sql
-- con la contraseña inicial '1234' (cámbiala desde la app al entrar).
INSERT INTO public.profiles (id, full_name, nickname, jersey_number, position, foot, role, is_captain, phone, email, is_active)
VALUES ('user-1', 'Pitu', 'pitu1386', 10, 2, 0, 0, false, '', 'pitu1386@atleticpoblenou.cat', true);

-- Rivales de la liga
INSERT INTO public.rival_teams (id, name, primary_color_hex, secondary_color_hex, kit_description)
VALUES
('team-1', 'FONTETAS', '#EAB308', '#15803D', 'Amarillo y Verde'),
('team-2', 'LA PEÑA', '#DC2626', '#FFFFFF', 'Rojo y Blanco'),
('team-3', 'ARISTOI B', '#1E3A8A', '#FFFFFF', 'Azul Marino y Blanco'),
('team-4', 'LA PLANADA A', '#EA580C', '#000000', 'Naranja y Negro'),
('team-6', 'LLANO', '#16A34A', '#FFFFFF', 'Verde y Blanco'),
('team-7', 'CAN ROCA74', '#2563EB', '#FACC15', 'Azul y Amarillo'),
('team-8', 'LA PLANADA B', '#F97316', '#FFFFFF', 'Naranja y Blanco'),
('team-9', 'LLIÇA D’AVALL', '#DC2626', '#FACC15', 'Rojo y Amarillo'),
('team-10', 'ATºBADIENSE', '#3B82F6', '#FFFFFF', 'Azul y Blanco'),
('team-11', 'CDPV BADIA', '#15803D', '#000000', 'Verde y Negro'),
('team-12', 'ATºLA CELESTE', '#0284C7', '#FFFFFF', 'Celeste y Blanco'),
('team-13', 'STA PERPETUA', '#1D4ED8', '#EF4444', 'Azul y Rojo'),
('team-14', 'PUEBLO NUEVO 2002', '#991B1B', '#000000', 'Granate y Negro'),
('team-15', 'ARISTOI A', '#1E3A8A', '#F59E0B', 'Azul Marino y Dorado');

-- Ajustes del club
INSERT INTO public.club_settings (id, club_name, short_name, league_name, season_name, season_fee_per_player, team_secret_code)
VALUES ('current', 'Atletic Poblenou', 'ATºPOBLENOU', 'Sábados División Honor (Temp. 26/27)', 'TEMP 26/27', 200.00, 'APN1929')
ON CONFLICT (id) DO NOTHING;
