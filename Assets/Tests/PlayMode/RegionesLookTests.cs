using System.Collections;
using NUnit.Framework;
using SlugOut.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SlugOut.PlayModeTests
{
    /// El look global pisaba niebla, ambiente, luz y decoracion de las cuatro cuevas. Esta
    /// prueba cruza las regiones y exige que cada una aplique su grupo y su paleta, sin
    /// duplicar el root ni crear luces point extra.
    public class RegionesLookTests
    {
        [UnityTest]
        public IEnumerator LasCuatroCuevasAplicanSuGrupoYNoDuplicanElLook()
        {
            SceneManager.LoadScene("Mapa3D");
            yield return null; yield return null; yield return null;
            if (GameFlow.Activo != null) GameFlow.Activo.IniciarCampana(1);
            yield return null; yield return null;

            RegionCuevaSeca region = Object.FindAnyObjectByType<RegionCuevaSeca>();
            LookCuevaPixar look = Object.FindAnyObjectByType<LookCuevaPixar>();
            PlayerController pc = Object.FindAnyObjectByType<PlayerController>();
            Assert.IsNotNull(region, "Mapa3D no tiene RegionCuevaSeca");
            Assert.IsNotNull(look, "Mapa3D no tiene LookCuevaPixar");
            Assert.IsNotNull(pc, "Mapa3D no tiene PlayerController");
            Assert.IsTrue(look.Construido, "el look no termino de construirse");

            string[][] activos =
            {
                new[] { "Agua", "Follaje", "Cristales", "Hongos", "Luces" },
                new[] { "Eco", "Cristales", "Luces" },
                new[] { "Agua", "Follaje", "Vena", "Cristales", "Hongos", "Luces" },
                new[] { "Canon" }
            };
            try
            {
                for (int r = 0; r < Regiones.Todas.Length; r++)
                {
                    region.Reconstruir(r, out _);
                    yield return null; yield return null;
                    RegionDefinicion d = Regiones.De(r);
                    Assert.AreEqual(r, RegionCuevaSeca.Actual);
                    Assert.AreEqual(r, look.RegionAplicada,
                        d.nombre + ": Look no recibio la region nueva");
                    foreach (string grupo in new[] { "Shell", "Agua", "Follaje", "Cristales",
                        "Hongos", "Luces", "Eco", "Vena", "Canon" })
                    {
                        bool esperado = grupo == "Shell" || System.Array.IndexOf(activos[r], grupo) >= 0;
                        Assert.AreEqual(esperado, look.GrupoActivo(grupo),
                            d.nombre + ": grupo " + grupo + " con estado incorrecto");
                    }

                    AssertColor(d.niebla, RenderSettings.fogColor, d.nombre + ": fog");
                    AssertColor(d.ambiente, RenderSettings.ambientLight, d.nombre + ": ambient");
                    Camera cam = Camera.main;
                    Assert.IsNotNull(cam);
                    AssertColor(d.fondoCamara, cam.backgroundColor, d.nombre + ": fondo de camara");

                    int puntos = 0, direccionales = 0;
                    Light[] luces = region.Root.GetComponentsInChildren<Light>(true);
                    foreach (Light l in luces)
                    {
                        if (l == null) continue;
                        if (l.type == LightType.Point) puntos++;
                        else if (l.type == LightType.Directional) direccionales++;
                    }
                    Assert.LessOrEqual(puntos, 6,
                        d.nombre + ": demasiadas luces point para la GT 640");
                    Assert.LessOrEqual(direccionales, 1,
                        d.nombre + ": la region debe traer una sola direccional");
                    GameObject fillGo = GameObject.Find("Luz_Cueva_Fill");
                    Assert.IsNotNull(fillGo, "falta el fill de Look");
                    Assert.AreEqual(1, CountCuevaPixarRoots(), d.nombre
                        + ": hay mas de un CuevaPixar o quedo decoracion vieja");

                    int renderers = 0, triangulos = 0;
                    Terrain3D terreno = Object.FindAnyObjectByType<Terrain3D>();
                    if (terreno != null) Contar(terreno.gameObject, ref renderers, ref triangulos, false);
                    Contar(region.Root.gameObject, ref renderers, ref triangulos, true);
                    GameObject cueva = GameObject.Find("CuevaPixar");
                    if (cueva != null) Contar(cueva, ref renderers, ref triangulos, true);
                    Debug.Log("LOOK_REGION " + d.nombre + " grupos=" + string.Join("/", activos[r])
                        + " renderers=" + renderers + " tris=" + triangulos
                        + " point=" + puntos + " dir=" + direccionales + "+fill");
                    // La Vena Verde mide 195 con sus tres habitats de agua/raices; el techo
                    // queda por encima del valor real medido, no al reves.
                    Assert.LessOrEqual(renderers, 210,
                        d.nombre + ": demasiados renderers activos");
                    Assert.LessOrEqual(triangulos, 120000,
                        d.nombre + ": demasiados triangulos activos");
                }

                // Un viaje real: la region 0 debe salir por la boca que apunta a la 1.
                region.Reconstruir(0, out _);
                yield return null;
                yield return new WaitForSecondsRealtime(2f);
                TunelPasadizo salida = null;
                if (region.TunelesRaiz != null)
                    for (int i = 0; i < region.TunelesRaiz.Length; i++)
                    {
                        if (region.TunelesRaiz[i] == null) continue;
                        if (region.DestinosTuneles[i] != 1) continue;
                        salida = region.TunelesRaiz[i].GetComponentInChildren<TunelPasadizo>();
                        break;
                    }
                Assert.IsNotNull(salida, "no hay boca de Cueva Seca hacia Galerias");
                Assert.AreEqual("GALERIAS DEL ECO", salida.NombreDestino);
                Assert.IsTrue(salida.Viajar(pc.transform), "el viaje por el pasadizo fue rechazado");
                yield return null; yield return null;
                Assert.AreEqual(1, RegionCuevaSeca.Actual, "el tunel no cambio de region");
                Assert.AreEqual(1, look.RegionAplicada, "Look no siguio el viaje");
                Assert.IsTrue(look.GrupoActivo("Eco"), "la firma Eco no quedo activa");
                Assert.That(pc.transform.position.y,
                    Is.EqualTo(Terrain3D.Altura(pc.transform.position.x,
                        pc.transform.position.z) + 1f).Within(0.75f),
                    "el jugador no salio apoyado en el piso de la region nueva");
            }
            finally
            {
                region.Reconstruir(0, out _);
            }
        }

        private static void AssertColor(Color esperado, Color real, string mensaje)
        {
            Assert.That(Mathf.Abs(esperado.r - real.r), Is.LessThan(0.01f), mensaje + " r");
            Assert.That(Mathf.Abs(esperado.g - real.g), Is.LessThan(0.01f), mensaje + " g");
            Assert.That(Mathf.Abs(esperado.b - real.b), Is.LessThan(0.01f), mensaje + " b");
        }

        private static int CountCuevaPixarRoots()
        {
            int roots = 0;
            GameObject[] todos = Object.FindObjectsByType<GameObject>();
            for (int i = 0; i < todos.Length; i++)
                if (todos[i] != null && todos[i].name == "CuevaPixar") roots++;
            return roots;
        }

        private static void Contar(GameObject raiz, ref int renderers, ref int triangulos,
                                   bool incluirHijos)
        {
            Renderer[] rs = incluirHijos
                ? raiz.GetComponentsInChildren<Renderer>(true)
                : raiz.GetComponents<Renderer>();
            for (int i = 0; i < rs.Length; i++)
            {
                Renderer r = rs[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                renderers++;
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                    triangulos += mf.sharedMesh.triangles.Length / 3;
            }
        }
    }
}
