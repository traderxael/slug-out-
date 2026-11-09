using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SlugOut.Slugs;
using SlugOut.World;
using UnityEngine;

namespace SlugOut.EditModeTests
{
    /// Cuarto arquetipo: pelea a distancia. Su promesa de diseno es legible en tres piezas:
    ///  - aviso de 0,5 s antes de escupir, con recuperacion en la que queda expuesto;
    ///  - la cobertura corta su tiro y su aviso (misma ley que el auto-aim del jugador);
    ///  - acorralado retrocede y, si lo alcanzas, muerde flojo.
    public class EscupidorTests
    {
        private GameObject _jugador;
        private GameObject _enemigo;
        private GameObject _muros;
        private SlugActor _actorJugador;
        private SlugActor _actorEnemigo;
        private EnemySlugAI _ai;
        private SlugData _data;

        [SetUp]
        public void Crear()
        {
            _data = SlugRoster.Buscar("centella");
            if (_data == null) _data = SlugRoster.Todos[0];

            _jugador = new GameObject("JugadorEscupidor");
            _jugador.transform.position = Vector3.zero;
            _actorJugador = _jugador.AddComponent<SlugActor>();
            _actorJugador.Configure(_data, true);

            _enemigo = new GameObject("EnemigoEscupidor");
            _enemigo.transform.position = new Vector3(0f, 0f, 8f);
            _actorEnemigo = _enemigo.AddComponent<SlugActor>();
            _actorEnemigo.Configure(_data, false);
            _ai = _enemigo.AddComponent<EnemySlugAI>();
            _ai.SetTarget(_jugador.transform);
            _ai.SetArquetipo(EnemySlugAI.Arquetipo.Escupidor);
        }

        [TearDown]
        public void Limpiar()
        {
            DestroyAll<SlugShot>();
            if (_muros != null) Object.DestroyImmediate(_muros);
            if (_enemigo != null) Object.DestroyImmediate(_enemigo);
            if (_jugador != null) Object.DestroyImmediate(_jugador);
        }

        [Test]
        public void ElCuartoArquetipoSaleDelStatQueNadieUsabaSinRobarLasOtrasSiluetas()
        {
            SlugData d = ScriptableObject.CreateInstance<SlugData>();
            try
            {
                d.clashPower = 50f; d.accelerationRate = 60f; d.launchSpeedMph = 55f;
                Assert.AreEqual(EnemySlugAI.Arquetipo.Escupidor, EnemySlugAI.ArquetipoDe(d),
                    "launchSpeed >= 55 es el unico stat que quedaba libre: tiene que dar Escupidor");

                d.launchSpeedMph = 54.9f;
                Assert.AreEqual(EnemySlugAI.Arquetipo.Frenetico, EnemySlugAI.ArquetipoDe(d),
                    "por debajo del umbral no puede aparecer el cuarto arquetipo");

                d.clashPower = 60f; d.launchSpeedMph = 70f;
                Assert.AreEqual(EnemySlugAI.Arquetipo.Embestidor, EnemySlugAI.ArquetipoDe(d),
                    "el Escupidor no puede robarle los cuerpos a cuerpo de clashPower alto");

                d.clashPower = 44f; d.accelerationRate = 90f; d.launchSpeedMph = 70f;
                Assert.AreEqual(EnemySlugAI.Arquetipo.Saltarin, EnemySlugAI.ArquetipoDe(d),
                    "el Escupidor no puede robarle los saltarines");

                Assert.AreEqual(EnemySlugAI.Arquetipo.Frenetico, EnemySlugAI.ArquetipoDe(null));
            }
            finally { Object.DestroyImmediate(d); }
        }

        /// La mezcla que el plan prometio con el roster real: 30/5/23/6. Se fija aca porque
        /// el cuarto arquetipo se deriva de stats y cualquier tuning futuro la mueve en silencio.
        [Test]
        public void ElRosterReparteTreintaEmbestidoresCincoSaltarinesSeisEscupidoresYVeintitresFreneticos()
        {
            int embestidores = 0, saltarines = 0, freneticos = 0, escupidores = 0;
            foreach (SlugData d in SlugRoster.Todos)
            {
                switch (EnemySlugAI.ArquetipoDe(d))
                {
                    case EnemySlugAI.Arquetipo.Embestidor: embestidores++; break;
                    case EnemySlugAI.Arquetipo.Saltarin: saltarines++; break;
                    case EnemySlugAI.Arquetipo.Escupidor: escupidores++; break;
                    default: freneticos++; break;
                }
            }
            Assert.AreEqual(30, embestidores);
            Assert.AreEqual(5, saltarines);
            Assert.AreEqual(23, freneticos);
            Assert.AreEqual(6, escupidores);
            Assert.AreEqual(SlugRoster.Todos.Length, embestidores + saltarines + freneticos + escupidores,
                "cada babosa tiene que caer en un arquetipo");
        }

        [Test]
        public void ElAvisoNoDisparaYElTiroSaleAlCerrarLaVentana()
        {
            _ai.PeleaEscupidor(0.05f);   // primer cuadro en rango y con linea: arranca el aviso
            Assert.IsTrue(_ai.Disparando, "con el jugador en rango y sin cobertura tiene que avisar");
            Assert.AreEqual(0, _ai.Disparos, "no puede disparar en el mismo cuadro que avisa");

            _ai.PeleaEscupidor(0.4f);
            Assert.AreEqual(0, _ai.Disparos, "durante el aviso no sale el escupitajo");
            Assert.Greater(_ai.ProgresoAviso, 0.5f, "el aviso tiene que dibujarse sobre el cuerpo");

            _ai.PeleaEscupidor(0.2f);    // 0,65 s: cerro la ventana
            Assert.AreEqual(1, _ai.Disparos, "al cerrar el aviso sale exactamente un tiro");
            Assert.IsTrue(_ai.EstaExpuesto, "despues de escupir queda expuesto");
            Assert.IsFalse(_ai.Mordiendo, "el aviso de disparo no es una mordida");

            _ai.PeleaEscupidor(0.5f);
            Assert.IsFalse(_ai.EstaExpuesto, "la recuperacion tiene que terminar");
        }

        [Test]
        public void SinLineaDeVisionNoEmpiezaElAviso()
        {
            float hpInicial = _actorJugador.Hp;
            Muro(4f, 8f);

            for (int i = 0; i < 12; i++) _ai.PeleaEscupidor(0.1f);
            Assert.IsFalse(_ai.Disparando, "detras de una roca no hay aviso: el tiro no saldria");
            Assert.AreEqual(0, _ai.Disparos, "la cobertura tiene que cortar el ataque a distancia");
            Assert.AreEqual(hpInicial, _actorJugador.Hp, 0.001f,
                "sin tiro no puede haber dano: el Escupidor no atraviesa la roca");
        }

        [Test]
        public void PerderLaLineaDuranteElAvisoCancelaElDisparo()
        {
            _ai.PeleaEscupidor(0.05f);
            Assert.IsTrue(_ai.Disparando, "sin cobertura el aviso arranca");

            Muro(4f, 8f);
            _ai.PeleaEscupidor(0.55f);
            Assert.AreEqual(0, _ai.Disparos, "si le tapas la linea durante el aviso, el tiro no sale");
            Assert.IsFalse(_ai.Disparando, "el aviso cancelado no puede quedar encendido");

            QuitarMuro();
            _ai.PeleaEscupidor(0.5f);
            Assert.IsTrue(_ai.Disparando, "al volver a tener linea tiene que rearmar el ataque");
        }

        [Test]
        public void AcorraladoRetrocedeParaRecuperarSuRango()
        {
            _enemigo.transform.position = new Vector3(0f, 0f, 2.8f);
            float antes = DistanciaHorizontal();

            for (int i = 0; i < 10; i++) _ai.PeleaEscupidor(0.05f);

            Assert.Greater(DistanciaHorizontal(), antes + 0.8f,
                "a menos de 4,5 m el Escupidor tiene que abrir distancia, no quedarse plantado");
            Assert.IsFalse(_ai.Disparando, "no puede avisar pegado al jugador: ahi muerde flojo");
        }

        [Test]
        public void AcorraladoPorUnaRocaMuerdeDentroDeAlcance()
        {
            _jugador.transform.position = Vector3.zero;
            _enemigo.transform.position = new Vector3(0f, 0f, 1.5f);
            Muro(2.6f, 4f);   // la retirada hacia +Z queda frenada por la roca
            float hp = _actorJugador.Hp;

            for (int i = 0; i < 10 && !_ai.Mordiendo; i++) _ai.PeleaEscupidor(0.05f);
            Assert.IsTrue(_ai.Mordiendo,
                "con la retirada bloqueada y el jugador a 1,5 m tiene que morder");

            for (int i = 0; i < 6; i++) _ai.PeleaMordida(0.04f);
            Assert.Less(_actorJugador.Hp, hp, "la mordida de emergencia no entro");
            Assert.AreEqual(1, _ai.Mordidas);
        }

        [Test]
        public void LaRetiradaLibreNoMuerdeAunqueElJugadorEsteCerca()
        {
            _jugador.transform.position = Vector3.zero;
            _enemigo.transform.position = new Vector3(0f, 0f, 1.5f);
            for (int i = 0; i < 8; i++) _ai.PeleaEscupidor(0.05f);

            Assert.IsFalse(_ai.Mordiendo, "si puede abrir distancia, retroceder es su prioridad");
            Assert.Greater(DistanciaHorizontal(), 2.2f, "el Escupidor tiene que haberse retirado");
            Assert.AreEqual(0, _ai.Mordidas);
        }

        [Test]
        public void LaSegundaMordidaEsperaElCooldown()
        {
            _jugador.transform.position = Vector3.zero;
            _enemigo.transform.position = new Vector3(0f, 0f, 1.5f);
            Muro(2.6f, 4f);
            for (int i = 0; i < 10 && !_ai.Mordiendo; i++) _ai.PeleaEscupidor(0.05f);
            Assert.IsTrue(_ai.Mordiendo);
            for (int i = 0; i < 6; i++) _ai.PeleaMordida(0.04f);
            Assert.AreEqual(1, _ai.Mordidas);

            for (int i = 0; i < 10; i++) _ai.PeleaEscupidor(0.1f);   // 1 s: aun no
            Assert.IsFalse(_ai.Mordiendo, "la segunda mordida no puede saltarse el cooldown");
            for (int i = 0; i < 12 && !_ai.Mordiendo; i++) _ai.PeleaEscupidor(0.1f);
            Assert.IsTrue(_ai.Mordiendo, "pasado el cooldown tiene que volver a morder");
        }

        [Test]
        public void AterrizarPegaEnLaPosicionRealYNoEnElDestinoViejo()
        {
            _ai.SetDamage(10f);
            _jugador.transform.position = new Vector3(0f, 0f, 5.5f);
            _enemigo.transform.position = new Vector3(0f, 0f, 5f);
            float hp = _actorJugador.Hp;

            MethodInfo aterrizar = typeof(EnemySlugAI).GetMethod("Aterrizar",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(aterrizar, "cambio el nombre de Aterrizar");
            aterrizar.Invoke(_ai, null);

            Assert.Less(_actorJugador.Hp, hp,
                "el golpe tiene que medirse desde donde aterrizo, no desde el destino viejo");
            Assert.AreEqual(1, _ai.AterrizajesConGolpe);
        }

        [Test]
        public void AterrizarSaleDeUnaRocaYApoyaEnElSuelo()
        {
            _jugador.transform.position = new Vector3(0f, 0f, 12f);
            _enemigo.transform.position = new Vector3(0f, 0f, 6f);
            Muro(6f, 3.2f);

            MethodInfo aterrizar = typeof(EnemySlugAI).GetMethod("Aterrizar",
                BindingFlags.NonPublic | BindingFlags.Instance);
            aterrizar.Invoke(_ai, null);

            Vector3 p = _enemigo.transform.position;
            Assert.IsFalse(ColisionJugador.SobreMuro(p + Vector3.up, 0.5f, 2f),
                "el Saltarin quedo dentro de la roca al aterrizar");
            Assert.AreEqual(Terrain3D.Altura(p.x, p.z), p.y, 0.001f,
                "despues del empuje la altura tiene que ser la del terreno nuevo");
        }

        [Test]
        public void LaCoberturaTambienFrenaAlEscupidorQueRetrocede()
        {
            // Retrocede hacia el muro: si la cobertura solo cortara tiros, lo atravesaria.
            // Arranca fuera del volumen del muro: el barrido resuelve contacto, no solape inicial.
            _jugador.transform.position = new Vector3(0f, 0f, 6.5f);
            _enemigo.transform.position = new Vector3(0f, 0f, 7f);
            Muro(8.7f, 6f);

            for (int i = 0; i < 20; i++) _ai.PeleaEscupidor(0.05f);

            Assert.Less(_enemigo.transform.position.z, 8.05f,
                "el Escupidor atraveso la cobertura al retroceder");
        }

        [Test]
        public void LaMordidaDelEscupidorEsFlojaComparadaConLaDeUnCuerpoACuerpo()
        {
            var jugadorCuerpo = new GameObject("JugadorCuerpo");
            var enemigoCuerpo = new GameObject("EnemigoCuerpo");
            try
            {
                jugadorCuerpo.transform.position = new Vector3(10f, 0f, 0f);
                SlugActor actorCuerpo = jugadorCuerpo.AddComponent<SlugActor>();
                actorCuerpo.Configure(_data, true);

                enemigoCuerpo.transform.position = new Vector3(10f, 0f, 1.2f);
                SlugActor actorEnemigoCuerpo = enemigoCuerpo.AddComponent<SlugActor>();
                actorEnemigoCuerpo.Configure(_data, false);
                EnemySlugAI cuerpo = enemigoCuerpo.AddComponent<EnemySlugAI>();
                cuerpo.SetTarget(jugadorCuerpo.transform);
                cuerpo.SetDamage(10f);   // sin SetArquetipo: queda en el Frenetico por defecto

                _enemigo.transform.position = new Vector3(0f, 0f, 1.2f);
                _ai.SetDamage(10f);

                float hpA = _actorJugador.Hp;
                float hpB = actorCuerpo.Hp;
                _ai.EmpezarMordida();
                cuerpo.EmpezarMordida();
                for (int i = 0; i < 6; i++) { _ai.PeleaMordida(0.04f); cuerpo.PeleaMordida(0.04f); }

                float danoEscupidor = hpA - _actorJugador.Hp;
                float danoCuerpo = hpB - actorCuerpo.Hp;
                Assert.Greater(danoCuerpo, 0f, "el control cuerpo a cuerpo no mordio");
                Assert.AreEqual(0.6f, danoEscupidor / danoCuerpo, 0.02f,
                    "acorralar al Escupidor no puede castigar como una mordida completa");
            }
            finally
            {
                if (enemigoCuerpo != null) Object.DestroyImmediate(enemigoCuerpo);
                if (jugadorCuerpo != null) Object.DestroyImmediate(jugadorCuerpo);
            }
        }

        [Test]
        public void ElAutoAimDescartaAlEnemigoTapadoPorLaCobertura()
        {
            var cercano = new GameObject("CercanoTapado");
            var lejano = new GameObject("LejanoVisible");
            var actores = new List<SlugActor>();
            try
            {
                cercano.transform.position = new Vector3(0f, 0f, 5f);
                SlugActor a = cercano.AddComponent<SlugActor>();
                a.Configure(_data, false);
                lejano.transform.position = new Vector3(0f, 0f, 9f);
                SlugActor b = lejano.AddComponent<SlugActor>();
                b.Configure(_data, false);
                actores.Add(a); actores.Add(b);

                Assert.AreSame(a, PlayerShooter.ElegirObjetivo(actores, Vector3.zero,
                    Vector3.forward, 30f, 0f), "sin cobertura el auto-aim elige al mas cercano");
                Assert.AreSame(b, PlayerShooter.ElegirObjetivo(actores, Vector3.zero,
                    Vector3.forward, 30f, 0f, x => x != a),
                    "si el mas cercano esta tapado, tiene que pasar al siguiente visible");
                Assert.IsNull(PlayerShooter.ElegirObjetivo(actores, Vector3.zero,
                    Vector3.forward, 30f, 0f, x => false),
                    "si todos estan tapados no hay objetivo que fijar");
            }
            finally
            {
                if (cercano != null) Object.DestroyImmediate(cercano);
                if (lejano != null) Object.DestroyImmediate(lejano);
            }
        }

        float DistanciaHorizontal()
        {
            Vector3 d = _enemigo.transform.position - _jugador.transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        void Muro(float z, float ancho)
        {
            if (_muros == null) _muros = new GameObject("MurosPrueba");
            MurosCueva.Caja(_muros.transform, "muro_prueba",
                new Vector3(0f, 1.5f, z), Quaternion.identity,
                new Vector3(ancho, 3f, 0.8f), 8);
            Physics.SyncTransforms();
        }

        void QuitarMuro()
        {
            if (_muros != null) Object.DestroyImmediate(_muros);
            _muros = null;
            Physics.SyncTransforms();
        }

        static void DestroyAll<T>() where T : Component
        {
            T[] vivos = Object.FindObjectsByType<T>();
            for (int i = 0; i < vivos.Length; i++)
                if (vivos[i] != null) Object.DestroyImmediate(vivos[i].gameObject);
        }
    }
}
