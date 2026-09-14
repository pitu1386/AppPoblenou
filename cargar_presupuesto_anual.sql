-- Carga el presupuesto anual estimado por categoría (valores del año pasado).
-- Copia y pega en: Supabase Dashboard -> SQL Editor -> Run
-- Es un merge: no borra otras categorías que ya tengas cargadas.
-- Requiere haber aplicado antes migracion_auth_rls.sql (crea la tabla expense_budget).

update public.expense_budget
   set budget = coalesce(budget, '{}'::jsonb) || jsonb_build_object(
       'Inscripción', 100,
       'Sanciones', 178,
       'Fichas', 654,
       'Árbitros', 1660,
       'Campos', 1275
   )
 where id = 'current';
