-- ==========================================================
-- CLUB ATLÈTIC POBLENOU - FULL DATABASE SCHEMA (SUPABASE)
-- ==========================================================

-- 1. PROFILES (Jugadores y Cuerpo Técnico)
DROP TABLE IF EXISTS public.match_events CASCADE;
DROP TABLE IF EXISTS public.attendance CASCADE;
DROP TABLE IF EXISTS public.payments CASCADE;
DROP TABLE IF EXISTS public.team_expenses CASCADE;
DROP TABLE IF EXISTS public.matches CASCADE;
DROP TABLE IF EXISTS public.profiles CASCADE;
DROP TABLE IF EXISTS public.rival_teams CASCADE;
DROP TABLE IF EXISTS public.announcements CASCADE;
DROP TABLE IF EXISTS public.club_settings CASCADE;

CREATE TABLE public.profiles (
    id TEXT PRIMARY KEY,
    full_name TEXT NOT NULL,
    nickname TEXT,
    jersey_number INTEGER,
    position INTEGER DEFAULT 2, -- 0: Portero, 1: Defensa, 2: Centrocampista, 3: Delantero
    foot INTEGER DEFAULT 0,     -- 0: Diestro, 1: Zurdo, 2: Ambidiestro
    role INTEGER DEFAULT 3,     -- 0: Admin, 1: Treasurer, 2: FieldManager, 3: Player
    is_captain BOOLEAN DEFAULT FALSE,
    is_sub_captain BOOLEAN DEFAULT FALSE,
    phone TEXT,
    email TEXT,
    password TEXT DEFAULT '1234',
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
    match_date TIMESTAMPTZ NOT NULL,
    opponent TEXT NOT NULL,
    rival_team_id TEXT,
    competition TEXT DEFAULT 'Sábados División Honor (Temp. 26/27)',
    location_name TEXT NOT NULL DEFAULT 'Camp Municipal Agapito Fernández',
    location_url TEXT DEFAULT 'https://maps.google.com/?q=Camp+Municipal+de+Futbol+Agapito+Fernandez+Barcelona',
    is_home BOOLEAN DEFAULT TRUE,
    our_score INTEGER,
    rival_score INTEGER,
    status INTEGER DEFAULT 0, -- 0: Upcoming, 1: Finished, 2: Suspended
    notes TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW()
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
    method INTEGER DEFAULT 0,         -- 0: Bizum, 1: Cash, 2: BankTransfer
    notes TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- 5. TEAM EXPENSES (Gastos de Caja Común)
CREATE TABLE public.team_expenses (
    id TEXT PRIMARY KEY,
    concept TEXT NOT NULL,
    amount NUMERIC(10,2) NOT NULL,
    expense_date DATE NOT NULL DEFAULT CURRENT_DATE,
    category INTEGER DEFAULT 0,       -- 0: Arbitros, 1: Canchas, 2: Material, 3: TercerTiempo, 4: Inscripcion, 5: Otros
    paid_by_player_id TEXT REFERENCES public.profiles(id) ON DELETE SET NULL,
    receipt_url TEXT,
    notes TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- 6. MATCH EVENTS (Goles, Asistencias, Tarjetas)
CREATE TABLE public.match_events (
    id TEXT PRIMARY KEY,
    match_id TEXT NOT NULL REFERENCES public.matches(id) ON DELETE CASCADE,
    player_id TEXT NOT NULL REFERENCES public.profiles(id) ON DELETE CASCADE,
    event_type INTEGER NOT NULL,      -- 0: Goal, 1: Assist, 2: YellowCard, 3: RedCard, 4: Mvp
    minute INTEGER,
    notes TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW()
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
    home_venue_name TEXT DEFAULT 'Camp Municipal Agapito Fernández',
    home_venue_maps_url TEXT DEFAULT 'https://maps.google.com/?q=Camp+Municipal+de+Futbol+Agapito+Fernandez+Barcelona',
    season_fee_per_player NUMERIC(10,2) DEFAULT 200.00,
    team_secret_code TEXT DEFAULT 'APN1929',
    season_history JSONB DEFAULT '[]'::jsonb
);

-- ==========================================================
-- PERMISOS PARA LA APP (ANON ACCESS)
-- ==========================================================
ALTER TABLE public.profiles DISABLE ROW LEVEL SECURITY;
ALTER TABLE public.matches DISABLE ROW LEVEL SECURITY;
ALTER TABLE public.attendance DISABLE ROW LEVEL SECURITY;
ALTER TABLE public.payments DISABLE ROW LEVEL SECURITY;
ALTER TABLE public.team_expenses DISABLE ROW LEVEL SECURITY;
ALTER TABLE public.match_events DISABLE ROW LEVEL SECURITY;
ALTER TABLE public.rival_teams DISABLE ROW LEVEL SECURITY;
ALTER TABLE public.announcements DISABLE ROW LEVEL SECURITY;
ALTER TABLE public.club_settings DISABLE ROW LEVEL SECURITY;

-- ==========================================================
-- DATOS INICIALES (Plantilla, Rivales, Partidos y Ajustes)
-- ==========================================================

-- Jugadores iniciales
INSERT INTO public.profiles (id, full_name, nickname, jersey_number, position, foot, role, is_captain, phone, email, password, birth_date, dni, is_active)
VALUES
('user-1', 'Pitu', 'pitu1386', 10, 2, 0, 0, true, '+34 600 00 00 00', 'pitu1386@atleticpoblenou.cat', '1234', '1986-05-14', '47891234X', true),
('user-2', 'Carles Puig', 'Carles', 4, 1, 0, 1, true, '+34 622 33 44 55', 'carles@atleticpoblenou.cat', '1234', '1985-11-23', '46543210Y', true),
('user-3', 'Marc Rovira', 'Marc', 1, 0, 0, 2, false, '+34 633 44 55 66', 'marc@atleticpoblenou.cat', '1234', '1989-02-08', NULL, true),
('user-4', 'Jordi Soler', 'Jordi', 2, 1, 0, 3, false, '+34 644 55 66 77', 'jordi@atleticpoblenou.cat', '1234', '1986-09-30', NULL, true),
('user-5', 'Sergi Vidal', 'Sergi', 3, 1, 1, 3, false, '+34 655 66 77 88', 'sergi@atleticpoblenou.cat', '1234', '1988-04-19', NULL, true),
('user-6', 'Xavi Font', 'Xavi', 6, 2, 2, 3, false, '+34 666 77 88 99', 'xavi@atleticpoblenou.cat', '1234', '1984-12-01', NULL, true),
('user-7', 'Albert Serra', 'Albert', 8, 2, 0, 3, false, '+34 677 88 99 00', 'albert@atleticpoblenou.cat', '1234', '1987-08-11', NULL, true),
('user-8', 'Lluís Martí', 'Lluís', 9, 3, 1, 3, false, '+34 688 99 00 11', 'lluis@atleticpoblenou.cat', '1234', '1986-07-25', NULL, true),
('user-9', 'Pol Navarro', 'Pol', 11, 3, 0, 3, false, '+34 699 00 11 22', 'pol@atleticpoblenou.cat', '1234', '1990-03-17', NULL, true),
('user-10', 'Gerard Mas', 'Geri', 5, 1, 0, 3, false, '+34 612 34 56 78', 'gerard@atleticpoblenou.cat', '1234', '1988-10-05', NULL, true);

-- Rivales
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

-- Partidos
INSERT INTO public.matches (id, match_date, opponent, rival_team_id, competition, location_name, location_url, is_home, status, notes)
VALUES
('match-1', NOW() + INTERVAL '3 days 18 hours', 'FONTETAS', 'team-1', 'Sábados División Honor (Temp. 26/27)', 'Camp Municipal Agapito Fernández', 'https://maps.google.com/?q=Camp+Municipal+de+Futbol+Agapito+Fernandez+Barcelona', true, 0, 'Llevar camiseta rojiblanca titular. Llegar 30 min antes para calentar.'),
('match-2', NOW() + INTERVAL '10 days 17 hours', 'LA PEÑA', 'team-2', 'Sábados División Honor (Temp. 26/27)', 'CEM Poblenou - Can Felipa', 'https://maps.google.com/?q=Can+Felipa+Poblenou+Barcelona', false, 0, 'Llevar las dos camisetas por coincidencia de colores.');

-- Ajustes del club
INSERT INTO public.club_settings (id, club_name, short_name, league_name, season_name, season_fee_per_player, team_secret_code)
VALUES ('current', 'Atletic Poblenou', 'ATºPOBLENOU', 'Sábados División Honor (Temp. 26/27)', 'TEMP 26/27', 200.00, 'APN1929')
ON CONFLICT (id) DO NOTHING;

-- Anuncio inicial
INSERT INTO public.announcements (id, title, content, author_name, has_poll, poll_options, votes, is_pinned, is_active)
VALUES
('ann-1', '🥩 Asado y Tercer Tiempo post-partido', 'Muchachos, después de jugar contra FONTETAS organizamos asado en el club. ¡Voten en la encuesta para calcular la compra!', 'pitu1386 (Capitán)', true, '["Me sumo al asado 🥩", "En duda / aviso el viernes 🤔", "No llego ❌"]'::jsonb, '{"user-1": 0, "user-2": 0, "user-3": 0}'::jsonb, true, true);
