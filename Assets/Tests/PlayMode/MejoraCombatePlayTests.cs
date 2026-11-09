using System.Collections;
using NUnit.Framework;
using SlugOut.Core;
using SlugOut.Slugs;
using SlugOut.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SlugOut.PlayModeTests
{
    /// El softlock del 2026-10-25: bajar al ultimo enemigo sin capturarlo dejaba el nivel sin
    /// enemigos, con la cuota de capturas sin cumplir y sin ningun final. La arena ahora repone
    /// una aparicion; esto lo mide por el camino real (escena, Iniciar, Update, SpawnEnemy).
    public class MejoraCombatePlayTests
    {
        private static SlugActor Vivo()
        {
            System.Collections.Generic.IReadOnlyList<SlugActor> vivos = SlugActor.Vivos;
            for (int i = 0; i < vivos.Count; i++)
            {
                SlugActor a = vivos[i];
                if (a != null && !a.IsPlayer && a.Alive && !a.Capturada) return a;
            }
            return null;
        }

        [UnityTest]
        public IEnumerator BajarAlUnicoEnemigoSinCapturarloReponeAparicion()
        {
            SceneManager.LoadScene("Mapa3D");
            yield return null; yield return null; yield return null;
            Assert.IsNotNull(GameFlow.Activo, "Mapa3D no tiene GameFlow");
            GameFlow.Activo.IniciarCampana(1);
            yield return null;

            // La arena del GameFlow persistente, no la copia de escena que Awake pudo destruir.
            SlugArenaGame arena = GameFlow.Activo.GetComponent<SlugArenaGame>();
            Assert.IsNotNull(arena, "Mapa3D no tiene SlugArenaGame");
            // Un nivel minimo: un enemigo, una captura. Si se lo baja sin capturar, la cuota
            // ya no se puede cumplir con lo que queda.
            arena.Iniciar(new NivelConfig(1, 1, 0.2f, 2.6f, 4f, 1), SlugRoster.Buscar("electroshock"));
            for (int i = 0; i < 25; i++) yield return null;

            StoryPanel historia = Object.FindAnyObjectByType<StoryPanel>();
            if (historia != null) historia.enabled = false;
            GameFeel.Pausa = false;
            Time.timeScale = 1f;
            EnemySlugAI[] ias = Object.FindObjectsByType<EnemySlugAI>();
            for (int i = 0; i < ias.Length; i++) if (ias[i] != null) ias[i].enabled = false;

            SlugActor presa = null;
            for (int i = 0; i < 300 && presa == null; i++)
            {
                presa = Vivo();
                if (presa == null) yield return null;
            }
            Assert.IsNotNull(presa, "el nivel no llego a spawnear al unico enemigo");
            Assert.AreEqual(0, arena.Capturas, "la prueba necesita empezar sin capturas");
            int refuerzosAntes = arena.Refuerzos;

            presa.TakeDamage(99999f, ElementType.Fire);
            for (int i = 0; i < 300 && arena.Refuerzos == refuerzosAntes; i++) yield return null;

            Assert.AreEqual(0, arena.Capturas, "matar no puede contar como captura");
            Assert.Greater(arena.Refuerzos, refuerzosAntes,
                "se bajo al unico enemigo sin capturarlo y la arena no repuso aparicion: " +
                "el nivel queda sin enemigos y sin final");
            Assert.GreaterOrEqual(arena.Spawned, 2, "el refuerzo no paso por SpawnEnemy");
            Assert.IsFalse(arena.Termino, "el nivel termino sin cumplir la cuota de capturas");

            SlugActor refuerzo = null;
            for (int i = 0; i < 300 && refuerzo == null; i++)
            {
                refuerzo = Vivo();
                if (refuerzo == null) yield return null;
            }
            Assert.IsNotNull(refuerzo, "el refuerzo contado no existe como enemigo vivo");
            Debug.Log("MEJORA_COMBATE refuerzos=" + arena.Refuerzos + " spawned=" + arena.Spawned
                + " capturas=" + arena.Capturas + " termino=" + arena.Termino);
        }
        /// El cableado de la separacion en el Update real: dos perseguidores que nacen en el mismo
        /// punto tienen que abrirse al caminar hacia el objetivo, lejos de la arena para no medir
        /// enemigos ajenos.
        [UnityTest]
        public IEnumerator LosPerseguidoresNoSeApilanEnElMismoPunto()
        {
            GameFeel.Pausa = false;
            Time.timeScale = 1f;
            SlugArenaGame.CombateCongelado = false;

            SlugData data = SlugRoster.Buscar("electroshock");
            if (data == null && SlugRoster.Todos != null && SlugRoster.Todos.Length > 0)
                data = SlugRoster.Todos[0];

            // Las fases del zigzag salen de la semilla del run: sin fijarla, la distancia
            // final oscila alrededor del umbral y la prueba mide el azar de la partida anterior.
            EnemySlugAI.NuevaSemillaDeRun(12345);

            GameObject objetivo = new GameObject("ObjetivoSeparacion");
            objetivo.transform.position = new Vector3(100f, 0f, 120f);
            GameObject e1 = new GameObject("Perseguidor1");
            GameObject e2 = new GameObject("Perseguidor2");
            e1.transform.position = new Vector3(100f, 0f, 100f);
            e2.transform.position = new Vector3(100f, 0f, 100f);
            e1.AddComponent<SlugActor>().Configure(data, false);
            e2.AddComponent<SlugActor>().Configure(data, false);
            e1.AddComponent<EnemySlugAI>().SetTarget(objetivo.transform);
            e2.AddComponent<EnemySlugAI>().SetTarget(objetivo.transform);

            // Por TIEMPO, no por frames: en batch el editor corre a ~700 fps y 90 frames son
            // 0,12 s reales; con ese reloj los perseguidores apenas dieron dos pasos.
            float inicio = Time.time;
            for (int i = 0; i < 4000 && Time.time - inicio < 1.5f; i++) yield return null;

            float distancia = Vector3.Distance(e1.transform.position, e2.transform.position);
            Debug.Log("SEPARACION distancia=" + distancia.ToString("F3")
                + " z1=" + e1.transform.position.z.ToString("F2")
                + " z2=" + e2.transform.position.z.ToString("F2"));
            Assert.Greater(distancia, 0.8f, "los dos perseguidores siguen ocupando el mismo punto");
            Assert.Greater(e1.transform.position.z, 102f, "el perseguidor 1 no camino");
            Assert.Greater(e2.transform.position.z, 102f, "el perseguidor 2 no camino");

            Object.Destroy(e1);
            Object.Destroy(e2);
            Object.Destroy(objetivo);
            yield return null;
        }

        /// El cableado real de la mordida en el Update de la IA: el golpe no entra durante el
        /// aviso y entra al terminarlo. La prueba EditMode integra la maquina a mano; esta usa
        /// el camino que corre en el juego.
        [UnityTest]
        public IEnumerator LaMordidaNoPegaDuranteElAvisoYSiAlTerminarlo()
        {
            GameFeel.Pausa = false;
            Time.timeScale = 1f;

            SlugData data = SlugRoster.Buscar("electroshock");
            if (data == null && SlugRoster.Todos != null && SlugRoster.Todos.Length > 0)
                data = SlugRoster.Todos[0];

            Vector3 basePos = new Vector3(200f, 0f, 200f);
            GameObject objetivo = new GameObject("ObjetivoMordida");
            objetivo.transform.position = basePos;
            SlugActor victima = objetivo.AddComponent<SlugActor>();
            victima.Configure(data, true);

            GameObject enemigo = new GameObject("EnemigoMordida");
            enemigo.transform.position = basePos + new Vector3(0f, 0f, 1.2f);
            SlugActor actor = enemigo.AddComponent<SlugActor>();
            actor.Configure(data, false);
            EnemySlugAI ai = enemigo.AddComponent<EnemySlugAI>();
            ai.SetArquetipo(EnemySlugAI.Arquetipo.Frenetico);
            ai.SetTarget(objetivo.transform);
            yield return null;

            float inicio = Time.time;
            for (int i = 0; i < 4000 && !ai.Mordiendo && Time.time - inicio < 2f; i++) yield return null;
            Assert.IsTrue(ai.Mordiendo, "la IA no llego a empezar la mordida a rango de contacto");

            float hpAntes = victima.Hp;
            float aviso = Time.time;
            for (int i = 0; i < 4000 && Time.time - aviso < 0.12f; i++) yield return null;
            Assert.AreEqual(hpAntes, victima.Hp, 0.001f,
                "la mordida pego durante el aviso: el jugador no tiene ventana para salir");

            float cierre = Time.time;
            for (int i = 0; i < 4000 && Time.time - cierre < 0.5f; i++) yield return null;
            Assert.Less(victima.Hp, hpAntes, "la mordida no entro al terminar el aviso");
            Assert.GreaterOrEqual(ai.Mordidas, 1, "la mordida que llego no se conto");

            Object.Destroy(enemigo);
            Object.Destroy(objetivo);
            yield return null;
        }

        /// La rodada cancela la carga y el tiro acumulado no sale solo al soltar. Se arranca la
        /// carga con el metodo de prueba (estado alcanzable con ESPACIO) y se rueda de verdad.
        [UnityTest]
        public IEnumerator LaRodadaCancelaLaCargaSinDisparar()
        {
            GameFeel.Pausa = false;
            Time.timeScale = 1f;
            SlugArenaGame.CombateCongelado = false;

            SlugData data = SlugRoster.Buscar("electroshock");
            if (data == null && SlugRoster.Todos != null && SlugRoster.Todos.Length > 0)
                data = SlugRoster.Todos[0];

            GameObject jugador = new GameObject("JugadorCancel");
            jugador.transform.position = new Vector3(300f, 0f, 300f);
            SlugActor actor = jugador.AddComponent<SlugActor>();
            actor.Configure(data, true);
            PlayerShooter shooter = jugador.AddComponent<PlayerShooter>();
            shooter.SetData(data);
            PlayerDodge dodge = jugador.AddComponent<PlayerDodge>();
            yield return null;

            shooter.CargarDePrueba(0.6f);
            Assert.Greater(shooter.CargaProgreso, 0.9f, "la carga de prueba no quedo llena");
            Assert.IsTrue(dodge.IntentarRodada(), "la rodada no arranco");

            yield return null;
            yield return null;
            Assert.AreEqual(0f, shooter.CargaProgreso, 0.0001f,
                "la carga sobrevivio a la rodada");
            Assert.AreEqual(0, shooter.ShotsFired, "la rodada disparo el tiro acumulado");
            Assert.IsTrue(shooter.CargaBloqueada,
                "la carga no quedo bloqueada hasta soltar el boton");

            Object.Destroy(jugador);
            yield return null;
        }

        private static SlugActor Enemigo(string nombre, Vector3 posicion, SlugData data)
        {
            GameObject go = new GameObject(nombre);
            go.transform.position = posicion;
            SlugActor a = go.AddComponent<SlugActor>();
            a.Configure(data, false);
            return a;
        }

        /// El cableado real del auto-aim (lista de vivos + forward del transform), sin escena:
        /// un enemigo a la espalda no puede ganarle a uno que esta adelante aunque este mas cerca.
        [UnityTest]
        public IEnumerator ElAutoAimDelTiradorIgnoraLoQueEstaAtras()
        {
            SlugData data = SlugRoster.Buscar("electroshock");
            if (data == null && SlugRoster.Todos != null && SlugRoster.Todos.Length > 0)
                data = SlugRoster.Todos[0];

            GameObject jugador = new GameObject("JugadorAim");
            // Lejos del mapa: el auto-aim ahora respeta cobertura y en el origen puede
            // haber props de la escena entre el jugador y el enemigo.
            Vector3 basePos = new Vector3(300f, 0f, 300f);
            jugador.transform.position = basePos;
            jugador.transform.rotation = Quaternion.identity;   // forward = +Z
            SlugActor actor = jugador.AddComponent<SlugActor>();
            actor.Configure(data, true);
            PlayerShooter shooter = jugador.AddComponent<PlayerShooter>();
            shooter.SetData(data);

            SlugActor delante = Enemigo("Delante", basePos + new Vector3(0f, 0f, 6f), data);
            SlugActor atras = Enemigo("Atras", basePos + new Vector3(0f, 0f, -2f), data);   // mas cerca
            yield return null;   // deja correr OnEnable y el primer Update

            Assert.AreSame(delante, shooter.NearestEnemy(),
                "el tirador esta apuntando al enemigo que tiene a la espalda");

            Object.Destroy(delante.gameObject);
            yield return null;
            Assert.IsNull(shooter.NearestEnemy(),
                "sin nadie adelante el tiro tiene que salir derecho, no girar hacia atras");

            Object.Destroy(atras.gameObject);
            Object.Destroy(jugador);
            yield return null;
        }
    }
}
