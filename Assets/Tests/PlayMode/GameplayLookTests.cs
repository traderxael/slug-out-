using System.Collections;
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
    public class GameplayLookTests
    {
        [UnityTest]
        public IEnumerator CapturaMapa3D()
        {
            SceneManager.LoadScene("Mapa3D");
            yield return null;
            yield return null;
            yield return null;

            Assert.IsNotNull(GameFlow.Activo, "Mapa3D no tiene GameFlow");
            GameFlow.Activo.IniciarCampana(1);
            yield return null;
            SlugArenaGame arena = Object.FindAnyObjectByType<SlugArenaGame>();
            Assert.IsNotNull(arena, "Mapa3D no tiene SlugArenaGame");
            arena.Iniciar(NivelConfig.Campana(1), SlugRoster.Buscar("electroshock"));
            for (int i = 0; i < 12; i++) yield return null;

            PlayerController pc = Object.FindAnyObjectByType<PlayerController>();
            Camera cam = Camera.main;
            Assert.IsNotNull(pc, "Mapa3D no tiene PlayerController");
            Assert.IsNotNull(cam, "Mapa3D no tiene camara principal");
            Vector3 posInicio = pc.transform.position;
            Quaternion rotInicio = pc.transform.rotation;

            Transform hero = pc.transform.Find("Cuerpo_Shane");
            if (hero == null) hero = GameObject.Find("Cuerpo_Shane")?.transform;
            GameObject comp = GameObject.Find("Companero");
            Assert.IsNotNull(hero, "el heroe nuevo no quedo montado en el jugador");
            Assert.IsNotNull(comp, "no se monto el companero babosa");

            Renderer[] cuerpoRenderers = pc.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < cuerpoRenderers.Length; i++)
            {
                Renderer rr = cuerpoRenderers[i];
                string mats = "";
                Material[] ms = rr.sharedMaterials;
                for (int j = 0; j < ms.Length; j++)
                {
                    Material m = ms[j];
                    Texture texBase = m != null && m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                    mats += (m != null ? m.name : "NULL") + "(tex=" + (texBase != null ? texBase.name : "NULL") + ") ";
                }
                Debug.Log("RENDER_HEROE " + rr.name + " enabled=" + rr.enabled
                    + " parent=" + (rr.transform.parent != null ? rr.transform.parent.name : "-")
                    + " bounds=" + rr.bounds.size + " mats=" + mats);

                SkinnedMeshRenderer smr = rr as SkinnedMeshRenderer;
                if (smr != null && smr.sharedMesh != null)
                {
                    Vector2[] uvs = smr.sharedMesh.uv;
                    float minU = 1f, maxU = 0f, minV = 1f, maxV = 0f;
                    int fuera = 0;
                    for (int u = 0; u < uvs.Length; u++)
                    {
                        minU = Mathf.Min(minU, uvs[u].x); maxU = Mathf.Max(maxU, uvs[u].x);
                        minV = Mathf.Min(minV, uvs[u].y); maxV = Mathf.Max(maxV, uvs[u].y);
                        if (uvs[u].x < -0.01f || uvs[u].x > 1.01f || uvs[u].y < -0.01f || uvs[u].y > 1.01f)
                            fuera++;
                    }
                    Debug.Log("UV_HEROE n=" + uvs.Length + " min=(" + minU.ToString("F2") + ","
                        + minV.ToString("F2") + ") max=(" + maxU.ToString("F2") + "," + maxV.ToString("F2")
                        + ") fuera=" + fuera + " sub=" + smr.sharedMesh.subMeshCount);
                    Assert.Greater(uvs.Length, 0,
                        "el FBX del heroe se importo sin UVs: la textura no puede muestrear y el cuerpo sale blanco");
                    bool textura = false;
                    Material[] matsHeroe = rr.sharedMaterials;
                    for (int k = 0; k < matsHeroe.Length; k++)
                        if (matsHeroe[k] != null && matsHeroe[k].HasProperty("_BaseMap")
                            && matsHeroe[k].GetTexture("_BaseMap") != null) textura = true;
                    Assert.IsTrue(textura, "ningun submesh del heroe tiene _BaseMap");
                }
            }

            Vector3 toCam = cam.transform.position - pc.transform.position;
            Assert.Greater(toCam.magnitude, 2f, "la camara no esta en tercera persona");
            Assert.Less(Vector3.Dot(toCam.normalized, pc.transform.forward), 0.35f,
                "la camara deberia estar detras del jugador");
            Vector3 toPlayer = pc.transform.position - cam.transform.position;
            Assert.Greater(Vector3.Dot(cam.transform.forward, toPlayer.normalized), 0.7f,
                "la camara no mira al jugador");

            Vector3 vpHero = cam.WorldToViewportPoint(hero.position + Vector3.up * 1f);
            Assert.Greater(vpHero.z, 0f, "el heroe quedo detras de la camara");
            Assert.That(vpHero.x, Is.InRange(-0.1f, 1.1f), "el heroe quedo fuera de cuadro (x)");
            Assert.That(vpHero.y, Is.InRange(-0.1f, 1.1f), "el heroe quedo fuera de cuadro (y)");

            Vector3 vpSlug = cam.WorldToViewportPoint(comp.transform.position + Vector3.up * 0.3f);
            Assert.Greater(vpSlug.z, 0f, "el companero quedo detras de la camara");
            Assert.That(vpSlug.x, Is.InRange(-0.3f, 1.3f), "el companero quedo fuera de cuadro");


            Terrain3D terreno = Object.FindAnyObjectByType<Terrain3D>();
            Assert.IsNotNull(terreno, "no hay Terrain3D");
            MeshRenderer mrT = terreno.GetComponent<MeshRenderer>();
            MeshFilter mfT = terreno.GetComponent<MeshFilter>();
            Assert.IsNotNull(mrT, "el terreno no tiene MeshRenderer");
            Assert.IsNotNull(mfT, "el terreno no tiene MeshFilter");
            Mesh meshT = mfT.sharedMesh;
            Material matT = mrT.sharedMaterials.Length > 0 ? mrT.sharedMaterials[0] : null;
            Texture texT = matT != null && matT.HasProperty("_BaseMap") ? matT.GetTexture("_BaseMap") : null;
            Vector2[] uvT = meshT != null ? meshT.uv : new Vector2[0];
            float tMinU = 1f, tMaxU = 0f, tMinV = 1f, tMaxV = 0f;
            for (int i = 0; i < uvT.Length; i++)
            {
                tMinU = Mathf.Min(tMinU, uvT[i].x); tMaxU = Mathf.Max(tMaxU, uvT[i].x);
                tMinV = Mathf.Min(tMinV, uvT[i].y); tMaxV = Mathf.Max(tMaxV, uvT[i].y);
            }
            Debug.Log("TERRENO uv=" + uvT.Length
                + " rango=(" + tMinU.ToString("F2") + "," + tMinV.ToString("F2") + ")-("
                + tMaxU.ToString("F2") + "," + tMaxV.ToString("F2") + ")"
                + " mats=" + mrT.sharedMaterials.Length
                + " tex=" + (texT != null ? texT.name : "NULL")
                + " shader=" + (matT != null ? matT.shader.name : "-")
                + " color=" + (matT != null ? matT.color.ToString() : "-")
                + " tiling=" + (matT != null && matT.HasProperty("_BaseMap") ? matT.GetTextureScale("_BaseMap").ToString() : "-"));
            Assert.Greater(meshT != null ? meshT.uv.Length : 0, 0,
                "el mesh del terreno no tiene UV: la piedra no puede repetirse");
            Assert.IsNotNull(texT, "el material del terreno no tiene _BaseMap");

            float t0 = Time.realtimeSinceStartup;
            int f0 = Time.frameCount;
            for (int i = 0; i < 120; i++) yield return null;
            float fps = (Time.frameCount - f0) / Mathf.Max(0.0001f, Time.realtimeSinceStartup - t0);
            Debug.Log("FPS_MAPA3D=" + fps.ToString("F1") + " (proxy PlayMode, no build)");

            const int w = 1280;
            const int h = 720;
            // Foto reproducible: apagar la IA y volver al punto de partida para que el
            // encuadre no dependa de donde empujaron los enemigos durante el bench.
            EnemySlugAI[] ias = Object.FindObjectsByType<EnemySlugAI>();
            for (int i = 0; i < ias.Length; i++)
                if (ias[i] != null) ias[i].gameObject.SetActive(false);
            pc.transform.position = posInicio;
            pc.transform.rotation = rotInicio;
            for (int i = 0; i < 30; i++) yield return null;
            // La foto es fija: apagar los animadores restaura el material del flash de golpe.
            // Si el heroe recibio un golpe justo antes, la captura salia blanca.
            SlugAnimator[] animadores = Object.FindObjectsByType<SlugAnimator>();
            for (int i = 0; i < animadores.Length; i++) animadores[i].enabled = false;
            yield return null;
            RenderTexture rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "arte-pixar"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "mapa3d_tercera.png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Debug.Log("CAPTURA_MAPA3D=" + path);
            Object.Destroy(tex);
            Object.Destroy(rt);
        }
    }
}
