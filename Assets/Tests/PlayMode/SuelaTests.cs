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
    /// Mide la suela VISIBLE (vertice mas bajo con peso dominante en pie_izq /
    /// pie_der sobre la malla horneada) contra el terreno bajo ESE vertice. El origen
    /// del hueso del tobillo esta ~0,7 m por debajo de la malla en este FBX y no sirve
    /// como planta. Diagnostico explicito.
    public class SuelaTests
    {
        [UnityTest, Explicit("Diagnostico de suela: no corre en la suite normal.")]
        public IEnumerator MideSuelaVisible()
        {
            // El paquete com.unity.ai.assistant loguea un error de relay al azar y
            // tumba la prueba que le toque; este diagnostico mide geometria, no logs.
            LogAssert.ignoreFailingMessages = true;
            SceneManager.LoadScene("Mapa3D");
            yield return null; yield return null; yield return null;
            Assert.IsNotNull(GameFlow.Activo, "Mapa3D no tiene GameFlow");
            GameFlow.Activo.IniciarCampana(1);
            yield return null;
            SlugArenaGame arena = UnityEngine.Object.FindAnyObjectByType<SlugArenaGame>();
            Assert.IsNotNull(arena, "Mapa3D no tiene SlugArenaGame");
            arena.Iniciar(NivelConfig.Campana(1), SlugRoster.Buscar("electroshock"));
            for (int i = 0; i < 25; i++) yield return null;

            StoryPanel historia = UnityEngine.Object.FindAnyObjectByType<StoryPanel>();
            if (historia != null) historia.enabled = false;
            GameFeel.Pausa = false;
            Time.timeScale = 1f;
            Time.captureDeltaTime = 1f / 60f;
            EnemySlugAI[] ias = UnityEngine.Object.FindObjectsByType<EnemySlugAI>();
            for (int i = 0; i < ias.Length; i++) if (ias[i] != null) ias[i].enabled = false;

            PlayerController pc = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            Assert.IsNotNull(pc, "no hay PlayerController");
            Transform hero = pc.transform.Find("Cuerpo_Shane");
            if (hero == null) hero = GameObject.Find("Cuerpo_Shane")?.transform;
            Assert.IsNotNull(hero, "no se monto Cuerpo_Shane");
            SkinnedMeshRenderer smr = hero.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.IsNotNull(smr, "el heroe no tiene SkinnedMeshRenderer");

            int idxIzq = -1, idxDer = -1;
            for (int i = 0; i < smr.bones.Length; i++)
            {
                if (smr.bones[i] == null) continue;
                if (smr.bones[i].name == "pie_izq") idxIzq = i;
                if (smr.bones[i].name == "pie_der") idxDer = i;
            }
            Assert.GreaterOrEqual(idxIzq, 0, "el skin no tiene pie_izq");
            Assert.GreaterOrEqual(idxDer, 0, "el skin no tiene pie_der");

            pc.GetComponent<PlayerController>().enabled = false;
            SlugAnimator raizAnim = pc.GetComponent<SlugAnimator>();
            if (raizAnim != null) raizAnim.enabled = false;
            pc.transform.position = new Vector3(0f, 1f, 0f);
            pc.transform.rotation = Quaternion.identity;

            Mesh malla = new Mesh();
            BoneWeight[] pesos = smr.sharedMesh.boneWeights;
            System.Collections.Generic.List<int> vertsIzq = new System.Collections.Generic.List<int>();
            System.Collections.Generic.List<int> vertsDer = new System.Collections.Generic.List<int>();
            for (int v = 0; v < pesos.Length; v++)
            {
                int dom = Dominante(pesos[v]);
                if (dom == idxIzq) vertsIzq.Add(v);
                if (dom == idxDer) vertsDer.Add(v);
            }
            Assert.Greater(vertsIzq.Count, 0, "sin vertices en pie_izq");
            Assert.Greater(vertsDer.Count, 0, "sin vertices en pie_der");

            void Suelas(out Vector3 izq, out Vector3 der)
            {
                malla.Clear();
                smr.BakeMesh(malla, true);
                Matrix4x4 m = smr.transform.localToWorldMatrix;
                izq = Vector3.zero;
                der = Vector3.zero;
                float yi = float.MaxValue, yd = float.MaxValue;
                for (int i = 0; i < vertsIzq.Count; i++)
                {
                    Vector3 p = m.MultiplyPoint3x4(malla.vertices[vertsIzq[i]]);
                    if (p.y < yi) { yi = p.y; izq = p; }
                }
                for (int i = 0; i < vertsDer.Count; i++)
                {
                    Vector3 p = m.MultiplyPoint3x4(malla.vertices[vertsDer[i]]);
                    if (p.y < yd) { yd = p.y; der = p; }
                }
            }

            DuelistaAnimator anim = pc.GetComponent<DuelistaAnimator>();
            for (int i = 0; i < 40; i++) yield return null;
            Suelas(out Vector3 izqInicio, out Vector3 derInicio);
            if (anim != null)
            {
                const System.Reflection.BindingFlags priv =
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var fLocal = typeof(DuelistaAnimator).GetField("_suelaIzqLocal", priv);
                var fPie = typeof(DuelistaAnimator).GetField("_pieIzq", priv);
                var fY = typeof(DuelistaAnimator).GetField("_posturaY", priv);
                if (fLocal != null && fPie != null && fY != null)
                {
                    Vector3 local = (Vector3)fLocal.GetValue(anim);
                    Transform pie = (Transform)fPie.GetValue(anim);
                    Vector3 mundo = pie.TransformPoint(local);
                    Debug.Log("STANCE_PUNTO y=" + mundo.y.ToString("F3")
                        + " ground=" + Terrain3D.Altura(mundo.x, mundo.z).ToString("F3")
                        + " error=" + (Terrain3D.Altura(mundo.x, mundo.z) - mundo.y).ToString("F3")
                        + " posturaY=" + ((float)fY.GetValue(anim)).ToString("F3")
                        + " local=" + local.ToString("F4"));
                }
            }
            Debug.Log("SUELA_IDLE izq=" + (izqInicio.y - Terrain3D.Altura(izqInicio.x, izqInicio.z)).ToString("F3")
                + " der=" + (derInicio.y - Terrain3D.Altura(derInicio.x, derInicio.z)).ToString("F3")
                + " terreno=" + Terrain3D.Altura(0f, 0f).ToString("F3")
                + " verts=" + vertsIzq.Count + "/" + vertsDer.Count
                + " modeloY=" + hero.localPosition.y.ToString("F3"));

            float minIzq = float.MaxValue, maxIzq = float.MinValue;
            float minDer = float.MaxValue, maxDer = float.MinValue;
            for (int i = 0; i < 240; i++)
            {
                float dt = Time.deltaTime;
                if (dt > 0.0001f) pc.transform.position += Vector3.forward * (6f * dt);
                pc.ApoyarEnPiso();
                yield return null;
                Suelas(out Vector3 izq, out Vector3 der);
                float oi = izq.y - Terrain3D.Altura(izq.x, izq.z);
                float od = der.y - Terrain3D.Altura(der.x, der.z);
                minIzq = Mathf.Min(minIzq, oi); maxIzq = Mathf.Max(maxIzq, oi);
                minDer = Mathf.Min(minDer, od); maxDer = Mathf.Max(maxDer, od);
            }
            Time.captureDeltaTime = 0f;
            Debug.Log("SUELA_CARRERA minIzq=" + minIzq.ToString("F3") + " maxIzq=" + maxIzq.ToString("F3")
                + " minDer=" + minDer.ToString("F3") + " maxDer=" + maxDer.ToString("F3"));
            LogAssert.ignoreFailingMessages = false;
        }

        static int Dominante(BoneWeight bw)
        {
            float max = bw.weight0;
            int idx = bw.boneIndex0;
            if (bw.weight1 > max) { max = bw.weight1; idx = bw.boneIndex1; }
            if (bw.weight2 > max) { max = bw.weight2; idx = bw.boneIndex2; }
            if (bw.weight3 > max) { max = bw.weight3; idx = bw.boneIndex3; }
            return max > 0.001f ? idx : -1;
        }
    }
}
