using System.Collections;
using NUnit.Framework;
using SlugOut.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SlugOut.PlayModeTests
{
    /// El tunel tiene que poder nombrar su destino (lo usa el minimapa y el aviso de
    /// proximidad) y dejar al jugador apoyado en la region nueva, no dentro de la boca.
    public class TunelAvisoTests
    {
        [UnityTest]
        public IEnumerator ElTunelNombraSuDestinoYColocaAlJugadorEnElPiso()
        {
            SceneManager.LoadScene("Mapa3D");
            yield return null; yield return null; yield return null;
            if (GameFlow.Activo != null) GameFlow.Activo.IniciarCampana(1);
            yield return null;

            RegionCuevaSeca region = Object.FindAnyObjectByType<RegionCuevaSeca>();
            PlayerController pc = Object.FindAnyObjectByType<PlayerController>();
            Assert.IsNotNull(region);
            Assert.IsNotNull(pc);
            TunelPasadizo tp = null;
            if (region.TunelesRaiz != null)
                for (int i = 0; i < region.TunelesRaiz.Length; i++)
                {
                    if (region.TunelesRaiz[i] == null) continue;
                    tp = region.TunelesRaiz[i].GetComponentInChildren<TunelPasadizo>();
                    if (tp != null) break;
                }
            Assert.IsNotNull(tp, "la region no tiene pasadizo");
            Assert.AreEqual(Regiones.De(tp.destino).nombre, tp.NombreDestino,
                "el cartel/aviso del tunel nombra otra cueva");

            int destino = tp.destino;
            yield return new WaitForSecondsRealtime(2f);
            Assert.IsTrue(tp.Viajar(pc.transform), "el viaje fue rechazado");
            yield return null; yield return null;
            Assert.AreEqual(destino, RegionCuevaSeca.Actual, "el tunel no cambio de region");
            float suelo = Terrain3D.Altura(pc.transform.position.x, pc.transform.position.z);
            Assert.That(pc.transform.position.y, Is.InRange(suelo + 0.25f, suelo + 2.5f),
                "el jugador quedo dentro del piso o flotando al salir del tunel");
            region.Reconstruir(0, out _);
        }
    }
}
