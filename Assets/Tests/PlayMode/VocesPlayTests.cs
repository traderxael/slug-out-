using System.Collections;
using NUnit.Framework;
using SlugOut.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SlugOut.PlayModeTests
{
    /// Prueba de integracion real: el nivel 1 arranca con la voz de Eli, la tarjeta se estira
    /// a la duracion del clip y el combate queda congelado hasta que se salta. Sin voces, la
    /// tarjeta vuelve a 7 s y no pausa; la frase de Blakk carga su clip.
    public class VocesPlayTests
    {
        private bool _vocesAntes;

        [SetUp] public void GuardarPreferencia()
        {
            _vocesAntes = GameFlow.VocesActivas;
        }

        [TearDown] public void Restaurar()
        {
            if (VozPlayer.Instancia != null) VozPlayer.Instancia.Stop();
            if (GameFlow.Activo != null && GameFlow.VocesActivas != _vocesAntes)
                GameFlow.Activo.AlternarVoces();
        }

        [UnityTest]
        public IEnumerator ElNivel1ArrancaConVozYPausaNarrativa()
        {
            SceneManager.LoadScene("Mapa3D");
            yield return null;
            yield return null;
            yield return null;

            Assert.IsNotNull(GameFlow.Activo, "Mapa3D no tiene GameFlow");
            if (!GameFlow.VocesActivas) GameFlow.Activo.AlternarVoces();

            GameFlow.Activo.IniciarCampana(1);
            StoryPanel panel = Object.FindAnyObjectByType<StoryPanel>();
            VozPlayer voz = VozPlayer.Instancia != null ? VozPlayer.Instancia
                : Object.FindAnyObjectByType<VozPlayer>();
            Assert.IsNotNull(panel, "no hay StoryPanel");
            Assert.IsNotNull(voz, "no hay VozPlayer");

            // La tarjeta se encola en Iniciar; el Update del panel arranca la voz y la pausa.
            yield return null;
            yield return null;

            Assert.IsTrue(StoryPanel.FragmentoVisible, "la tarjeta del nivel 1 no se mostro");
            Assert.IsTrue(panel.PausaNarrativaActiva, "la narracion no congelo el combate");
            Assert.AreEqual("Voces/campana/1", voz.RutaActual, "no cargo el clip del nivel 1");
            Assert.Greater(voz.DuracionActual, 0.5f, "el clip del nivel 1 no tiene duracion");
            Assert.Greater(panel.DuracionFragmentoActual, 7f, "la tarjeta no se estiro a la voz");

            panel.SaltarFragmento();
            yield return null;
            Assert.IsFalse(panel.PausaNarrativaActiva, "saltar no solto la pausa narrativa");
            Assert.IsFalse(GameFeel.Pausa, "el combate quedo congelado despues de saltar");
            Assert.IsNull(voz.RutaActual, "la voz no se detuvo al saltar");

            // Sin voces: tarjeta normal, sin pausa y sin clip.
            if (GameFlow.VocesActivas) GameFlow.Activo.AlternarVoces();
            GameFlow.Activo.IniciarCampana(2);
            yield return null;
            yield return null;
            Assert.IsTrue(StoryPanel.FragmentoVisible, "la tarjeta del nivel 2 no se mostro");
            Assert.IsFalse(panel.PausaNarrativaActiva, "sin voces no debe pausar el combate");
            Assert.IsNull(voz.RutaActual, "sin voces no debe cargar clip");

            // Frase de duelista: clip propio y duracion para el cartel. Hay que reactivar
            // las voces: el bloque anterior las apago a proposito.
            if (!GameFlow.VocesActivas) GameFlow.Activo.AlternarVoces();
            float duracion = voz.PlayDuelista("dr-blakk", 3);
            Assert.Greater(duracion, 0.5f, "la frase de Blakk no cargo");
            Assert.AreEqual("Voces/duelistas/dr-blakk/3", voz.RutaActual);

            voz.Stop();
            GameFlow.Activo.VolverAlMenu();
        }

        /// Regression: ArenaGame (arena + StoryPanel + GameFlow) es un solo GameObject
        /// persistente, asi que el progreso de fragmentos sobrevive a volver al menu. Sin
        /// ReiniciarCampana() el jugador que empieza una aventura nueva no vuelve a ver ni
        /// el fragmento 1, y la prueba de arriba solo pasa por el orden de ejecucion.
        [UnityTest]
        public IEnumerator EmpezarDeCeroVuelveAMostrarElFragmento1()
        {
            SceneManager.LoadScene("Mapa3D");
            yield return null;
            yield return null;

            GameFlow.Activo.IniciarCampana(1);
            yield return null;
            yield return null;
            Assert.IsTrue(StoryPanel.FragmentoVisible, "primera partida: no mostro el fragmento 1");

            StoryPanel panel = Object.FindAnyObjectByType<StoryPanel>();
            panel.SaltarFragmento();
            yield return null;
            Assert.IsFalse(StoryPanel.FragmentoVisible, "saltar no cerro la tarjeta");

            GameFlow.Activo.VolverAlMenu();
            GameFlow.Activo.IniciarCampana(1);
            yield return null;
            yield return null;
            Assert.IsTrue(StoryPanel.FragmentoVisible,
                "reinicio de campana: el fragmento 1 no volvio a mostrarse");
            Assert.LessOrEqual(panel.VistosCampana, 1, "no olvido los fragmentos de la partida anterior");

            panel.SaltarFragmento();
            GameFlow.Activo.VolverAlMenu();
        }
    }
}
