using System.Collections;
using NUnit.Framework;
using SlugOut.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SlugOut.PlayModeTests
{
    /// La cuarta cueva tiene que ser parte de la campaña, no una habitación opcional:
    /// el arranque de un nivel reconstruye la región de su arco.
    public class CampanaRegionesPlayTests
    {
        [UnityTest]
        public IEnumerator ElNivelVeintidosArrancaEnElCanonYElNivelUnoVuelveALaCuevaSeca()
        {
            SceneManager.LoadScene("Mapa3D");
            yield return null; yield return null; yield return null;
            Assert.IsNotNull(GameFlow.Activo, "Mapa3D no tiene GameFlow");

            GameFlow.Activo.IniciarCampana(22);
            yield return null;
            Assert.AreEqual(3, RegionCuevaSeca.Actual, "el nivel 22 tiene que jugarse en el Cañón");
            Assert.AreEqual("EL CAÑON DE CENIZA", RegionCuevaSeca.NombreRegionActual);
            RegionCuevaSeca region = Object.FindAnyObjectByType<RegionCuevaSeca>();
            Assert.IsNotNull(region, "no hay RegionCuevaSeca en Mapa3D");
            Assert.AreEqual(3, region.Microhabitats, "el Cañón tiene que armar sus tres zonas");

            GameFlow.Activo.IniciarCampana(1);
            yield return null;
            Assert.AreEqual(0, RegionCuevaSeca.Actual, "NUEVA AVENTURA tiene que volver a la primera cueva");
            Assert.AreEqual("LA CUEVA SECA", RegionCuevaSeca.NombreRegionActual);
        }
    }
}
