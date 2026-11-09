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
    /// El balance A tactico llega al combate montado, no solo a los factores sueltos.
    public class BalanceTacticoPlayTests
    {
        [UnityTest]
        public IEnumerator ElCombateArrancaConVida120YDanoEnemigo7()
        {
            SceneManager.LoadScene("Mapa3D");
            yield return null; yield return null; yield return null;
            Assert.IsNotNull(GameFlow.Activo, "Mapa3D no tiene GameFlow");
            GameFlow.Activo.IniciarCampana(1);
            yield return null;
            SlugArenaGame arena = Object.FindAnyObjectByType<SlugArenaGame>();
            Assert.IsNotNull(arena, "Mapa3D no tiene SlugArenaGame");
            arena.Iniciar(NivelConfig.Campana(1), SlugRoster.Buscar("electroshock"));
            yield return null; yield return null;

            PlayerController pc = Object.FindAnyObjectByType<PlayerController>();
            Assert.IsNotNull(pc, "no hay PlayerController");
            SlugActor heroe = pc.GetComponent<SlugActor>();
            Assert.IsNotNull(heroe, "el heroe no tiene SlugActor");
            Assert.AreEqual(SlugActor.VidaJugadorTactica, heroe.MaxHp, 0.001f);
            Assert.AreEqual(120f, heroe.MaxHp, 0.001f, "la vida del jugador no es 120");

            System.Reflection.FieldInfo campo = typeof(SlugArenaGame).GetField(
                "enemyDamage",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(campo, "cambio el campo enemyDamage de SlugArenaGame");
            float dano = (float)campo.GetValue(arena);
            Assert.AreEqual(3.5f, dano, 0.001f,
                "el nivel 1 deberia pegar 4 * 7/8 = 3.5; el resto de la curva sigue igual");
            Debug.Log("BALANCE_TACTICO hpJugador=" + heroe.MaxHp.ToString("F1")
                + " danoNivel1=" + dano.ToString("F2")
                + " factorArma=" + PlayerShooter.AjusteDanoTactico(35f).ToString("F2"));
        }
    }
}
