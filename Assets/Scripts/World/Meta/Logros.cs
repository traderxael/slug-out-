using System.Collections.Generic;
using UnityEngine;

namespace SlugOut.World
{
    /// Logros: condiciones desbloqueables durante la partida, guardados en PlayerPrefs.
    public static class Logros
    {
        private const string CLAVE = "slugout.logros";

        public class Logro
        {
            public string id;
            public string nombre;
            public string descripcion;
            public bool desbloqueado;
        }

        static readonly List<Logro> _todos = new List<Logro>
        {
            new Logro { id = "primera_captura", nombre = "Primer abrazo", descripcion = "Captura tu primera babosa." },
            new Logro { id = "coleccionista", nombre = "Coleccionista", descripcion = "Captura 10 babosas distintas." },
            new Logro { id = "skill_shot", nombre = "Skill shot", descripcion = "Derrota a un volador solo con tiro a 100 MPH." },
            new Logro { id = "blakk_fase3", nombre = "Frente a Blakk", descripcion = "Llega a la fase 3 de Dr. Blakk." },
            new Logro { id = "campana_completa", nombre = "Campaña completa", descripcion = "Completa los 28 niveles." },
            new Logro { id = "cinco_estrellas", nombre = "Cinco estrellas", descripcion = "Consigue 5 estrellas en total." },
        };

        static readonly HashSet<string> _desbloqueados = new HashSet<string>();
        static bool _cargado;

        static void Cargar()
        {
            if (_cargado) return;
            _cargado = true;
            string csv = PlayerPrefs.GetString(CLAVE, "");
            if (!string.IsNullOrEmpty(csv))
                foreach (var id in csv.Split(','))
                    if (!string.IsNullOrEmpty(id)) _desbloqueados.Add(id.Trim());
        }

        static void Guardar()
        {
            PlayerPrefs.SetString(CLAVE, string.Join(",", _desbloqueados));
            PlayerPrefs.Save();
        }

        public static IReadOnlyList<Logro> Todos
        {
            get
            {
                Cargar();
                foreach (var l in _todos) l.desbloqueado = _desbloqueados.Contains(l.id);
                return _todos;
            }
        }

        public static bool EstaDesbloqueado(string id)
        {
            Cargar();
            return _desbloqueados.Contains(id);
        }

        /// Desbloquea un logro. Devuelve true si era nuevo.
        public static bool Desbloquear(string id)
        {
            Cargar();
            if (_desbloqueados.Contains(id)) return false;
            _desbloqueados.Add(id);
            Guardar();
            Debug.Log("[Logros] Desbloqueado: " + id);
            return true;
        }

        /// Verifica condiciones simples que se pueden comprobar desde fuera.
        public static void Verificar(int capturasTotales, bool voladorConSkillShot, bool blakkFase3, int nivelAlcanzado, int estrellasTotal)
        {
            if (capturasTotales >= 1) Desbloquear("primera_captura");
            if (capturasTotales >= 10) Desbloquear("coleccionista");
            if (voladorConSkillShot) Desbloquear("skill_shot");
            if (blakkFase3) Desbloquear("blakk_fase3");
            if (nivelAlcanzado >= SlugStory.TotalCampana) Desbloquear("campana_completa");
            if (estrellasTotal >= 5) Desbloquear("cinco_estrellas");
        }
    }
}
