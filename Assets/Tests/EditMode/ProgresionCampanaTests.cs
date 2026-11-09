using NUnit.Framework;
using SlugOut.Slugs;
using SlugOut.World;
using UnityEngine;

namespace SlugOut.EditModeTests
{
    /// La campana tiene que ser ganable de punta a punta.
    ///
    /// Medido en Play (arnes de campana, Tools/ArneltCampanaMedida.cs): con 120 de vida
    /// fijos, el cargador a 1,6 s por tubo y el dano enemigo subiendo 4 -> 17, el nivel 1 no
    /// terminaba en 90 s, el 8 se ganaba con la mitad de vida y del 12 en adelante el jugador
    /// moria. La progresion se trababa en el nivel 9 y el muro era aritmetico: el cargador
    /// daba 41 disparos en un nivel de 90 s y el nivel 28 pedia ~48 solo para neutralizar a
    /// sus enemigos.
    ///
    /// Estos tests son el contrato: si alguien vuelve a subir el dano, a bajar la vida o a
    /// dejar el cargador lento, el nivel deja de ser ganable y la suite se cae.
    public class ProgresionCampanaTests
    {
        const int Niveles = 28;

        /// Segundos minimos que el jugador aguanta si lo tocan sin parar (con i-frames).
        const float SueloDeSupervivencia = 6f;

        /// El cargador tiene que dar un 15 % mas de disparos de los que el nivel pide.
        const float HolguraDeDisparos = 1.15f;

        /// Tiempo de referencia de un nivel: el que tardaba el bot antes de estos cambios.
        const float TiempoDeNivel = 90f;

        /// Aire que tiene que sobrar: el jugador aguanta el nivel entero con este margen.
        const float MargenDeSupervivencia = 1.2f;

        [Test] public void LaVidaDelJugadorSubeConLaCampanaYElPrimerNivelNoCambia()
        {
            Assert.AreEqual(120f, NivelConfig.VidaDelJugador(1), 0.001f,
                "el nivel 1 tiene que seguir jugando con 120 de vida: es el que enseña a jugar");
            float anterior = 0f;
            for (int n = 1; n <= Niveles; n++)
            {
                float vida = NivelConfig.VidaDelJugador(n);
                Assert.Greater(vida, anterior, "nivel " + n + ": la vida no sube");
                anterior = vida;
            }
            Assert.AreEqual(SlugActor.VidaJugadorTactica, NivelConfig.VidaDelJugador(1), 0.001f,
                "la base de la progresion tiene que ser la vida tactica de siempre");
            Assert.AreEqual(930f, NivelConfig.VidaDelJugador(Niveles), 0.001f,
                "la vida del final de la campana cambio de numero");
        }

        [Test] public void ElInfinitoEmpiezaDondeTerminaLaCampanaYSigueConLaOleada()
        {
            Assert.AreEqual(NivelConfig.VidaDelJugador(Niveles),
                NivelConfig.VidaDelJugadorEnInfinito(1), 0.001f,
                "al infinito se entra con la vida del final de la campana, no con 120");
            Assert.Greater(NivelConfig.VidaDelJugadorEnInfinito(2),
                NivelConfig.VidaDelJugadorEnInfinito(1), "la vida no crece con la oleada");
        }

        [Test] public void ElCargadorMejoraConLaCampana()
        {
            Assert.AreEqual(1.6f, NivelConfig.RecargaPorTuboParaNivel(1), 0.001f,
                "el nivel 1 conserva la recarga de antes");
            Assert.AreEqual(0.95f, NivelConfig.RecargaPorTuboParaNivel(Niveles), 0.001f);
            float anterior = 99f;
            for (int n = 1; n <= Niveles; n++)
            {
                float recarga = NivelConfig.RecargaPorTuboParaNivel(n);
                Assert.Less(recarga, anterior, "nivel " + n + ": el cargador no mejora");
                anterior = recarga;
            }
            Assert.Greater(NivelConfig.PenalizacionDisparoPesadoParaNivel(Niveles),
                1f, "el tiro pesado tiene que seguir costando recarga");
            Assert.Less(NivelConfig.PenalizacionDisparoPesadoParaNivel(Niveles),
                NivelConfig.PenalizacionDisparoPesadoParaNivel(1),
                "en el final de la campana el tiro pesado castiga menos");
        }

        /// El tiro pesado se cobra UNA vez. Antes la penalizacion vivia metida dentro de
        /// RecargaEscala y la campana le sumo otra por su cuenta: el nivel 1 recargaba a
        /// 4,1 s por tubo en vez de 2,56 s y el jugador disparaba la mitad de veces
        /// (22 tiros medidos donde antes eran 40).
        [Test] public void LaPenalizacionDelTiroPesadoSeCobraUnaSolaVez()
        {
            Assert.AreEqual(1f, PlayerShooter.RecargaEscala(0f, 1.6f), 0.0001f,
                "un tiro sin cargar no puede costar recarga extra");
            Assert.AreEqual(1.6f, PlayerShooter.RecargaEscala(1f, 1.6f), 0.0001f,
                "a tope, la recarga se multiplica por el total y no mas");
            Assert.AreEqual(1.35f, PlayerShooter.RecargaEscala(1f, 1.35f), 0.0001f,
                "la penalizacion del final de campana");

            // El numero que se ve jugando: segundos por tubo con el cargador lleno.
            Assert.AreEqual(2.56f, NivelConfig.RecargaEfectivaPorTubo(1), 0.001f,
                "el nivel 1 tiene que recargar igual que antes: 1,6 x 1,6 = 2,56 s");
            Assert.AreEqual(1.2825f, NivelConfig.RecargaEfectivaPorTubo(Niveles), 0.001f,
                "el final de campana recarga mas rapido: 0,95 x 1,35");
        }

        [Test] public void NingunNivelDejaAlJugadorSinAireParaSobrevivir()
        {
            for (int n = 1; n <= Niveles; n++)
            {
                float segundos = NivelConfig.SegundosDeSupervivenciaExtrema(n);
                Assert.GreaterOrEqual(segundos, SueloDeSupervivencia,
                    "nivel " + n + ": el jugador aguanta solo " + segundos.ToString("F1")
                    + " s si lo tocan sin parar (con el dano maximo del nivel)");
            }
        }

        /// El contrato de fondo: con el ritmo real de golpes (medido en Play) y la recarga
        /// real, el jugador tiene que aguantar el nivel entero con aire.
        [Test] public void ElJugadorAguantaCadaNivelEnteroConAire()
        {
            for (int n = 1; n <= Niveles; n++)
            {
                float aguanta = NivelConfig.SegundosDeSupervivencia(n);
                float dura = NivelConfig.DuracionEstimadaDelNivel(n);
                Assert.GreaterOrEqual(aguanta, dura * MargenDeSupervivencia,
                    "nivel " + n + ": aguanta " + aguanta.ToString("F1") + " s y el nivel dura "
                    + dura.ToString("F1") + " s: no alcanza para terminarlo");
            }
        }

        [Test] public void ElCargadorCubreLosDisparosQuePideCadaNivel()
        {
            for (int n = 1; n <= Niveles; n++)
            {
                float pide = NivelConfig.DisparosNecesarios(n);
                float da = NivelConfig.DisparosDisponibles(n, TiempoDeNivel);
                Assert.GreaterOrEqual(da, pide * HolguraDeDisparos,
                    "nivel " + n + ": el cargador da " + da.ToString("F0")
                    + " disparos en " + TiempoDeNivel + " s y el nivel pide " + pide.ToString("F0"));
            }
        }

        [Test] public void LaCurvaDeDanoSubePeroTieneTecho()
        {
            NivelConfig[] f = NivelConfig.Campana();
            for (int i = 1; i < f.Length; i++)
                Assert.GreaterOrEqual(f[i].dano, f[i - 1].dano,
                    "nivel " + (i + 1) + ": menos dano que en el anterior");
            for (int n = 1; n <= Niveles; n++)
                Assert.LessOrEqual(NivelConfig.Campana(n).dano, 12f,
                    "nivel " + n + ": el dano enemigo paso el techo (12). El techo existe para que "
                    + "el dano no crezca mas rapido que la vida del jugador");
        }

        /// El peor golpe de un nivel solo puede ser el de un enemigo que existe en ese
        /// nivel: los velocimorphs (x1,4 de dano) aparecen desde el enemigo numero 10,
        /// asi que un nivel con menos de 10 enemigos no tiene ninguno.
        [Test] public void ElPeorGolpeDeCadaNivelCuentaSoloLosEnemigosQueExisten()
        {
            for (int n = 1; n <= Niveles; n++)
            {
                bool evolucionado = NivelConfig.Campana(n).total >= 10;
                Assert.AreEqual(evolucionado, NivelConfig.TieneEnemigosEvolucionados(n),
                    "nivel " + n + ": los velocimorphs empiezan en el enemigo numero 10");
                Assert.AreEqual(NivelConfig.GolpeEnemigoMaximo(n, evolucionado),
                    NivelConfig.PeorGolpeDelNivel(n), 0.001f,
                    "nivel " + n + ": el peor golpe no es el del nivel");
            }
        }

        /// Un enemigo que nace dentro de un pilar no puede salir (la IA choca contra el
        /// proxy) y el jugador tampoco lo alcanza con el tiro: el nivel no se termina
        /// nunca. Medido en Play: 6 de las 28 posiciones de aparicion caian dentro de
        /// roca (spawn 03, 13, 14, 16, 24 y 26).
        [Test] public void ElEnemigoNoNaceAdentroDeUnPilarNiDeUnaRoca()
        {
            GameObject roca = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                roca.name = "roca_prueba";
                roca.layer = ColisionJugador.CapaMuros;
                roca.transform.position = Vector3.zero;
                roca.transform.localScale = new Vector3(6f, 6f, 6f);
                Physics.SyncTransforms();

                Vector3 libre = ColisionJugador.PuntoLibre(new Vector3(0f, 0f, 0f),
                    Vector3.forward, 0.5f, 2f);
                Assert.IsFalse(ColisionJugador.SobreMuro(libre, 0.5f, 2f),
                    "el punto libre sigue dentro del solido: el enemigo volveria a aparecer atrapado");
                Assert.Greater(Vector3.Distance(
                    new Vector3(libre.x, 0f, libre.z), Vector3.zero), 3f,
                    "no se movio lo suficiente como para salir de la roca");
            }
            finally
            {
                Object.DestroyImmediate(roca);
            }
        }

        [Test] public void FijarVidaMaximaSoloAfectaAlJugador()
        {
            SlugData electro = SlugRoster.Buscar("electroshock");
            Assert.IsNotNull(electro, "falta el slug electroshock");

            GameObject go = new GameObject("actor_progresion");
            try
            {
                SlugActor actor = go.AddComponent<SlugActor>();
                actor.Configure(electro, false);
                float vidaEnemiga = actor.MaxHp;
                actor.FijarVidaMaxima(500f);
                Assert.AreEqual(vidaEnemiga, actor.MaxHp, 0.001f,
                    "un enemigo no puede cambiar su vida con la progresion del jugador");

                actor.Configure(electro, true);
                actor.FijarVidaMaxima(240f);
                Assert.AreEqual(240f, actor.MaxHp, 0.001f, "la vida del jugador no se aplico");
                Assert.AreEqual(240f, actor.Hp, 0.001f, "el jugador tiene que empezar el nivel con la vida llena");

                actor.Heal(500f);
                actor.AumentarVidaMaxima(10f);
                Assert.AreEqual(250f, actor.MaxHp, 0.001f, "la vida maxima no sumo");
                Assert.AreEqual(250f, actor.Hp, 0.001f,
                    "sumar vida maxima no tiene que curar de mas: el jugador recibe lo que le sobra");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
