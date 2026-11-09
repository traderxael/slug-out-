using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SlugOut.Core;
using SlugOut.Slugs;
using SlugOut.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SlugOut.PlayModeTests
{
    /// Diagnostico del patinaje del pie. Mueve al actor con la misma ley de velocidad
    /// que PlayerController (pos += forward * v * dt + ApoyarEnPiso) y mide, cuadro a
    /// cuadro, cuanto se desplaza horizontalmente el pie de apoyo respecto de la raiz.
    /// El hueso del pie es el tobillo: es un proxy, no la planta. Escribe el resultado
    /// en Logs/patinaje.txt para comparar antes/despues.
    public class PatinajeTests
    {
        private class Montaje
        {
            public PlayerController pc;
            public DuelistaAnimator anim;
            public Transform pieIzq;
            public Transform pieDer;
            public Transform modelo;
        }

        [UnityTest, Explicit("Diagnostico de patinaje: no corre en la suite normal.")]
        public IEnumerator MidePatinaje()
        {
            Montaje m = new Montaje();
            yield return Preparar(m);

            yield return Medir(m, 0.8f, "paso-cuidado");
            yield return Medir(m, 3f, "enemigo");
            yield return Medir(m, 6f, "carrera");
            Time.captureDeltaTime = 0f;

            Debug.Log("PATINAJE_RUTA=" + Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "Logs", "patinaje.txt")));
        }

        private static IEnumerator Preparar(Montaje m)
        {
            SceneManager.LoadScene("Mapa3D");
            yield return null; yield return null; yield return null;
            Assert.IsNotNull(GameFlow.Activo, "Mapa3D no tiene GameFlow");
            GameFlow.Activo.IniciarCampana(1);
            yield return null;
            SlugArenaGame arena = UnityEngine.Object.FindAnyObjectByType<SlugArenaGame>();
            Assert.IsNotNull(arena, "Mapa3D no tiene SlugArenaGame");
            arena.Iniciar(NivelConfig.Campana(1), SlugRoster.Buscar("electroshock"));
            for (int i = 0; i < 20; i++) yield return null;

            m.pc = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            Assert.IsNotNull(m.pc, "no hay PlayerController");
            m.anim = m.pc.GetComponent<DuelistaAnimator>();
            Assert.IsNotNull(m.anim, "el jugador no tiene DuelistaAnimator");
            Transform hero = m.pc.transform.Find("Cuerpo_Shane");
            if (hero == null) hero = GameObject.Find("Cuerpo_Shane")?.transform;
            Assert.IsNotNull(hero, "no se monto Cuerpo_Shane");
            m.modelo = hero;
            foreach (Transform t in hero.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "pie_izq") m.pieIzq = t;
                else if (t.name == "pie_der") m.pieDer = t;
            }
            Assert.IsNotNull(m.pieIzq, "el rig no tiene pie_izq");
            Assert.IsNotNull(m.pieDer, "el rig no tiene pie_der");

            StoryPanel historia = UnityEngine.Object.FindAnyObjectByType<StoryPanel>();
            if (historia != null) historia.enabled = false;
            GameFeel.Pausa = false;
            Time.timeScale = 1f;
            // El runner de batch avanza con deltaTime minusculo; con captura fija el
            // desplazamiento y la fase del animador avanzan con el mismo reloj.
            Time.captureDeltaTime = 1f / 60f;
            m.pc.transform.rotation = Quaternion.identity;
            m.pc.GetComponent<PlayerController>().enabled = false;
            SlugAnimator raizAnim = m.pc.GetComponent<SlugAnimator>();
            if (raizAnim != null) raizAnim.enabled = false;

            // Luces de estudio para la evidencia: el heroe se mide a 10 m de altura,
            // fuera del alcance de las luces de la cueva.
            GameObject keyGo = new GameObject("luz_key_patinaje");
            Light key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.86f, 0.72f);
            key.intensity = 1.2f;
            key.shadows = LightShadows.None;
            key.transform.rotation = Quaternion.Euler(35f, -25f, 0f);
            GameObject fillGo = new GameObject("luz_fill_patinaje");
            Light fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.75f, 0.82f, 1f);
            fill.intensity = 0.55f;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(15f, 150f, 0f);
            yield return null;
        }

        private static IEnumerator Medir(Montaje m, float velocidad, string etiqueta)
        {
            // Altura fija: el tobillo local mide la animacion, no los altibajos del
            // terreno. El desplazamiento horizontal sigue siendo el del juego.
            Vector3 inicio = new Vector3(0f, 10f, 0f);
            m.pc.transform.position = inicio;
            m.pc.transform.rotation = Quaternion.identity;
            for (int i = 0; i < 45; i++) yield return null; // que el suavizado de velocidad arranque

            Vector3 rootPrev = m.pc.transform.position;
            Vector3 izqPrev = m.pieIzq.position;
            Vector3 derPrev = m.pieDer.position;
            int muestras = 0;
            float slip = 0f;
            float recorrido = 0f;
            float minIzq = float.MaxValue, maxIzq = float.MinValue;
            float minDer = float.MaxValue, maxDer = float.MinValue;

            for (int i = 0; i < 240; i++)
            {
                // Mismo dt que el animador: con un paso fijo el ciclo avanzaria a otra
                // velocidad que el desplazamiento y el patinaje medido seria artificial.
                float dtFrame = Time.deltaTime;
                if (dtFrame > 0.0001f)
                    m.pc.transform.position += Vector3.forward * (velocidad * dtFrame);
                yield return null;

                Vector3 root = m.pc.transform.position;
                Vector3 izq = m.pieIzq.position;
                Vector3 der = m.pieDer.position;
                if (i > 45)
                {
                    Vector3 dRoot = root - rootPrev; dRoot.y = 0f;
                    Vector3 dIzq = izq - izqPrev; dIzq.y = 0f;
                    Vector3 dDer = der - derPrev; dDer.y = 0f;
                    // El apoyo es el pie que menos se mueve en el mundo. El pie mas
                    // bajo no sirve: en el vuelo tambien puede quedar bajo.
                    slip += Mathf.Min(dIzq.magnitude, dDer.magnitude);
                    recorrido += dRoot.magnitude;
                    muestras++;
                }
                if (i % 60 == 0)
                    Debug.Log("PATINAJE_DEBUG " + etiqueta + " i=" + i
                        + " actor=" + root.ToString("F2")
                        + " izq=" + izq.ToString("F2")
                        + " der=" + der.ToString("F2")
                        + " relIzq=" + (izq - root).ToString("F2"));
                if (i == 120) Capturar(m, etiqueta);
                rootPrev = root;
                izqPrev = izq;
                derPrev = der;
                minIzq = Mathf.Min(minIzq, izq.y); maxIzq = Mathf.Max(maxIzq, izq.y);
                minDer = Mathf.Min(minDer, der.y); maxDer = Mathf.Max(maxDer, der.y);
            }

            float relativo = recorrido > 0.001f ? slip / recorrido : 0f;
            float liftIzq = maxIzq - minIzq;
            float liftDer = maxDer - minDer;
            string linea = etiqueta
                + " v=" + velocidad.ToString("F1")
                + " muestras=" + muestras
                + " slipRel=" + relativo.ToString("F4")
                + " liftIzq=" + liftIzq.ToString("F3")
                + " liftDer=" + liftDer.ToString("F3")
                + " recorrido=" + recorrido.ToString("F2");
            Debug.Log("PATINAJE " + linea);
            string ruta = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "patinaje.txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(ruta));
            File.AppendAllText(ruta, linea + "\n");

            Assert.Greater(muestras, 30, "no hubo suficientes muestras de apoyo a " + velocidad + " m/s");
            Assert.Greater(liftIzq + liftDer, 0.02f, "los pies no se levantan: la animacion no corre");
        }

        private static void Capturar(Montaje m, string nombre)
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "anim-heroe", "u3"));
            Directory.CreateDirectory(dir);
            GameObject go = new GameObject("cam_patinaje");
            Camera cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.015f, 0.025f, 0.05f, 1f);
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            cam.enabled = false;
            RenderTexture rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            Vector3 foco = m.modelo.position + Vector3.up * 0.9f;
            cam.transform.position = foco + m.pc.transform.right * 3.5f + Vector3.up * 0.3f;
            cam.transform.LookAt(foco);
            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, nombre + ".png"), tex.EncodeToPNG());
            RenderTexture.active = null;
            UnityEngine.Object.Destroy(tex);
            UnityEngine.Object.Destroy(rt);
            UnityEngine.Object.Destroy(go);
        }
    }
}
