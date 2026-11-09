using System.Collections;
using NUnit.Framework;
using SlugOut.Core;
using SlugOut.Slugs;
using SlugOut.World;
using UnityEngine;
using UnityEngine.TestTools;

namespace SlugOut.PlayModeTests
{
    /// El bando del proyectil se prueba en Play: fuera de Play los SlugActor no se registran
    /// en `Vivos` (OnEnable no corre) y el tiro no encontraria a nadie. En Play, en cambio,
    /// se usa el mismo camino que el juego.
    public class EscupidorPlayTests
    {
        private readonly System.Collections.Generic.List<GameObject> _creados = new System.Collections.Generic.List<GameObject>();
        private static readonly Vector3 Origen = new Vector3(500f, 0f, 500f);

        [TearDown]
        public void LimpiarBarridos()
        {
            for (int i = _creados.Count - 1; i >= 0; i--)
                if (_creados[i] != null) Object.DestroyImmediate(_creados[i]);
            _creados.Clear();
            Physics.SyncTransforms();
        }

        private SlugActor Actor(Vector3 offset, bool jugador = false)
        {
            var go = new GameObject("ActorBarrido");
            _creados.Add(go);
            go.transform.position = Origen + offset;
            SlugActor actor = go.AddComponent<SlugActor>();
            actor.Configure(SlugRoster.Buscar("centella"), jugador);
            Assert.That(SlugActor.Vivos, Does.Contain(actor), "debe probar actores registrados en Play");
            return actor;
        }

        private SlugShot Tiro(float velocidad, float dano = 4f, SlugActor dueno = null)
        {
            SlugShot tiro = SlugShot.Lanzar(Origen + Vector3.up * 0.8f, Vector3.forward,
                ElementType.Water, dano, true, velocidad, Color.blue);
            _creados.Add(tiro.gameObject);
            tiro.enabled = false;
            tiro.ConfigurarDisparo(dueno, 1f, true);
            return tiro;
        }

        private void Piedra(float z, float ancho = 4f)
        {
            var go = new GameObject("PiedraBarrido");
            _creados.Add(go);
            go.layer = 8;
            go.transform.position = Origen + new Vector3(0f, 1f, z);
            go.AddComponent<BoxCollider>().size = new Vector3(ancho, 3f, 0.1f);
            Physics.SyncTransforms();
        }

        [TestCase(18f, 0.02f, false)] [TestCase(18f, 0.02f, true)]
        [TestCase(18f, 0.1f, false)] [TestCase(18f, 0.1f, true)]
        [TestCase(18f, 0.3333333f, false)] [TestCase(18f, 0.3333333f, true)]
        [TestCase(24f, 0.02f, false)] [TestCase(24f, 0.02f, true)]
        [TestCase(24f, 0.1f, false)] [TestCase(24f, 0.1f, true)]
        [TestCase(24f, 0.3333333f, false)] [TestCase(24f, 0.3333333f, true)]
        [TestCase(62f, 0.02f, false)] [TestCase(62f, 0.02f, true)]
        [TestCase(62f, 0.1f, false)] [TestCase(62f, 0.1f, true)]
        [TestCase(62f, 0.3333333f, false)] [TestCase(62f, 0.3333333f, true)]
        public void BarridoGolpeaAlPrimerRivalEnAmbosOrdenes(float velocidad, float dt, bool inverso)
        {
            SlugActor primero, segundo;
            if (inverso)
            {
                segundo = Actor(Vector3.forward * 4f);
                primero = Actor(Vector3.forward * 2f);
            }
            else
            {
                primero = Actor(Vector3.forward * 2f);
                segundo = Actor(Vector3.forward * 4f);
            }
            float hp1 = primero.Hp, hp2 = segundo.Hp;
            SlugShot tiro = Tiro(velocidad);
            for (int i = 0; i < 40 && primero.Hp == hp1 && segundo.Hp == hp2; i++)
                tiro.Paso(dt);
            Assert.Less(primero.Hp, hp1, "debe golpear la primera entrada al disco, no el ultimo del registro");
            Assert.AreEqual(hp2, segundo.Hp, 0.001f, "el segundo rival no recibe ese tiro");
        }

        [TestCase(18f, false)] [TestCase(18f, true)]
        [TestCase(24f, false)] [TestCase(24f, true)]
        [TestCase(62f, false)] [TestCase(62f, true)]
        public void BarridoOrdenaEntradasDescentradasNoCentros(float velocidad, bool inverso)
        {
            SlugActor primero, centroCercano;
            // Centro 3,1 entra a 2,3; centro 3 con offset 0,79 entra a ~2,874.
            if (inverso)
            {
                primero = Actor(new Vector3(0f, 0f, 3.1f));
                centroCercano = Actor(new Vector3(0.79f, 0f, 3f));
            }
            else
            {
                centroCercano = Actor(new Vector3(0.79f, 0f, 3f));
                primero = Actor(new Vector3(0f, 0f, 3.1f));
            }
            float hp1 = primero.Hp, hp2 = centroCercano.Hp;
            Tiro(velocidad).Paso(0.3333333f);
            Assert.Less(primero.Hp, hp1, "se requiere la raiz de entrada, no proyeccion del centro");
            Assert.AreEqual(hp2, centroCercano.Hp, 0.001f);
        }

        [TestCase(1.8f, false)] [TestCase(5f, true)]
        public void BarridoComparaContactoConMuroAnteriorOPosterior(float muroZ, bool golpea)
        {
            SlugActor rival = Actor(Vector3.forward * 3f);
            float hp = rival.Hp;
            Piedra(muroZ);
            Tiro(62f).Paso(0.2f);
            Assert.AreEqual(golpea, rival.Hp < hp, "el primer contacto del segmento decide el impacto");
        }

        [Test]
        public void BarridoMuroAnteriorBloqueaAunqueLaLineaAlCentroSeaClara()
        {
            SlugActor rival = Actor(new Vector3(0.6f, 0f, 3f));
            float hp = rival.Hp;
            Piedra(1.8f, 0.1f);
            Assert.IsTrue(SlugShot.LineaDespejada(Origen + Vector3.up * 0.8f, rival.transform.position));
            Tiro(62f).Paso(0.2f);
            Assert.AreEqual(hp, rival.Hp, 0.001f, "LOS al centro no sustituye al muro en la trayectoria");
        }

        [Test]
        public void BarridoIgnoraAliadoAntesDelRival()
        {
            SlugActor rival = Actor(Vector3.forward * 3f);
            SlugActor aliado = Actor(Vector3.forward, true);
            float hpRival = rival.Hp, hpAliado = aliado.Hp;
            Tiro(24f).Paso(0.2f);
            Assert.Less(rival.Hp, hpRival);
            Assert.AreEqual(hpAliado, aliado.Hp, 0.001f);
        }

        [TestCase(false)] [TestCase(true)]
        public void BarridoCapturaSupervivienteBajoUmbralPeroNoMuerto(bool letal)
        {
            SlugActor rival = Actor(Vector3.forward * 2f);
            SlugActor dueno = Actor(Vector3.back * 3f, true);
            rival.FijarVidaPrueba(rival.MaxHp * 0.34f);
            float energia = rival.Energia;
            Tiro(24f, letal ? 10000f : 1f, dueno).Paso(0.2f);
            Assert.AreEqual(!letal, rival.Alive);
            Assert.AreEqual(!letal, rival.Capturada, "no capturar muertos ni perder captura de supervivientes");
            Assert.AreEqual(1, dueno.Combo, "conservar el impacto del dueno");
            if (!letal) Assert.Less(rival.Energia, energia, "efecto garantizado antes de captura");
        }

        [Test]
        public void BarridoConsumidoNoRepiteImpactoEnElMismoFrame()
        {
            SlugActor rival = Actor(Vector3.forward * 2f);
            SlugActor dueno = Actor(Vector3.back * 3f, true);
            SlugShot tiro = Tiro(24f, 4f, dueno);
            tiro.Paso(0.2f);
            float hp = rival.Hp, energia = rival.Energia;
            Assert.IsNotNull(tiro, "Destroy aun es diferido en este frame");
            tiro.Paso(0.2f);
            Assert.AreEqual(hp, rival.Hp, 0.001f, "el tiro ya consumido no puede repetir dano");
            Assert.AreEqual(energia, rival.Energia, 0.001f, "tampoco repetir efecto");
            Assert.AreEqual(1, dueno.Combo);
        }

        [TestCase(0f)] [TestCase(-0.1f)]
        public void BarridoSinTiempoNoGolpeaNiAvanza(float dt)
        {
            SlugActor rival = Actor(Vector3.forward * 0.2f);
            float hp = rival.Hp;
            SlugShot tiro = Tiro(24f);
            Vector3 inicio = tiro.transform.position;
            tiro.Paso(dt);
            Assert.AreEqual(hp, rival.Hp, 0.001f);
            Assert.AreEqual(inicio, tiro.transform.position);
        }

        [Test]
        public void BarridoEmpateIdenticoNoDependeDelRegistroEnLaMismaEscena()
        {
            SlugActor a = Actor(Vector3.forward * 2f), b = Actor(Vector3.forward * 2f);
            SlugActor esperado = a.GetEntityId().CompareTo(b.GetEntityId()) < 0 ? a : b;
            SlugActor otro = esperado == a ? b : a;
            for (int i = 0; i < 2; i++)
            {
                a.Configure(a.Data, false); b.Configure(b.Data, false);
                float hp = esperado.Hp, hpOtro = otro.Hp;
                Tiro(24f).Paso(0.2f);
                Assert.Less(esperado.Hp, hp, "desempate estable por EntityId dentro de esta escena");
                Assert.AreEqual(hpOtro, otro.Hp, 0.001f);
                a.gameObject.SetActive(false); b.gameObject.SetActive(false);
                b.gameObject.SetActive(true); a.gameObject.SetActive(true);
            }
        }

        [UnityTest]
        public IEnumerator ElEscupitajoEnemigoGolpeaAlJugadorYNoASusCompaneros()
        {
            SlugData data = SlugRoster.Buscar("centella");
            if (data == null) data = SlugRoster.Todos[0];

            // Lejos del origen para no cruzarse con actores que otra prueba haya dejado vivos.
            Vector3 basePos = new Vector3(100f, 0f, 100f);
            var jugadorGo = new GameObject("JugadorTiro");
            var companeroGo = new GameObject("CompaneroTiro");
            SlugShot tiro = null;
            try
            {
                jugadorGo.transform.position = basePos;
                SlugActor jugador = jugadorGo.AddComponent<SlugActor>();
                jugador.Configure(data, true);

                companeroGo.transform.position = basePos + new Vector3(0f, 0f, 4f);
                SlugActor companero = companeroGo.AddComponent<SlugActor>();
                companero.Configure(data, false);

                yield return null;   // OnEnable registra a los dos en SlugActor.Vivos

                float hpJugador = jugador.Hp;
                float hpCompanero = companero.Hp;

                tiro = SlugShot.Lanzar(basePos + new Vector3(0f, 0.8f, 6f), Vector3.back,
                    ElementType.Earth, 12f, false, 18f, Color.yellow);
                Assert.That(tiro, Is.Not.Null, "el Escupidor no genero el proyectil");
                Assert.IsFalse(tiro.DesdeJugador, "el tiro enemigo tiene que estar marcado como tal");

                tiro.Paso(0.4f);   // 7,2 m: pasa por el companero (4 m) y llega al jugador (6 m)

                Assert.Less(jugador.Hp, hpJugador, "el escupitajo tenia que golpear al jugador");
                Assert.AreEqual(hpCompanero, companero.Hp, 0.001f,
                    "un tiro enemigo no puede danar a otros enemigos");
            }
            finally
            {
                if (tiro != null) Object.DestroyImmediate(tiro.gameObject);
                if (companeroGo != null) Object.DestroyImmediate(companeroGo);
                if (jugadorGo != null) Object.DestroyImmediate(jugadorGo);
            }
        }
    }
}
