using System.Collections;
using NUnit.Framework;
using SlugOut.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SlugOut.PlayModeTests
{
    /// La prueba que faltaba: el mesh que se ve en Mapa3D tiene que estar en las mismas
    /// coordenadas de mundo que Altura, la que usan jugador, props, camara y enemigos.
    public class TerrenoVisualTests
    {
        [UnityTest]
        public IEnumerator ElPisoVisibleCoincideConAlturaYNoTieneCollider()
        {
            SceneManager.LoadScene("Mapa3D");
            yield return null; yield return null; yield return null;

            Terrain3D t = Object.FindAnyObjectByType<Terrain3D>();
            Assert.IsNotNull(t, "Mapa3D no tiene Terrain3D");
            RegionCuevaSeca region = Object.FindAnyObjectByType<RegionCuevaSeca>();
            Assert.IsNotNull(region, "Mapa3D no tiene RegionCuevaSeca");

            Assert.That(t.transform.localScale, Is.EqualTo(Vector3.one),
                "el terreno tiene que estar en metros de mundo, no escalado 5x");
            Assert.That(region.Root.localScale, Is.EqualTo(Vector3.one),
                "el root de la region no debe compensar una escala que ya no existe");
            Assert.IsNull(t.GetComponent<MeshCollider>(),
                "no hay raycasts contra el terreno: el MeshCollider solo cuesta cooking");

            Mesh mesh = t.GetComponent<MeshFilter>().sharedMesh;
            Assert.IsNotNull(mesh);
            Assert.AreEqual(Terrain3D.Bandas, mesh.subMeshCount);
            Assert.GreaterOrEqual(-mesh.bounds.min.x, 26f,
                "el mesh tiene que cubrir las paredes en 24.6");
            Vector3[] vs = mesh.vertices;
            for (int i = 0; i < vs.Length; i += 41)
            {
                Vector3 w = t.transform.TransformPoint(vs[i]);
                Assert.That(w.y, Is.EqualTo(Terrain3D.Altura(w.x, w.z)).Within(0.001f),
                    "vertice " + i + " en " + w.ToString("F2") + " no coincide con Altura");
            }
        }
    }
}
