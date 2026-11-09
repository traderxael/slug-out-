using NUnit.Framework;
using SlugOut.Slugs;
using SlugOut.World;
using UnityEngine;

namespace SlugOut.EditModeTests
{
    /// Balance A tactico aprobado: vida del jugador 120, dano base 32 (tabla de 35) y
    /// dano enemigo 7 (curva sobre 8). Velocidad, cupos y tubos no cambian.
    public class BalanceTacticoTests
    {
        [Test] public void ElJugadorTieneVidaFijaYElEnemigoConservaLaFormula()
        {
            SlugData electro = SlugRoster.Buscar("electroshock");
            Assert.IsNotNull(electro, "falta el slug electroshock");

            GameObject go = new GameObject("actor_balance");
            try
            {
                SlugActor actor = go.AddComponent<SlugActor>();
                actor.Configure(electro, true);
                Assert.AreEqual(120f, actor.MaxHp, 0.001f, "la vida del jugador no es 120");

                actor.Configure(electro, false);
                Assert.AreEqual(80f + electro.clashPower * 0.6f, actor.MaxHp, 0.001f,
                    "la vida enemiga dejo de salir de la tabla del slug");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test] public void ElDanoBasePasaDe35A32SinCambiarLaProporcion()
        {
            Assert.AreEqual(32f, PlayerShooter.AjusteDanoTactico(35f), 0.0001f);
            Assert.AreEqual(32f, PlayerShooter.AjusteDanoTactico(PlayerShooter.DanoBaseReferencia),
                0.0001f);
            Assert.AreEqual(64f, PlayerShooter.AjusteDanoTactico(70f), 0.0001f);
        }

        [Test] public void ElDanoEnemigoPasaDe8A7YLaIaArrancaEn7()
        {
            Assert.AreEqual(7f, SlugArenaGame.AjusteDanoEnemigoTactico(8f), 0.0001f);
            Assert.AreEqual(14f, SlugArenaGame.AjusteDanoEnemigoTactico(16f), 0.0001f);

            GameObject go = new GameObject("ai_balance");
            try
            {
                EnemySlugAI ai = go.AddComponent<EnemySlugAI>();
                System.Reflection.FieldInfo campo = typeof(EnemySlugAI).GetField(
                    "attackDamage",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.IsNotNull(campo, "cambio el campo attackDamage de EnemySlugAI");
                Assert.AreEqual(7f, (float)campo.GetValue(ai), 0.001f,
                    "el dano por defecto de la IA no es 7");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
