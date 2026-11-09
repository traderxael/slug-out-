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
    /// Los caminos son cintas que siguen el relieve y la plaza es una laja enterrada.
    /// Si la piedra sobresale del terreno, el personaje -que apoya en Terrain3D.Altura-
    /// camina hundido en ella. Se comprueba vertice a vertice.
    public class MapaCaminableTests
    {
        [UnityTest]
        public IEnumerator LosCaminosSiguenElTerreno()
        {
            SceneManager.LoadScene("Mapa3D");
            yield return null; yield return null; yield return null;
            Assert.IsNotNull(GameFlow.Activo, "Mapa3D no tiene GameFlow");
            GameFlow.Activo.IniciarCampana(1);
            yield return null;
            SlugArenaGame arena = UnityEngine.Object.FindAnyObjectByType<SlugArenaGame>();
            Assert.IsNotNull(arena, "Mapa3D no tiene SlugArenaGame");
            arena.Iniciar(NivelConfig.Campana(1), SlugRoster.Buscar("electroshock"));
            for (int i = 0; i < 30; i++) yield return null;

            int cintas = 0, vertices = 0;
            float peor = float.MinValue;
            string peorNombre = "";
            GameObject[] todos = UnityEngine.Object.FindObjectsByType<GameObject>();
            for (int i = 0; i < todos.Length; i++)
            {
                GameObject go = todos[i];
                if (go == null) continue;
                string n = go.name;
                if (n.StartsWith("cinta"))
                {
                    MeshFilter mf = go.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    cintas++;
                    Vector3[] vs = mf.sharedMesh.vertices;
                    for (int v = 0; v < vs.Length; v++)
                    {
                        Vector3 w = go.transform.TransformPoint(vs[v]);
                        float sobre = w.y - Terrain3D.Altura(w.x, w.z);
                        vertices++;
                        if (sobre > peor) { peor = sobre; peorNombre = n; }
                    }
                }
                else if (n == "disco" || n.StartsWith("borde_plaza"))
                {
                    Renderer r = go.GetComponent<Renderer>();
                    if (r == null) continue;
                    cintas++;
                    float sobre = r.bounds.max.y - Terrain3D.Altura(r.bounds.center.x, r.bounds.center.z);
                    if (sobre > peor) { peor = sobre; peorNombre = n; }
                }
            }
            Assert.Greater(cintas, 5, "no se encontraron los caminos de la region");
            Debug.Log("CAMINOS cintas=" + cintas + " vertices=" + vertices
                + " peorSobre=" + peor.ToString("F3") + " (" + peorNombre + ")");
            Assert.LessOrEqual(peor, 0.06f,
                "la piedra sobresale " + peor.ToString("F3") + " m del terreno: " + peorNombre);
        }
    }
}
