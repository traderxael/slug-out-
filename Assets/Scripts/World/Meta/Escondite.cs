using System.Collections.Generic;
using UnityEngine;

namespace SlugOut.World
{
    /// Escondite: mejoras permanentes compradas con cristales, aplicadas al iniciar cada nivel.
    /// Se guarda en PlayerPrefs (slugout.cristales, slugout.mejoras).
    public static class Escondite
    {
        private const string CLAVE_CRISTALES = "slugout.cristales";
        private const string CLAVE_MEJORAS = "slugout.mejoras";

        public static int Cristales
        {
            get { return PlayerPrefs.GetInt(CLAVE_CRISTALES, 0); }
            set { PlayerPrefs.SetInt(CLAVE_CRISTALES, Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        /// Nivel alcanzado de cada mejora permanente (se aplica en cada nivel).
        static readonly Dictionary<string, int> _niveles = new Dictionary<string, int>();
        static bool _cargado;

        static void Cargar()
        {
            if (_cargado) return;
            _cargado = true;
            string json = PlayerPrefs.GetString(CLAVE_MEJORAS, "");
            if (!string.IsNullOrEmpty(json))
            {
                var save = JsonUtility.FromJson<MejoraSave>(json);
                foreach (var m in save.mejoras) _niveles[m.id] = m.nivel;
            }
        }

        static void Guardar()
        {
            var lista = new List<MejoraEntry>();
            foreach (var kv in _niveles) lista.Add(new MejoraEntry { id = kv.Key, nivel = kv.Value });
            PlayerPrefs.SetString(CLAVE_MEJORAS, JsonUtility.ToJson(new MejoraSave { mejoras = lista }));
            PlayerPrefs.Save();
        }

        public static int NivelDe(string id)
        {
            Cargar();
            _niveles.TryGetValue(id, out int v);
            return v;
        }

        /// Compra un nivel de mejora si hay cristales suficientes. Devuelve true si se compró.
        public static bool Comprar(string id, int costo)
        {
            if (Cristales < costo) return false;
            int n = NivelDe(id) + 1;
            _niveles[id] = n;
            Cristales -= costo;
            Guardar();
            return true;
        }

        /// Aplica todas las mejoras permanentes al jugador al iniciar un nivel.
        public static void AplicarAlJugador(SlugActor jugador, PlayerShooter shooter)
        {
            Cargar();
            // Vida base +20 por nivel de mejora "reserva".
            int vidaNivel = NivelDe("mej_vida");
            if (vidaNivel > 0 && jugador != null && jugador.IsPlayer)
                jugador.AumentarVidaMaxima(vidaNivel * 20f);

            // Recarga más rápida: -8% por nivel de mejora "cadencia".
            int cadNivel = NivelDe("mej_cadencia");
            if (cadNivel > 0 && shooter != null)
            {
                float nueva = shooter.RecargaPorTubo * (1f - 0.08f * cadNivel);
                shooter.SetRecargaPorTubo(Mathf.Max(0.15f, nueva));
            }

            // Daño base +10% por nivel de mejora "fuerza".
            int fuerNivel = NivelDe("mej_fuerza");
            if (fuerNivel > 0 && shooter != null)
                shooter.AgregarBonoDanio(0.10f * fuerNivel);

            // Umbral de captura más bajo: -0.03 por nivel de mejora "captura".
            int capNivel = NivelDe("mej_captura");
            if (capNivel > 0 && jugador != null && jugador.IsPlayer)
                jugador.FijarUmbralCaptura(Mathf.Max(0.15f, jugador.UmbralCaptura - 0.03f * capNivel));
        }

        /// Gana cristales (se llama al capturar y al completar un nivel).
        public static void GanarCristales(int cantidad)
        {
            if (cantidad <= 0) return;
            Cristales += cantidad;
        }

        [System.Serializable] class MejoraEntry { public string id; public int nivel; }
        [System.Serializable] class MejoraSave { public List<MejoraEntry> mejoras = new List<MejoraEntry>(); }

        /// Costos de cada mejora (por nivel).
        public static int CostoDe(string id)
        {
            int n = NivelDe(id) + 1;
            switch (id)
            {
                case "mej_vida": return 50 + n * 25;
                case "mej_cadencia": return 60 + n * 30;
                case "mej_fuerza": return 70 + n * 35;
                case "mej_captura": return 40 + n * 20;
                default: return 50 + n * 25;
            }
        }

        public static string NombreDe(string id)
        {
            switch (id)
            {
                case "mej_vida": return "Reserva de vida";
                case "mej_cadencia": return "Recarga veloz";
                case "mej_fuerza": return "Fuerza de tiro";
                case "mej_captura": return "Captura fácil";
                default: return id;
            }
        }

        public static string DescripcionDe(string id)
        {
            switch (id)
            {
                case "mej_vida": return "+20 de vida máxima por nivel";
                case "mej_cadencia": return "-8 % de recarga por tubo";
                case "mej_fuerza": return "+10 % de daño por disparo";
                case "mej_captura": return "Umbral de captura más bajo";
                default: return "";
            }
        }
    }
}
