using UnityEngine;
using SlugOut.Core;

namespace SlugOut.World
{
    /// Configuracion de un nivel de campana: cantidad de enemigos, presion y objetivo de captura.
    /// El numero de nivel coincide con el fragmento de SlugStory.DeCampana(nivel).
    public struct NivelConfig
    {
        public int total;
        public int maxVivos;
        public float intervalo;
        public float velocidad;
        public float dano;
        public int capturas;
        public bool infinito;

        /// Elemento que abre la tanda: los enemigos de ese elemento llegan primero.
        public ElementType tema;

        /// Desde el nivel 25 el anillo se da vuelta, como promete el texto del 24.
        public bool anilloInvertido;

        /// Desde el nivel 4 caen rocas sobre un punto avisado en el piso.
        public bool rocas;

        public NivelConfig(int t, int m, float i, float v, float d, int c, bool inf = false)
        {
            total = t; maxVivos = m; intervalo = i; velocidad = v; dano = d; capturas = c; infinito = inf;
            tema = ElementType.Fire; anilloInvertido = false; rocas = false;
        }

        /// A partir de que nivel de campana el anillo funciona al reves.
        public const int AnilloAlRevesDesde = 25;

        /// Desde que nivel empiezan las rocas: lo bastante tarde como para que el jugador ya
        /// sepa pelear antes de tener que mirar el piso.
        public const int RocasDesde = 4;

        private static NivelConfig[] _filas;

        /// Una fila por nivel: del 1 al 28 ninguno repite configuracion. Antes del 13 la curva
        /// esta medida en Play; del 13 en adelante sigue subiendo y se congela en 25-26 para que
        /// el cambio de ley elemental sea lo unico nuevo en pantalla.
        public static int TotalNiveles { get { return Campana().Length; } }

        public static NivelConfig[] Campana()
        {
            if (_filas == null)
            {
                NivelConfig[] f =
                {
                    F( 6, 2, 2.00f, 2.60f,  4f,  1), F( 7, 2, 1.90f, 2.80f,  5f,  1),
                    F( 8, 3, 1.80f, 3.00f,  5f,  1), F( 8, 3, 1.70f, 3.10f,  6f,  2),
                    F( 9, 3, 1.60f, 3.20f,  6f,  2), F(10, 3, 1.60f, 3.30f,  6f,  2),
                    F(10, 3, 1.50f, 3.40f,  7f,  2), F(11, 3, 1.50f, 3.50f,  7f,  3),
                    F(12, 3, 1.40f, 3.60f,  7f,  3), F(12, 4, 1.40f, 3.70f,  8f,  3),
                    F(13, 4, 1.30f, 3.80f,  8f,  4), F(14, 4, 1.20f, 3.90f,  9f,  4),
                    F(14, 4, 1.20f, 4.00f,  9f,  4), F(15, 4, 1.18f, 4.10f, 10f,  4),
                    F(15, 5, 1.16f, 4.20f, 10f,  5), F(16, 5, 1.14f, 4.30f, 11f,  5),
                    F(16, 5, 1.12f, 4.35f, 11f,  5), F(17, 5, 1.10f, 4.40f, 12f,  6),
                    F(18, 5, 1.08f, 4.50f, 12f,  6), F(18, 6, 1.06f, 4.60f, 13f,  6),
                    F(19, 6, 1.04f, 4.70f, 13f,  7), F(20, 6, 1.02f, 4.80f, 14f,  7),
                    F(20, 6, 1.00f, 4.90f, 14f,  8), F(21, 6, 0.98f, 5.00f, 15f,  8),
                    F(22, 6, 0.96f, 5.00f, 15f,  8), F(22, 6, 0.94f, 5.05f, 15f,  9),
                    F(23, 6, 0.92f, 5.15f, 16f,  9), F(24, 6, 0.90f, 5.30f, 17f, 10),
                };
                for (int i = 0; i < f.Length; i++)
                {
                    f[i].tema = SlugStory.TemaDeNivel(i + 1);
                    f[i].anilloInvertido = (i + 1) >= AnilloAlRevesDesde;
                    f[i].rocas = (i + 1) >= RocasDesde;
                }
                _filas = f;
            }
            return _filas;
        }

        private static NivelConfig F(int t, int m, float i, float v, float d, int c)
        {
            return new NivelConfig(t, m, i, v, d, c);
        }

        public static NivelConfig Campana(int nivel)
        {
            NivelConfig[] f = Campana();
            int n = Mathf.Clamp(nivel, 1, f.Length);
            return f[n - 1];
        }

        public static NivelConfig Infinito()
        {
            // total=int.MaxValue: con 20 el modo se cerraba en la oleada 5 y los fragmentos de
            // la 24, 31, 38, 45 y 52 (SlugStory.Infinito) eran inalcanzables por construccion.
            // El techo de vivos (4) y la rampa por oleada son los que sostienen la dificultad.
            NivelConfig c = new NivelConfig(int.MaxValue, 4, 1.4f, 3.4f, 6f, 0, true);
            c.rocas = true;
            return c;
        }

        public static string NombreTema(ElementType e)
        {
            switch (e)
            {
                case ElementType.Fire: return "Fuego";
                case ElementType.Ice: return "Hielo";
                case ElementType.Plant: return "Planta";
                case ElementType.Electric: return "Rayo";
                case ElementType.Earth: return "Tierra";
                case ElementType.Shadow: return "Sombra";
                case ElementType.Air: return "Aire";
                case ElementType.Water: return "Agua";
                case ElementType.Energy: return "Energia";
                case ElementType.Light: return "Luz";
                case ElementType.Toxic: return "Toxico";
                default: return e.ToString();
            }
        }

        public string Objetivo
        {
            get
            {
                string base_ = infinito ? "Aguanta las " + total + " oleadas"
                                        : "Baja a " + total + " · captura " + capturas;
                return anilloInvertido ? base_ + " · anillo al reves" : base_;
            }
        }
    }
}
