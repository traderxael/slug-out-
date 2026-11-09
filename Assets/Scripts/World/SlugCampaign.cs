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

        /// Numero de nivel de campana (1..28); 0 en infinito. La historia lo usa para elegir
        /// el fragmento: GameFlow.Nivel puede quedar en otro valor si se inicia por otra via.
        public int nivel;

        /// Desde el nivel 4 caen rocas sobre un punto avisado en el piso.
        public bool rocas;

        public NivelConfig(int t, int m, float i, float v, float d, int c, bool inf = false)
        {
            total = t; maxVivos = m; intervalo = i; velocidad = v; dano = d; capturas = c; infinito = inf;
            tema = ElementType.Fire; anilloInvertido = false; rocas = false; nivel = 0;
        }

        /// A partir de que nivel de campana el anillo funciona al reves.
        public const int AnilloAlRevesDesde = 25;

        /// Desde que nivel empiezan las rocas: lo bastante tarde como para que el jugador ya
        /// sepa pelear antes de tener que mirar el piso.
        public const int RocasDesde = 4;

        // ---------------------------------------------------------------------------------
        // Progresion del jugador.
        //
        // Antes el jugador tenia 120 de vida FIJOS en los 28 niveles, mientras el dano
        // enemigo subia de 4 a 17 (x1.4 en los velocimorphs y hasta x1.75 por elemento) y el
        // cargador del blaster limitaba el disparo a uno cada 2,56 s con carga plena. Medido en
        // Play (arnés de campana, ver Tools/ArneltCampanaMedida.cs): el nivel 1 no terminaba
        // en 90 s, el 8 se ganaba con la mitad de vida y del 12 en adelante el jugador moria:
        // la campaña se trababa en el 9. El muro era aritmetico, no de reflejos.
        //
        // Ahora el jugador crece con la campana: mas vida, cargador mas rapido y el tiro
        // pesado castiga menos. La dificultad sigue subiendo (mas enemigos, mas rapidos, mas
        // capturas) pero el jugador ya no se queda sin aire a mitad del nivel 12.
        // ---------------------------------------------------------------------------------

        /// Vida del jugador en el nivel 1: el mismo numero de antes, para no cambiar el
        /// primer nivel (el que enseña a jugar).
        public const float VidaJugadorBase = 120f;

        /// Vida que suma cada nivel de campana. Con 28 niveles el jugador termina con 930.
        ///
        /// Calibrado contra el modelo medido en Play (Tasks/calculo-curva.mjs, version 4:
        /// recarga real, 2,2 disparos por enemigo, 2,5 s entre golpes recibidos y 12 s de
        /// viaje). Con 30 por nivel el peor margen de supervivencia de los 28 niveles es
        /// 1,18x (en el 14): el jugador siempre llega al final del nivel con aire.
        public const float VidaPorNivel = 30f;

        /// Vida maxima del jugador en un nivel de campana (1..28).
        public static float VidaDelJugador(int nivel)
        {
            int n = Mathf.Clamp(nivel, 1, TotalNiveles);
            return VidaJugadorBase + (n - 1) * VidaPorNivel;
        }

        /// Vida en el modo infinito: arranca donde termina la campana (sin escalon al entrar)
        /// y sigue creciendo con la oleada.
        public static float VidaDelJugadorEnInfinito(int oleada)
        {
            return VidaDelJugador(TotalNiveles) + Mathf.Max(0, oleada - 1) * VidaPorNivel;
        }

        /// Recarga de un tubo del blaster en el nivel 1: la de antes (1,6 s).
        public const float RecargaTuboBase = 1.6f;

        /// Recarga de un tubo en el final de la campana. El cargador era el techo del dano
        /// del jugador: con 6 tubos y 2,56 s por tiro cargado daba 41 disparos en un nivel de
        /// 90 s, y el nivel 28 necesita ~48 solo para neutralizar a sus enemigos.
        public const float RecargaTuboMinima = 0.95f;

        /// Recarga por tubo segun el nivel: el equipo mejora al avanzar en la campana.
        public static float RecargaPorTuboParaNivel(int nivel)
        {
            int n = Mathf.Clamp(nivel, 1, TotalNiveles);
            float t = TotalNiveles > 1 ? (n - 1) / (float)(TotalNiveles - 1) : 0f;
            return Mathf.Lerp(RecargaTuboBase, RecargaTuboMinima, t);
        }

        /// Cuanto cuesta el peso de un tiro cargado: la recarga siguiente se multiplica por
        /// este numero (1,6 = el de antes).
        public const float PenalizacionDisparoPesadoBase = 1.6f;

        /// En el final de la campana el tiro pesado castiga menos. La decision sigue siendo
        /// real (cargar sigue costando movilidad y recarga), pero ya no vacia el cargador.
        public const float PenalizacionDisparoPesadoMinima = 1.35f;

        public static float PenalizacionDisparoPesadoParaNivel(int nivel)
        {
            int n = Mathf.Clamp(nivel, 1, TotalNiveles);
            float t = TotalNiveles > 1 ? (n - 1) / (float)(TotalNiveles - 1) : 0f;
            return Mathf.Lerp(PenalizacionDisparoPesadoBase, PenalizacionDisparoPesadoMinima, t);
        }

        /// Golpe maximo que puede pegar un enemigo del nivel: el dano de la curva ajustado al
        /// balance tactico (7/8), el x1.4 de los velocimorphs (enemigos desde el numero 10) y
        /// el multiplicador elemental adverso (1.75 es el maximo de la tabla).
        public static float GolpeEnemigoMaximo(int nivel, bool evolucionado)
        {
            float golpe = SlugArenaGame.AjusteDanoEnemigoTactico(Campana(nivel).dano);
            if (evolucionado) golpe *= 1.4f;
            return golpe * 1.75f;
        }

        /// Golpe TIPICO del nivel, medido en Play: el dano de la curva ajustado (7/8), el
        /// promedio de enemigos evolucionados (x1.4 desde el enemigo numero 10) y el
        /// multiplicador de arquetipo (x1.4 de media entre contacto y salto).
        ///
        /// No lleva el multiplicador elemental: el roster del nivel es mixto y el jugador
        /// lleva el elemento que le conviene, asi que el elemental adverso (hasta x1.75)
        /// es el caso raro, no el ritmo normal. Para ese caso esta `GolpeEnemigoMaximo`.
        public const float FactorArquetipo = 1.4f;

        /// Multiplicador de dano de los velocimorphs (enemigos desde el numero 10).
        public const float FactorVelocimorph = 1.4f;

        /// Golpe tipico del nivel (sin el elemento adverso).
        public static float GolpeEnemigoTipico(int nivel)
        {
            NivelConfig cfg = Campana(nivel);
            int total = Mathf.Max(1, cfg.total);
            int evolucionados = Mathf.Max(0, total - 9);
            float factorEvolucion = 1f + (FactorVelocimorph - 1f) * (evolucionados / (float)total);
            float golpe = SlugArenaGame.AjusteDanoEnemigoTactico(cfg.dano);
            return golpe * factorEvolucion * FactorArquetipo;
        }

        /// El nivel tiene enemigos evolucionados? Los velocimorphs aparecen desde el enemigo
        /// numero 10 (SlugArenaGame.SpawnEnemy), asi que un nivel con menos de 10 enemigos
        /// NO tiene ninguno: su peor golpe es el de un enemigo normal.
        public static bool TieneEnemigosEvolucionados(int nivel)
        {
            return Campana(nivel).total >= 10;
        }

        /// El peor golpe que puede pegar un enemigo que EXISTE en ese nivel.
        public static float PeorGolpeDelNivel(int nivel)
        {
            return GolpeEnemigoMaximo(nivel, TieneEnemigosEvolucionados(nivel));
        }

        /// Segundos que aguanta el jugador si lo tocan sin parar con el PEOR golpe del nivel.
        /// Los i-frames son el techo: aunque lo rodeen seis enemigos entra un golpe cada
        /// medio segundo.
        public static float SegundosDeSupervivenciaExtrema(int nivel)
        {
            float golpesPorSegundo = 1f / Mathf.Max(0.05f, SlugActor.InvulnerabilidadTrasGolpe);
            float golpe = Mathf.Max(0.01f, PeorGolpeDelNivel(nivel));
            return VidaDelJugador(nivel) / (golpe * golpesPorSegundo);
        }

        /// Segundos que aguanta el jugador con el ritmo REAL de golpes (medido en Play: un
        /// golpe cada 2,5 s, no cada 0,5 s) y el golpe tipico del nivel.
        public static float SegundosDeSupervivencia(int nivel)
        {
            float golpe = Mathf.Max(0.01f, GolpeEnemigoTipico(nivel));
            float dps = golpe / Mathf.Max(0.05f, IntervaloEntreGolpes);
            return VidaDelJugador(nivel) / Mathf.Max(0.01f, dps);
        }

        /// Ritmo real de golpes recibidos, medido en Play: 2,5 s entre un golpe y el
        /// siguiente (no 0,5 s: los i-frames son el techo, no el ritmo de juego).
        public const float IntervaloEntreGolpes = 2.5f;

        /// Tiempo de viaje y busqueda dentro del nivel antes de que empiecen los disparos
        /// (medido en Play: ~12 s para rodear la cueva y llegar al primer enemigo).
        public const float SegundosDeViaje = 12f;

        /// Tubos del cargador del blaster.
        public const int TubosCargador = 6;

        /// Disparos que hacen falta para neutralizar a todos los enemigos de un nivel.
        /// Medido en Play: 13 tiros para 6 enemigos en el nivel 1, o sea 2,2 por enemigo
        /// (no 2: el ultimo tiro suele ser el de la captura).
        public const float DisparosPorEnemigo = 2.2f;

        /// Segundos que tarda en recargar un tubo en el peor caso del nivel (tiro a tope):
        /// la recarga por tubo multiplicada por la penalizacion TOTAL del tiro pesado.
        /// El nivel 1 queda en 1,6 x 1,6 = 2,56 s, igual que antes de estos cambios.
        public static float RecargaEfectivaPorTubo(int nivel)
        {
            return RecargaPorTuboParaNivel(nivel) * PenalizacionDisparoPesadoParaNivel(nivel);
        }

        /// Disparos que da el cargador en un tiempo dado, con el nivel como referencia.
        /// Los 6 tubos arrancan LLENOS: se disparan al instante y despues se recarga uno
        /// cada `RecargaEfectivaPorTubo` segundos.
        public static float DisparosDisponibles(int nivel, float segundos)
        {
            float recarga = Mathf.Max(0.05f, RecargaEfectivaPorTubo(nivel));
            return TubosCargador + Mathf.Max(0f, segundos) / recarga;
        }

        /// Disparos que pide el nivel para neutralizar a todos sus enemigos.
        public static float DisparosNecesarios(int nivel)
        {
            return Campana(nivel).total * DisparosPorEnemigo;
        }

        /// Cuanto dura el nivel, segun el modelo medido: los disparos que pide, menos los
        /// 6 que salen sin recargar, a la recarga efectiva, mas el tiempo de viaje.
        public static float DuracionEstimadaDelNivel(int nivel)
        {
            float tiros = DisparosNecesarios(nivel);
            float porTubo = RecargaEfectivaPorTubo(nivel);
            return Mathf.Max(0f, tiros - TubosCargador) * porTubo + SegundosDeViaje;
        }

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
                    // El dano sube hasta 12 y ahi se queda: el techo del dano enemigo no puede
                    // crecer mas rapido que la vida del jugador (que ahora sube con el nivel),
                    // o a partir del 13 cualquier contacto mataba. La presion sigue creciendo
                    // por otro lado: mas enemigos, mas vivos simultaneos, menos respiro entre
                    // apariciones y mas capturas.
                    F( 6, 2, 2.00f, 2.60f,  4f,  1), F( 7, 2, 1.90f, 2.80f,  5f,  1),
                    F( 8, 3, 1.80f, 3.00f,  5f,  1), F( 8, 3, 1.70f, 3.10f,  6f,  2),
                    F( 9, 3, 1.60f, 3.20f,  6f,  2), F(10, 3, 1.60f, 3.30f,  6f,  2),
                    F(10, 3, 1.50f, 3.40f,  7f,  2), F(11, 3, 1.50f, 3.50f,  7f,  3),
                    F(12, 3, 1.40f, 3.60f,  7f,  3), F(12, 4, 1.40f, 3.70f,  8f,  3),
                    F(13, 4, 1.30f, 3.80f,  8f,  4), F(14, 4, 1.20f, 3.90f,  9f,  4),
                    F(14, 4, 1.20f, 4.00f,  9f,  4), F(15, 4, 1.18f, 4.10f, 10f,  4),
                    F(15, 5, 1.16f, 4.20f, 10f,  5), F(16, 5, 1.14f, 4.30f, 10f,  5),
                    F(16, 5, 1.12f, 4.35f, 10f,  5), F(17, 5, 1.10f, 4.40f, 11f,  6),
                    F(18, 5, 1.08f, 4.50f, 11f,  6), F(18, 6, 1.06f, 4.60f, 11f,  6),
                    F(19, 6, 1.04f, 4.70f, 11f,  7), F(20, 6, 1.02f, 4.80f, 11f,  7),
                    F(20, 6, 1.00f, 4.90f, 12f,  8), F(21, 6, 0.98f, 5.00f, 12f,  8),
                    F(22, 6, 0.96f, 5.00f, 12f,  8), F(22, 6, 0.94f, 5.05f, 12f,  9),
                    F(23, 6, 0.92f, 5.15f, 12f,  9), F(24, 6, 0.90f, 5.30f, 12f, 10),
                };
                for (int i = 0; i < f.Length; i++)
                {
                    f[i].nivel = i + 1;
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
