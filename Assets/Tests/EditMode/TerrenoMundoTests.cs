using NUnit.Framework;
using SlugOut.World;
using UnityEngine;

namespace SlugOut.EditModeTests
{
    /// El piso visible y el que pisan jugador/props tienen que ser la misma funcion. La
    /// escena traia `Terreno` escalado 5x y el mesh se construia en local: en mundo (20,0)
    /// se veia Altura(4,0) y se pisaba Altura(20,0). Estas pruebas fijan la conversion.
    public class TerrenoMundoTests
    {
        [Test] public void ReconstruirNormalizaLaEscalaYElMeshCoincideConAltura()
        {
            Vector2 desfaseAntes = Terrain3D.DesfaseRegion;
            float amplitudAntes = Terrain3D.AmplitudRegion;
            var go = new GameObject("TerrenoPrueba");
            try
            {
                Terrain3D.DesfaseRegion = Vector2.zero;
                Terrain3D.AmplitudRegion = 1f;
                var t = go.AddComponent<Terrain3D>();
                go.transform.localScale = new Vector3(5f, 1f, 5f);
                t.Reconstruir();

                Assert.That(go.transform.localScale, Is.EqualTo(Vector3.one),
                    "el mesh se construye en mundo: la escala del Terreno tiene que ser 1");
                MeshFilter mf = go.GetComponent<MeshFilter>();
                MeshRenderer mr = go.GetComponent<MeshRenderer>();
                Assert.IsNotNull(mf, "sin MeshFilter no hay piso visible");
                Assert.IsNotNull(mr, "sin MeshRenderer no hay piso visible");
                Assert.IsNull(go.GetComponent<MeshCollider>(),
                    "el terreno no necesita MeshCollider: el gameplay apoya en Altura");

                Mesh mesh = mf.sharedMesh;
                Assert.IsNotNull(mesh);
                Assert.AreEqual(Terrain3D.Bandas, mesh.subMeshCount,
                    "el color por banda depende de tres submeshes");
                Assert.GreaterOrEqual(mesh.bounds.min.x, -t.MedioLado - 0.01f);
                Assert.LessOrEqual(mesh.bounds.max.x, t.MedioLado + 0.01f);
                Assert.GreaterOrEqual(-mesh.bounds.min.x, 26f,
                    "el mesh tiene que cubrir las paredes en 24.6");
                Assert.LessOrEqual(mesh.bounds.size.x, 80f,
                    "un mesh gigante fuera de las paredes solo gasta vertices");

                Vector3[] vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i += 37)
                {
                    Vector3 w = go.transform.TransformPoint(vertices[i]);
                    float esperado = Terrain3D.Altura(w.x, w.z);
                    Assert.That(w.y, Is.EqualTo(esperado).Within(0.001f),
                        "vertice " + i + " en " + w.ToString("F2")
                        + " no coincide con Altura en mundo");
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
                Terrain3D.DesfaseRegion = desfaseAntes;
                Terrain3D.AmplitudRegion = amplitudAntes;
            }
        }

        [Test] public void AplicarPaletaConservaTexturaYColores()
        {
            var go = new GameObject("TerrenoPaleta");
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                var t = go.AddComponent<Terrain3D>();
                t.Reconstruir();
                t.AplicarPaleta(new Color(0.1f, 0.2f, 0.3f),
                    new Color(0.4f, 0.5f, 0.6f), new Color(0.7f, 0.8f, 0.9f), tex, 8f);

                MeshRenderer mr = go.GetComponent<MeshRenderer>();
                Assert.IsNotNull(mr);
                Assert.AreEqual(3, mr.sharedMaterials.Length);
                AssertColor(new Color(0.1f, 0.2f, 0.3f), mr.sharedMaterials[0].color);
                AssertColor(new Color(0.4f, 0.5f, 0.6f), mr.sharedMaterials[1].color);
                AssertColor(new Color(0.7f, 0.8f, 0.9f), mr.sharedMaterials[2].color);
                if (mr.sharedMaterials[0].HasProperty("_BaseMap"))
                    Assert.IsNotNull(mr.sharedMaterials[0].GetTexture("_BaseMap"),
                        "GameplayLookTests exige _BaseMap en el terreno");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(tex);
            }
        }

        private static void AssertColor(Color esperado, Color real)
        {
            Assert.That(Mathf.Abs(esperado.r - real.r), Is.LessThan(0.001f));
            Assert.That(Mathf.Abs(esperado.g - real.g), Is.LessThan(0.001f));
            Assert.That(Mathf.Abs(esperado.b - real.b), Is.LessThan(0.001f));
        }
    }
}
