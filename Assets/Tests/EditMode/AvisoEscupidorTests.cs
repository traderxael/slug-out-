using NUnit.Framework;
using SlugOut.Slugs;
using SlugOut.World;
using UnityEngine;

namespace SlugOut.EditModeTests
{
    /// El aviso del Escupidor tiene que existir fuera del tinte del cuerpo: es la unica
    /// pieza que le dice al jugador que busque cobertura antes del primer escupitajo.
    public class AvisoEscupidorTests
    {
        [TearDown]
        public void Limpiar()
        {
            GameHUD.ReiniciarAvisoEscupidor();
        }

        [Test]
        public void ElAvisoApareceConSuTextoYSePuedeReiniciar()
        {
            GameHUD.ReiniciarAvisoEscupidor();
            Assert.IsFalse(GameHUD.AvisoEscupidorVisible);
            GameHUD.MostrarAvisoEscupidor();
            Assert.IsTrue(GameHUD.AvisoEscupidorVisible,
                "el aviso no se encendio al empezar el aviso del Escupidor");
            Assert.AreEqual("ESCUPIDOR · CUBRITE O RODÁ", GameHUD.TextoAvisoEscupidor);
        }

        [Test]
        public void ElPrimerAvisoDelEscupidorEnciendeElCartel()
        {
            SlugData data = SlugRoster.Buscar("centella");
            if (data == null) data = SlugRoster.Todos[0];

            var jugador = new GameObject("JugadorAviso");
            var enemigo = new GameObject("EnemigoAviso");
            try
            {
                jugador.transform.position = Vector3.zero;
                jugador.AddComponent<SlugActor>().Configure(data, true);

                enemigo.transform.position = new Vector3(0f, 0f, 8f);
                enemigo.AddComponent<SlugActor>().Configure(data, false);
                EnemySlugAI ai = enemigo.AddComponent<EnemySlugAI>();
                ai.SetTarget(jugador.transform);
                ai.SetArquetipo(EnemySlugAI.Arquetipo.Escupidor);

                GameHUD.ReiniciarAvisoEscupidor();
                ai.PeleaEscupidor(0.05f);   // entra en AvisoDisparo

                Assert.IsTrue(ai.Disparando, "el Escupidor no arranco su aviso");
                Assert.IsTrue(GameHUD.AvisoEscupidorVisible,
                    "el aviso del Escupidor no encendio el cartel del HUD");
            }
            finally
            {
                Object.DestroyImmediate(enemigo);
                Object.DestroyImmediate(jugador);
            }
        }
    }
}
