using System;
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
    /// Herramienta de calibracion, no una prueba de regresion. Primero alinea el canon
    /// moviendo la muneca derecha; despues busca un apoyo de mano izquierda medido sobre
    /// el FBX: palma cerca del guardamanos, lejos de la cabeza y codo por debajo del
    /// hombro. Escribe el resultado en Logs/aim-calibracion.txt para pegarlo en
    /// DuelistaAnimator. Los ejes del modelo no son los de Blender; no se adivinan.
    public class AimPoseCalibracionTests
    {
        [UnityTest, Explicit("Herramienta de calibracion: no corre en la suite normal.")]
        public IEnumerator BuscaApoyoDeManoIzquierda()
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

            PlayerController pc = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            Assert.IsNotNull(pc, "no hay PlayerController");
            DuelistaAnimator anim = pc.GetComponent<DuelistaAnimator>();
            Assert.IsNotNull(anim, "el jugador no tiene DuelistaAnimator");
            Transform hero = pc.transform.Find("Cuerpo_Shane");
            if (hero == null) hero = GameObject.Find("Cuerpo_Shane")?.transform;
            Assert.IsNotNull(hero, "no se monto Cuerpo_Shane");
            SkinnedMeshRenderer smr = hero.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.IsNotNull(smr, "el heroe no tiene SkinnedMeshRenderer");

            Transform brazoIzq = null, antebrazoIzq = null, manoIzq = null, manoDer = null, cabeza = null;
            foreach (Transform t in hero.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "brazo_izq") brazoIzq = t;
                else if (t.name == "antebrazo_izq") antebrazoIzq = t;
                else if (t.name == "mano_izq") manoIzq = t;
                else if (t.name == "mano_der") manoDer = t;
                else if (t.name == "cabeza") cabeza = t;
            }
            Assert.IsNotNull(brazoIzq, "falta brazo_izq");
            Assert.IsNotNull(antebrazoIzq, "falta antebrazo_izq");
            Assert.IsNotNull(manoIzq, "falta mano_izq");
            Assert.IsNotNull(manoDer, "falta mano_der");
            Assert.IsNotNull(cabeza, "falta cabeza");

            int blaster = -1;
            for (int i = 0; i < smr.sharedMaterials.Length; i++)
                if (smr.sharedMaterials[i] != null && smr.sharedMaterials[i].name.ToLowerInvariant().Contains("blaster"))
                    blaster = i;
            Assert.GreaterOrEqual(blaster, 0, "no hay submesh de blaster");

            // Referencia estable: el actor mira a +Z y no lo mueve nadie durante la
            // calibracion. Sin esto, el forward cambia entre corridas y el angulo medido
            // deja de ser comparable.
            pc.transform.rotation = Quaternion.identity;
            PlayerController control = pc.GetComponent<PlayerController>();
            if (control != null) control.enabled = false;
            SlugAnimator raizAnim = pc.GetComponent<SlugAnimator>();
            if (raizAnim != null) raizAnim.enabled = false;

            // Pose de produccion: la referencia contra la que se mide todo.
            anim.ProbeApuntar();
            yield return null;

            Mesh horneada = new Mesh();
            Vector3 fwdPlano = Vector3.ProjectOnPlane(pc.transform.forward, Vector3.up).normalized;
            Vector3 ag0, bo0, pa0;
            Medir(smr, horneada, blaster, manoDer, manoIzq, out ag0, out bo0, out pa0);
            LogHuesosBlaster(smr, horneada, blaster);
            Vector3 dirLocal = Quaternion.Inverse(manoDer.rotation) * (bo0 - ag0).normalized;
            Vector3 palmaLocal = manoIzq.InverseTransformPoint(pa0);

            // 1) Muneca derecha: alinea el canon en 3D con el frente horizontal. El
            // blaster esta 100 % pesado a mano_der, asi que la direccion se predice
            // con la rotacion del hueso y el bake final la confirma.
            Vector3 mejorMd = new Vector3(135f, 30f, -15f);
            float mejorAng = float.MaxValue;
            for (int x = 80; x <= 200; x += 5)
            for (int y = -60; y <= 100; y += 5)
            for (int z = -80; z <= 50; z += 5)
            {
                Vector3 md = new Vector3(x, y, z);
                anim.ProbeApuntarDerecha(md);
                Vector3 dir = manoDer.rotation * dirLocal;
                float ang = Vector3.Angle(dir, fwdPlano);
                if (ang < mejorAng) { mejorAng = ang; mejorMd = md; }
            }
            for (int iter = 0; iter < 3; iter++)
            {
                Vector3 centro = mejorMd;
                for (int dx = -4; dx <= 4; dx++)
                for (int dy = -4; dy <= 4; dy++)
                for (int dz = -4; dz <= 4; dz++)
                {
                    Vector3 md = centro + new Vector3(dx, dy, dz);
                    anim.ProbeApuntarDerecha(md);
                    Vector3 dir = manoDer.rotation * dirLocal;
                    float ang = Vector3.Angle(dir, fwdPlano);
                    if (ang < mejorAng) { mejorAng = ang; mejorMd = md; }
                }
            }

            anim.ProbeApuntarDerecha(mejorMd);
            yield return null;
            Vector3 ag, bo, pa;
            Medir(smr, horneada, blaster, manoDer, manoIzq, out ag, out bo, out pa);
            Vector3 guardamanos = Vector3.Lerp(ag, bo, 0.30f);
            float anguloActual = Vector3.Angle((bo - ag).normalized, fwdPlano);

            // 2) Brazo izquierdo: apoyo medido sobre el guardamanos nuevo.
            Vector3 cabezaCentro = cabeza.position + Vector3.up * 0.12f;
            Vector3 hombro = brazoIzq.position;
            Func<Vector3, Vector3, Vector3, float> evaluar = (bi, ai, mi) =>
            {
                anim.ProbeApuntarCompleta(mejorMd, bi, ai, mi);
                Vector3 palma = manoIzq.TransformPoint(palmaLocal);
                float costo = Vector3.Distance(palma, guardamanos);
                float aCabeza = Vector3.Distance(palma, cabezaCentro);
                if (aCabeza < 0.32f) costo += 10f + (0.32f - aCabeza) * 50f;
                if (antebrazoIzq.position.y > hombro.y + 0.05f)
                    costo += 1f + (antebrazoIzq.position.y - hombro.y) * 10f;
                return costo;
            };

            var rnd = new System.Random(20261022);
            Func<float, float, float> rango = (a, b) => a + (float)rnd.NextDouble() * (b - a);
            Vector3 mejorBi = Vector3.zero, mejorAi = new Vector3(-45f, 0f, 0f), mejorMi = Vector3.zero;
            float mejor = evaluar(mejorBi, mejorAi, mejorMi);

            for (int i = 0; i < 30000; i++)
            {
                Vector3 bi = new Vector3(rango(-120f, 50f), rango(-60f, 60f), rango(-60f, 60f));
                Vector3 ai = new Vector3(rango(-130f, 10f), rango(-20f, 20f), rango(-45f, 45f));
                Vector3 mi = new Vector3(rango(-60f, 60f), rango(-40f, 40f), rango(-60f, 60f));
                float c = evaluar(bi, ai, mi);
                if (c < mejor) { mejor = c; mejorBi = bi; mejorAi = ai; mejorMi = mi; }
            }
            for (int iter = 0; iter < 4; iter++)
            {
                float r = 20f / (iter + 1);
                for (int i = 0; i < 4000; i++)
                {
                    Vector3 bi = mejorBi + new Vector3(rango(-r, r), rango(-r, r), rango(-r, r));
                    Vector3 ai = mejorAi + new Vector3(rango(-r, r), rango(-r, r), rango(-r, r));
                    Vector3 mi = mejorMi + new Vector3(rango(-r, r), rango(-r, r), rango(-r, r));
                    float c = evaluar(bi, ai, mi);
                    if (c < mejor) { mejor = c; mejorBi = bi; mejorAi = ai; mejorMi = mi; }
                }
            }

            anim.ProbeApuntarCompleta(mejorMd, mejorBi, mejorAi, mejorMi);
            yield return null;
            Vector3 agF, boF, paF;
            Medir(smr, horneada, blaster, manoDer, manoIzq, out agF, out boF, out paF);
            Vector3 guardamanosF = Vector3.Lerp(agF, boF, 0.30f);
            float anguloFinal = Vector3.Angle((boF - agF).normalized, fwdPlano);
            float distanciaFinal = Vector3.Distance(paF, guardamanosF);
            float cabezaFinal = Vector3.Distance(paF, cabezaCentro);
            float bajoCabeza = cabezaCentro.y - paF.y;

            string reporte =
                "AIM_CALIB mano_der=" + Fmt(mejorMd) + "\n" +
                "AIM_CALIB brazo_izq=" + Fmt(mejorBi) + "\n" +
                "AIM_CALIB antebrazo_izq=" + Fmt(mejorAi) + "\n" +
                "AIM_CALIB mano_izq=" + Fmt(mejorMi) + "\n" +
                "AIM_CALIB costo=" + mejor.ToString("F4") + "\n" +
                "AIM_CALIB angulo_muneca=" + anguloActual.ToString("F2") + "\n" +
                "AIM_CALIB angulo_final=" + anguloFinal.ToString("F2") + "\n" +
                "AIM_CALIB palma_guardamanos=" + distanciaFinal.ToString("F4") + "\n" +
                "AIM_CALIB palma_cabeza=" + cabezaFinal.ToString("F4") + "\n" +
                "AIM_CALIB bajo_cabeza=" + bajoCabeza.ToString("F4") + "\n" +
                "AIM_CALIB guardamanos=" + guardamanosF.ToString("F3") + "\n" +
                "AIM_CALIB palma=" + paF.ToString("F3") + "\n" +
                "AIM_CALIB agarre=" + agF.ToString("F3") + "\n" +
                "AIM_CALIB boca=" + boF.ToString("F3") + "\n" +
                "AIM_CALIB hombro=" + hombro.ToString("F3") + "\n";
            string ruta = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "aim-calibracion.txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(ruta));
            File.WriteAllText(ruta, reporte);
            Debug.Log(reporte.Replace("\n", " | "));
            Debug.Log("AIM_CALIB_RUTA=" + ruta);

            string dirFotos = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "anim-heroe", "calib"));
            Directory.CreateDirectory(dirFotos);
            Capturar(pc, dirFotos, "apuntar_frente_calib.png", pc.transform.forward, 3.4f, 1.4f);
            Capturar(pc, dirFotos, "apuntar_lado_calib.png", pc.transform.right, 3.4f, 1.4f);
            Debug.Log("AIM_CALIB_FOTOS=" + dirFotos);

            UnityEngine.Object.Destroy(horneada);
            Assert.LessOrEqual(anguloFinal, 5f, "el canon sigue fuera del frente: " + reporte);
            Assert.LessOrEqual(distanciaFinal, 0.12f, "el brazo izquierdo no alcanza el guardamanos: " + reporte);
            Assert.GreaterOrEqual(cabezaFinal, 0.30f, "la mano izquierda quedo junto a la cabeza: " + reporte);
            Assert.GreaterOrEqual(bajoCabeza, 0.15f, "la mano izquierda quedo a la altura de la cabeza: " + reporte);
        }

        private static void Medir(SkinnedMeshRenderer smr, Mesh horneada, int blaster,
            Transform manoDer, Transform manoIzq,
            out Vector3 agarre, out Vector3 boca, out Vector3 palma)
        {
            smr.BakeMesh(horneada, true);
            Matrix4x4 aMundo = smr.transform.localToWorldMatrix;
            int[] tris = horneada.GetTriangles(blaster);
            var vistos = new HashSet<int>();
            var puntos = new List<Vector3>();
            for (int i = 0; i < tris.Length; i++)
            {
                if (!vistos.Add(tris[i])) continue;
                puntos.Add(aMundo.MultiplyPoint3x4(horneada.vertices[tris[i]]));
            }
            Assert.Greater(puntos.Count, 10, "el submesh del blaster no tiene vertices");

            agarre = puntos[0];
            boca = puntos[0];
            float dMin = float.MaxValue, dMax = -1f;
            for (int i = 0; i < puntos.Count; i++)
            {
                float d = (puntos[i] - manoDer.position).sqrMagnitude;
                if (d < dMin) { dMin = d; agarre = puntos[i]; }
            }
            for (int i = 0; i < puntos.Count; i++)
            {
                float d = (puntos[i] - agarre).sqrMagnitude;
                if (d > dMax) { dMax = d; boca = puntos[i]; }
            }

            Mesh malla = smr.sharedMesh;
            BoneWeight[] pesos = malla.boneWeights;
            Transform[] huesos = smr.bones;
            Vector3 suma = Vector3.zero;
            int cuenta = 0;
            for (int v = 0; v < malla.vertexCount && v < horneada.vertexCount; v++)
            {
                int idx = Dominante(pesos[v]);
                if (idx < 0 || idx >= huesos.Length || huesos[idx] == null) continue;
                if (huesos[idx].name != "mano_izq") continue;
                suma += aMundo.MultiplyPoint3x4(horneada.vertices[v]);
                cuenta++;
            }
            Assert.Greater(cuenta, 0, "no hay vertices con peso dominante en mano_izq");
            palma = suma / cuenta;
        }

        private static int Dominante(BoneWeight bw)
        {
            float max = bw.weight0;
            int idx = bw.boneIndex0;
            if (bw.weight1 > max) { max = bw.weight1; idx = bw.boneIndex1; }
            if (bw.weight2 > max) { max = bw.weight2; idx = bw.boneIndex2; }
            if (bw.weight3 > max) { max = bw.weight3; idx = bw.boneIndex3; }
            return max > 0.001f ? idx : -1;
        }

        private static void LogHuesosBlaster(SkinnedMeshRenderer smr, Mesh horneada, int blaster)
        {
            int[] tris = horneada.GetTriangles(blaster);
            Mesh malla = smr.sharedMesh;
            BoneWeight[] pesos = malla.boneWeights;
            Transform[] huesos = smr.bones;
            var cuenta = new Dictionary<string, int>();
            var vistos = new HashSet<int>();
            for (int i = 0; i < tris.Length; i++)
            {
                int vi = tris[i];
                if (!vistos.Add(vi)) continue;
                int idx = Dominante(pesos[vi]);
                string n = idx >= 0 && idx < huesos.Length && huesos[idx] != null ? huesos[idx].name : "?";
                cuenta.TryGetValue(n, out int c);
                cuenta[n] = c + 1;
            }
            string s = "";
            foreach (KeyValuePair<string, int> kv in cuenta) s += kv.Key + "=" + kv.Value + " ";
            Debug.Log("AIM_BLASTER_HUESOS " + s);
        }

        private static string Fmt(Vector3 v)
        {
            return "(" + v.x.ToString("F0") + "," + v.y.ToString("F0") + "," + v.z.ToString("F0") + ")";
        }

        private static void Capturar(PlayerController pc, string dir, string nombre,
            Vector3 hacia, float distancia, float altura)
        {
            GameObject go = new GameObject("cam_calib");
            Camera cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.015f, 0.025f, 0.05f, 1f);
            cam.fieldOfView = 35f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            cam.enabled = false;
            RenderTexture rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.transform.position = pc.transform.position + hacia.normalized * distancia + Vector3.up * altura;
            cam.transform.LookAt(pc.transform.position + Vector3.up * 0.65f);
            cam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, nombre), tex.EncodeToPNG());
            RenderTexture.active = null;
            UnityEngine.Object.Destroy(tex);
            UnityEngine.Object.Destroy(rt);
            UnityEngine.Object.Destroy(go);
        }
    }
}
