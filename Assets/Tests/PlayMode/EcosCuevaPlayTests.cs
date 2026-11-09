using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SlugOut.Slugs;
using SlugOut.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SlugOut.PlayModeTests
{
    /// S1 (ecos de la cueva): el menu de 3 mejoras se abre DENTRO del nivel, pero jamas puede
    /// dejar la partida congelada. El arnes de campana (Tools/ArneltCampanaMedida.cs) mide con
    /// Time.deltaTime: si el menu pausa y nadie elige, su contador deja de avanzar y la corrida
    /// se queda colgada para siempre. Estos tests miden justamente ese cierre.
    public class EcosCuevaPlayTests
    {
        /// Errores que haya logueado el propio gestor de ecos (no el ruido de los paquetes).
        private readonly List<string> _erroresPropios = new List<string>();

        // Los paquetes externos (HFDownloader, com.unity.ai.assistant) loguean errores de red al
        // azar y tumban la prueba que les toque; estas pruebas miden el menu de ecos, no logs.
        // El biton vive en el ambito de log de la prueba: hay que ponerlo dentro del cuerpo
        // (un [UnitySetUp] solo cubre su propio ambito) y dejarlo puesto en el tear down.
        [UnitySetUp]
        public IEnumerator SilenciarRuidoExterno()
        {
            _erroresPropios.Clear();
            Application.logMessageReceived += CapturarLog;
            LogAssert.ignoreFailingMessages = true;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator RestaurarLogs()
        {
            Application.logMessageReceived -= CapturarLog;
            LogAssert.ignoreFailingMessages = true;
            yield return null;
        }

        /// ignoreFailingMessages se traga hasta los errores del codigo propio: estos se cazan
        /// a mano para que un fallo del gestor no se esconda debajo del ruido de red.
        private void CapturarLog(string condicion, string stackTrace, LogType tipo)
        {
            if (tipo != LogType.Error && tipo != LogType.Exception && tipo != LogType.Assert) return;
            if (stackTrace == null || stackTrace.IndexOf("EcosCuevaManager") < 0) return;
            _erroresPropios.Add(condicion + " | " + stackTrace.Substring(0, Mathf.Min(160, stackTrace.Length)));
        }

        private void AssertSinErroresDelGestor()
        {
            if (_erroresPropios.Count == 0) return;
            Assert.Fail("el gestor de ecos logueo errores: " + string.Join(" || ", _erroresPropios.ToArray()));
        }

        [UnityTest]
        public IEnumerator ElMenuDeEcosSeCierraSoloSiNadieElige()
        {
            LogAssert.ignoreFailingMessages = true;
            SceneManager.LoadScene("Mapa3D");
            yield return null; yield return null; yield return null;
            Assert.IsNotNull(GameFlow.Activo, "Mapa3D no tiene GameFlow");
            GameFlow.Activo.IniciarCampana(1);
            yield return null;
            SlugArenaGame arena = GameFlow.Activo.GetComponent<SlugArenaGame>();
            Assert.IsNotNull(arena, "el GameFlow no tiene SlugArenaGame");
            arena.Iniciar(NivelConfig.Campana(1), SlugRoster.Buscar("electroshock"));
            for (int i = 0; i < 10; i++) yield return null;

            // La voz de la historia pausa la partida por su cuenta: el menu de ecos no debe
            // pelearse con ese pausa (por eso el gestor no ofrece con el juego ya pausado).
            StoryPanel historia = Object.FindAnyObjectByType<StoryPanel>();
            if (historia != null) historia.enabled = false;
            GameFeel.Pausa = false;
            Time.timeScale = 1f;

            EcosCuevaManager eco = EcosCuevaManager.Instancia;
            Assert.IsNotNull(eco, "no hay EcosCuevaManager vivo");
            // Margen corto: el test no necesita esperar los 5 s de juego.
            eco.MargenAutoEleccion = 1.5f;
            eco.NotificarCaptura();
            for (int i = 0; i < 60 && !eco.Activo; i++) yield return null;
            Assert.IsTrue(eco.Activo, "la captura no abrio el menu de ecos");
            Assert.IsTrue(GameFeel.Pausa, "el menu de ecos no pauso la partida");

            // Nadie elige (arnes, pruebas, jugador distraido): el menu tiene que cerrarse solo
            // en tiempo real y devolver el control. Si no, la partida queda congelada.
            // El id del primer eco, BEFORE de esperar: al cerrarse el menu, OfertaActual es null.
            Assert.GreaterOrEqual(eco.OfertaActual.Length, 3, "la oferta no trae 3 ecos");
            string idAuto = eco.OfertaActual[0].def.id;
            float t0 = Time.unscaledTime;
            while (eco.Activo && Time.unscaledTime - t0 < 20f) yield return null;
            float espera = Time.unscaledTime - t0;
            Assert.IsFalse(eco.Activo, "el menu de ecos quedo abierto: la partida se congelaria");
            Assert.IsFalse(GameFeel.Pausa, "la pausa del menu de ecos quedo puesta");
            // Cerrarse no basta: el auto-elegido tiene que aplicar el bono del primer eco
            // (si no, el menu se cierra y el jugador se queda sin mejora).
            Assert.AreEqual(1, eco.NivelDeEco(idAuto),
                "el auto-elegido no aplico el primer eco de la oferta");
            // Y tiene que hacerlo pronto: con el margen en 1,5 s, 8 s ya es un margen enorme.
            Assert.Less(espera, 8f, "el menu tardo " + espera.ToString("F1") + " s en cerrarse solo");
            yield return null;
            Assert.Greater(Time.timeScale, 0f, "el tiempo del juego no se reanudo");
            AssertSinErroresDelGestor();
            Debug.Log("ECOS menu cerrado solo tras " + espera.ToString("F1") + " s reales");
        }

        [UnityTest]
        public IEnumerator ElegirUnEcoAplicaElBonoYReanuda()
        {
            LogAssert.ignoreFailingMessages = true;
            SceneManager.LoadScene("Mapa3D");
            yield return null; yield return null; yield return null;
            Assert.IsNotNull(GameFlow.Activo, "Mapa3D no tiene GameFlow");
            GameFlow.Activo.IniciarCampana(1);
            yield return null;
            SlugArenaGame arena = GameFlow.Activo.GetComponent<SlugArenaGame>();
            Assert.IsNotNull(arena, "el GameFlow no tiene SlugArenaGame");
            arena.Iniciar(NivelConfig.Campana(1), SlugRoster.Buscar("electroshock"));
            for (int i = 0; i < 10; i++) yield return null;

            StoryPanel historia = Object.FindAnyObjectByType<StoryPanel>();
            if (historia != null) historia.enabled = false;
            GameFeel.Pausa = false;
            Time.timeScale = 1f;

            EcosCuevaManager eco = EcosCuevaManager.Instancia;
            Assert.IsNotNull(eco, "no hay EcosCuevaManager vivo");
            PlayerShooter shooter = Object.FindAnyObjectByType<PlayerShooter>();
            Assert.IsNotNull(shooter, "no hay PlayerShooter");
            int tubosAntes = shooter.TubosCapacidad;

            eco.NotificarCaptura();
            for (int i = 0; i < 60 && !eco.Activo; i++) yield return null;
            Assert.IsTrue(eco.Activo, "la captura no abrio el menu de ecos");

            // Un eco de cargador: su bono se ve en el arma del jugador.
            var defPrueba = new EcoDef
            {
                id = "eco_prueba_cargador",
                nombre = "Tubo extra",
                descripcion = "prueba",
                tipo = EcoTipo.Cargador,
                valor = 1f,
            };
            eco.Elegir(defPrueba);
            Assert.IsFalse(eco.Activo, "el menu sigue abierto tras elegir");
            Assert.IsFalse(GameFeel.Pausa, "la pausa quedo puesta tras elegir");
            Assert.AreEqual(tubosAntes + 1, shooter.TubosCapacidad,
                "el bono del eco no llego al cargador");
            AssertSinErroresDelGestor();
        }

        [UnityTest]
        public IEnumerator ElegirPorIndiceCogeElEcoDeEsaFila()
        {
            LogAssert.ignoreFailingMessages = true;
            SceneManager.LoadScene("Mapa3D");
            yield return null; yield return null; yield return null;
            Assert.IsNotNull(GameFlow.Activo, "Mapa3D no tiene GameFlow");
            GameFlow.Activo.IniciarCampana(1);
            yield return null;
            SlugArenaGame arena = GameFlow.Activo.GetComponent<SlugArenaGame>();
            Assert.IsNotNull(arena, "el GameFlow no tiene SlugArenaGame");
            arena.Iniciar(NivelConfig.Campana(1), SlugRoster.Buscar("electroshock"));
            for (int i = 0; i < 10; i++) yield return null;

            StoryPanel historia = Object.FindAnyObjectByType<StoryPanel>();
            if (historia != null) historia.enabled = false;
            GameFeel.Pausa = false;
            Time.timeScale = 1f;

            EcosCuevaManager eco = EcosCuevaManager.Instancia;
            Assert.IsNotNull(eco, "no hay EcosCuevaManager vivo");
            eco.NotificarCaptura();
            for (int i = 0; i < 60 && !eco.Activo; i++) yield return null;
            Assert.IsTrue(eco.Activo, "la captura no abrio el menu de ecos");
            Assert.GreaterOrEqual(eco.OfertaActual.Length, 2, "la oferta no trae 3 ecos");

            // El atajo 2 elige el SEGUNDO eco de la lista (lo que pulsa la tecla 2).
            int fila;
            Assert.IsTrue(EcosCuevaManager.FilaDeTecla(KeyCode.Alpha2, out fila), "la tecla 2 no es atajo");
            Assert.AreEqual(1, fila, "la tecla 2 no apunta a la segunda fila");
            int filaMuerta;
            Assert.IsFalse(EcosCuevaManager.FilaDeTecla(KeyCode.Alpha9, out filaMuerta), "la tecla 9 deberia ignorarse");
            string idEsperado = eco.OfertaActual[fila].def.id;
            Assert.AreEqual(0, eco.NivelDeEco(idEsperado), "el eco ya venia cogido");
            eco.ElegirPorIndice(fila);
            Assert.AreEqual(1, eco.NivelDeEco(idEsperado),
                "el atajo no cogio el eco de su fila");
            Assert.IsFalse(eco.Activo, "el menu sigue abierto tras elegir por indice");
            Assert.IsFalse(GameFeel.Pausa, "la pausa quedo puesta tras elegir por indice");
            AssertSinErroresDelGestor();
        }

        [UnityTest]
        public IEnumerator UnNivelNuevoNoHeredaLaPausaDelMenuAnterior()
        {
            LogAssert.ignoreFailingMessages = true;
            SceneManager.LoadScene("Mapa3D");
            yield return null; yield return null; yield return null;
            Assert.IsNotNull(GameFlow.Activo, "Mapa3D no tiene GameFlow");
            GameFlow.Activo.IniciarCampana(1);
            yield return null;
            SlugArenaGame arena = GameFlow.Activo.GetComponent<SlugArenaGame>();
            Assert.IsNotNull(arena, "el GameFlow no tiene SlugArenaGame");
            arena.Iniciar(NivelConfig.Campana(1), SlugRoster.Buscar("electroshock"));
            for (int i = 0; i < 10; i++) yield return null;

            StoryPanel historia = Object.FindAnyObjectByType<StoryPanel>();
            if (historia != null) historia.enabled = false;

            EcosCuevaManager eco = EcosCuevaManager.Instancia;
            Assert.IsNotNull(eco, "no hay EcosCuevaManager vivo");

            // El menu anterior quedo abierto y la partida congelada.
            GameFeel.Pausa = true;
            eco.ReiniciarNivel();
            Assert.IsFalse(GameFeel.Pausa,
                "el nivel nuevo arranco con la pausa del menu anterior: el combate nace congelado");
            AssertSinErroresDelGestor();
        }
    }
}
