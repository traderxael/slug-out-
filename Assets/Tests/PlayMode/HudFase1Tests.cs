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
    /// Fase 1 del HUD: el aviso de captura se dispara con el contador real y el
    /// resultado aparece una sola vez, congela el combate y permite abrir el equipo.
    public class HudFase1Tests
    {
        [UnityTest]
        public IEnumerator AvisoDeCapturaYResultadoUnico()
        {
            SceneManager.LoadScene("Mapa3D");
            yield return null; yield return null; yield return null;
            Assert.IsNotNull(GameFlow.Activo, "Mapa3D no tiene GameFlow");
            GameFlow.Activo.IniciarCampana(1);
            yield return null;
            // La arena del GameFlow persistente: FindFirst puede devolver la copia de la escena
            // que su Awake ya destruyo, y entonces el nivel arranca en un objeto fantasma.
            SlugArenaGame arena = GameFlow.Activo.GetComponent<SlugArenaGame>();
            Assert.IsNotNull(arena, "Mapa3D no tiene SlugArenaGame");
            arena.Iniciar(NivelConfig.Campana(1), SlugRoster.Buscar("electroshock"));
            for (int i = 0; i < 25; i++) yield return null;

            StoryPanel historia = UnityEngine.Object.FindAnyObjectByType<StoryPanel>();
            if (historia != null) historia.enabled = false;
            GameFeel.Pausa = false;
            Time.timeScale = 1f;
            EnemySlugAI[] ias = UnityEngine.Object.FindObjectsByType<EnemySlugAI>();
            for (int i = 0; i < ias.Length; i++) if (ias[i] != null) ias[i].enabled = false;

            GameHUD hud = UnityEngine.Object.FindAnyObjectByType<GameHUD>();
            PlayerController pc = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            SlugActor jugador = pc != null ? pc.GetComponent<SlugActor>() : null;
            Assert.IsNotNull(hud, "no hay GameHUD");
            Assert.IsNotNull(jugador, "no hay SlugActor del jugador");

            SlugActor presa = null;
            // En batch el reloj de frames no es el del juego: 25 frames pueden ser menos que
            // el intervalo de aparicion. Se espera al spawn real en vez de a un numero fijo.
            for (int i = 0; i < 300 && presa == null; i++)
            {
                var vivos = SlugActor.Vivos;
                for (int v = 0; v < vivos.Count; v++)
                {
                    SlugActor a = vivos[v];
                    if (a != null && !a.IsPlayer && a.Alive && !a.Capturada) { presa = a; break; }
                }
                if (presa == null) yield return null;
            }
            Assert.IsNotNull(presa, "no hay babosa viva para capturar"
                + " | spawned=" + arena.Spawned + " refuerzos=" + arena.Refuerzos
                + " vivos=" + SlugActor.Vivos.Count + " activa=" + arena.isActiveAndEnabled
                + " enabled=" + arena.enabled + " pausa=" + GameFeel.Pausa
                + " timeScale=" + Time.timeScale + " termino=" + arena.Termino);
            int antes = arena.Capturadas;
            presa.Capturar();
            for (int i = 0; i < 4; i++) yield return null;
            Assert.Greater(arena.Capturadas, antes, "la captura no incremento el contador");
            Assert.IsTrue(hud.ToastVisible, "el aviso de captura no aparecio");
            string especie = presa.Data != null ? presa.Data.slugName : "";
            if (!string.IsNullOrEmpty(especie))
                Assert.IsTrue(hud.ToastTexto.ToUpperInvariant().Contains(especie.ToUpperInvariant()),
                    "el aviso no nombra la especie capturada: " + hud.ToastTexto);
            Debug.Log("HUD_FASE1 captura antes=" + antes + " despues=" + arena.Capturadas
                + " toast=" + hud.ToastVisible + " texto=" + hud.ToastTexto);

            Assert.IsFalse(hud.ResultadoVisible, "el resultado aparecio antes de terminar");
            // Dejar vencer cualquier i-frame de un tiro previo antes del golpe de prueba.
            for (int i = 0; i < 40; i++) yield return null;
            float dano = jugador.TakeDamage(99999f, ElementType.Fire);
            Assert.Greater(dano, 0f, "el golpe mortal no entro");
            for (int i = 0; i < 12; i++) yield return null;
            Assert.IsTrue(arena.Termino, "la arena no marco el final");
            Assert.IsTrue(hud.ResultadoVisible, "el HUD no muestra el resultado");
            Assert.IsTrue(SlugArenaGame.CombateCongelado, "el combate no quedo congelado");

            CollectionUI.Abrir();
            Assert.IsTrue(CollectionUI.Abierto, "REVISAR EQUIPO no abrio la coleccion");
            CollectionUI.Cerrar();
            yield return null;
            Assert.IsFalse(CollectionUI.Abierto, "la coleccion no se cerro");
            Debug.Log("HUD_FASE1 resultado outcome=" + arena.outcome
                + " termino=" + arena.Termino + " equipo=true");
        }
    }
}
