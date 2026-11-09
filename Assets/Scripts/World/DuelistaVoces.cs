using System.Collections.Generic;

namespace SlugOut.World
{
    /// Nombres y frases de los duelistas del mazo. Tabla pura (sin estado de Unity): el HUD la
    /// consulta una vez al aparecer el rival y no arma strings por cuadro, y EditMode la puede probar.
    public static class DuelistaVoces
    {
        public struct Voz
        {
            public string id;
            public string nombre;
            public string[] frases;
        }

        private static readonly Voz[] _voces =
        {
            new Voz { id = "kord", nombre = "KORD", frases = new[]
            {
                "El que aguanta, gana.",
                "Otra vez vos. Dale.",
                "No me pegues en el casco.",
                "Cuando bajes, contá conmigo.",
            } },
            new Voz { id = "trixie", nombre = "TRIXIE", frases = new[]
            {
                "Anoté todo lo que hiciste mal.",
                "El anillo miente; mirá el color.",
                "No te distraigas con las luces.",
                "Si salís, lo escribo.",
            } },
            new Voz { id = "pronto", nombre = "PRONTO", frases = new[]
            {
                "No era mi agua, pero era mi sed.",
                "Yo cubro la retaguardia. Desde acá.",
                "¿Eso era tuyo? Perdón.",
                "Un topo siempre encuentra la salida.",
            } },
            new Voz { id = "dr-blakk", nombre = "DR. BLAKK", frases = new[]
            {
                "Otra vez midiéndote, chico.",
                "Tu padre bajó con la misma cara.",
                "La cueva no te debe nada.",
                "No peleo para ganarte. Peleo para saber cuánto aguantás.",
                "El sello no se rompe: se hereda.",
                "Bajá. Te espero abajo.",
            } },
        };

        // Cursor por duelista: cada aparicion usa la frase siguiente y da la vuelta al agotarlas.
        // Vive entre niveles (se reinicia al empezar una aventura) para que Blakk no repita
        // siempre la primera frase en cada nivel, que es lo que pasaria con un cursor por partida.
        private static readonly Dictionary<string, int> _cursores = new Dictionary<string, int>();

        public static bool TryVoz(string id, out Voz voz)
        {
            for (int i = 0; i < _voces.Length; i++)
            {
                if (_voces[i].id == id) { voz = _voces[i]; return true; }
            }
            voz = default(Voz);
            return false;
        }

        public static string NombreDe(string id)
        {
            Voz v;
            return TryVoz(id, out v) ? v.nombre : (id ?? "").ToUpperInvariant();
        }

        public static string Siguiente(string id)
        {
            int indice;
            return Siguiente(id, out indice);
        }

        /// Igual que Siguiente(id) pero devuelve el indice de la frase: el clip de voz
        /// `Voces/duelistas/<id>/<indice>` tiene que corresponder al texto que muestra el HUD.
        public static string Siguiente(string id, out int indice)
        {
            indice = -1;
            Voz v;
            if (!TryVoz(id, out v) || v.frases == null || v.frases.Length == 0) return "";

            int cursor;
            _cursores.TryGetValue(id, out cursor);
            if (cursor < 0) cursor = 0;
            indice = cursor % v.frases.Length;
            string frase = v.frases[indice];
            _cursores[id] = (indice + 1) % v.frases.Length;
            return frase;
        }

        /// Tabla completa para el generador de voces y los tests. No mutar.
        public static Voz[] Voces()
        {
            return _voces;
        }

        public static void ReiniciarPartida()
        {
            _cursores.Clear();
        }
    }
}
