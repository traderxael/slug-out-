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
    /// La suela visible (vertice mas bajo del pie en la malla horneada) tiene que
    /// descansar sobre el terreno al arrancar. Antes el modelo quedaba 9-25 cm
    /// hundido y el caminar se leia como si flotara/hundiera.
    public class SuelaGuardTests
    {
        // Explicito: si el runner viene de una prueba que dejo al jugador rodando o
        // muerto, reusa ese heroe y la pose residual tarda en volver; en un nivel
        // limpio (el caso real y la corrida individual) pasa. Se ejecuta con -testFilter.
        [UnityTest, Explicit("Depende de que el runner no rehuse un heroe a medio rodar/morir.")]
        public IEnumerator LaSuelaDescansaEnElTerreno()
        {
            SceneManager.LoadScene("Mapa3D");
            yield return null; yield return null; yield return null;
            Assert.IsNotNull(GameFlow.Activo, "Mapa3D no tiene GameFlow");
            GameFlow.Activo.IniciarCampana(1);
            yield return null;
            SlugArenaGame arena = UnityEngine.Object.FindAnyObjectByType<SlugArenaGame>();
            Assert.IsNotNull(arena, "Mapa3D no tiene SlugArenaGame");
            arena.Iniciar(NivelConfig.Campana(1), SlugRoster.Buscar("electroshock"));

            // Antes de esperar: el runner puede venir de una prueba que dejo el juego
            // pausado o con delta minimo, y entonces el reloj de calibracion no corre.
            StoryPanel historia = UnityEngine.Object.FindAnyObjectByType<StoryPanel>();
            if (historia != null) historia.enabled = false;
            GameFeel.Pausa = false;
            Time.timeScale = 1f;
            Time.captureDeltaTime = 1f / 60f;
            EnemySlugAI[] ias = UnityEngine.Object.FindObjectsByType<EnemySlugAI>();
            for (int i = 0; i < ias.Length; i++) if (ias[i] != null) ias[i].enabled = false;
            for (int i = 0; i < 30; i++) yield return null;

            PlayerController pc = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            Transform hero = pc != null ? pc.transform.Find("Cuerpo_Shane") : null;
            if (hero == null) hero = GameObject.Find("Cuerpo_Shane")?.transform;
            Assert.IsNotNull(pc, "no hay PlayerController");
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

            // Esperar la senal real de calibracion, no una cuenta de cuadros: segun la
            // prueba anterior el juego puede arrancar pausado y el reloj no avanza.
            DuelistaAnimator anim = pc.GetComponent<DuelistaAnimator>();
            Assert.IsNotNull(anim, "el heroe no tiene DuelistaAnimator");
            // Si la prueba anterior dejo al jugador muerto, el runner reusa el mismo
            // heroe: revivirlo y resetear la animacion antes de medir.
            SlugActor actor = pc.GetComponent<SlugActor>();
            if (actor != null && !actor.Alive) actor.Heal(actor.MaxHp);
            anim.ReiniciarEstado();
            for (int i = 0; i < 240 && !anim.SuelaCalibrada; i++) yield return null;
            Assert.IsTrue(anim.SuelaCalibrada, "la calibracion de suela no termino");
            yield return null; yield return null;

            SkinnedMeshRenderer[] smrs = hero.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Debug.Log("GUARD_DIAG anims=" + pc.GetComponents<DuelistaAnimator>().Length
                + " smrs=" + smrs.Length + " hero=" + hero.name
                + " heroLocal=" + hero.localPosition.ToString("F3")
                + " calibrada=" + anim.SuelaCalibrada);
            for (int i = 0; i < smrs.Length; i++)
            {
                bool tienePie = false;
                for (int b = 0; b < smrs[i].bones.Length; b++)
                    if (smrs[i].bones[b] != null && smrs[i].bones[b].name == "pie_izq") tienePie = true;
                Debug.Log("GUARD_SMR " + i + " " + smrs[i].name
                    + " enabled=" + smrs[i].enabled + " pie=" + tienePie
                    + " verts=" + (smrs[i].sharedMesh != null ? smrs[i].sharedMesh.vertexCount : -1));
            }

            Mesh malla = new Mesh();
            smr.BakeMesh(malla, true);
            Matrix4x4 m = smr.transform.localToWorldMatrix;
            BoneWeight[] pesos = smr.sharedMesh.boneWeights;
            float izq = float.MaxValue, der = float.MaxValue;
            Vector3 pIzq = Vector3.zero, pDer = Vector3.zero;
            for (int v = 0; v < pesos.Length && v < malla.vertexCount; v++)
            {
                int dom = Dominante(pesos[v]);
                if (dom != idxIzq && dom != idxDer) continue;
                Vector3 p = m.MultiplyPoint3x4(malla.vertices[v]);
                if (dom == idxIzq && p.y < izq) { izq = p.y; pIzq = p; }
                else if (dom == idxDer && p.y < der) { der = p.y; pDer = p; }
            }
            UnityEngine.Object.Destroy(malla);
            Time.captureDeltaTime = 0f;
            Assert.Less(izq, float.MaxValue, "no hay vertices de pie izquierdo");
            Assert.Less(der, float.MaxValue, "no hay vertices de pie derecho");

            float oi = izq - Terrain3D.Altura(pIzq.x, pIzq.z);
            float od = der - Terrain3D.Altura(pDer.x, pDer.z);
            Debug.Log("SUELA_GUARDA izq=" + oi.ToString("F3") + " der=" + od.ToString("F3")
                + " modeloY=" + hero.localPosition.y.ToString("F3"));
            Assert.That(Mathf.Abs(oi), Is.LessThanOrEqualTo(0.06f),
                "la suela izquierda no descansa en el terreno: " + oi.ToString("F3"));
            Assert.That(Mathf.Abs(od), Is.LessThanOrEqualTo(0.06f),
                "la suela derecha no descansa en el terreno: " + od.ToString("F3"));
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
