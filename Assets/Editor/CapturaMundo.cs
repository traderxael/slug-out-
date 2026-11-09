using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

namespace SlugOut.EditorTools
{
    /// Captura offscreen del mundo abierto para verificar visualmente los modelos 3D.
    /// Se invoca con: Unity -batchmode -projectPath <proj> -executeMethod SlugOut.EditorTools.CapturaMundo.Render -quit
    public static class CapturaMundo
    {
        private const string ESCENA = "Assets/Scenes/ArtePixar.unity";
        private const string SALIDA = "C:/Users/USUARIO/SLUG OUT/Capturas/mundo_cerca2.png";

        public static void Render()
        {
            EditorSceneManager.OpenScene(ESCENA, OpenSceneMode.Single);

            // Asegurar controlador de mundo abierto
            var mundo = Object.FindObjectOfType<SlugOut.World.MundoAbierto>();
            if (mundo == null)
            {
                var go = new GameObject("MundoAbierto");
                mundo = go.AddComponent<SlugOut.World.MundoAbierto>();
            }

            // Crear NPCs de ejemplo frente al origen con sus modelos 3D
            CrearNpc("Characters/NpcSabio", new Vector3(-2.5f, 0f, 2f));
            CrearNpc("Characters/NpcExplorador", new Vector3(2.5f, 0f, 2f));
            CrearNpc("Characters/NpcBarbaro", new Vector3(0f, 0f, 4f));
            CrearNpc("Characters/Duelista", new Vector3(0f, 0f, -2f));
            CrearNpc("Characters/Jugador", new Vector3(0f, 0f, 0f));

            // Cámara aérea
            var camGO = new GameObject("CamaraCaptura");
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.1f);
            camGO.transform.position = new Vector3(0f, 6f, -13f);
            camGO.transform.LookAt(new Vector3(0f, 1f, 0f));
            cam.fieldOfView = 55f;

            int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            Directory.CreateDirectory(Path.GetDirectoryName(SALIDA));
            File.WriteAllBytes(SALIDA, tex.EncodeToPNG());
            Debug.Log("[CapturaMundo] PNG guardado en: " + SALIDA);

            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }

        private static void CrearNpc(string modeloPath, Vector3 pos)
        {
            pos.y = SlugOut.World.Terrain3D.Altura(pos.x, pos.z);
            var go = new GameObject("NPC_" + Path.GetFileNameWithoutExtension(modeloPath));
            go.transform.position = pos;
            var npc = go.AddComponent<SlugOut.World.NpcAmbulante>();
            npc.modeloPath = modeloPath;
            // Forzar carga inmediata del modelo para la captura (AssetDatabase en editor)
            string ruta = "Assets/Resources/" + modeloPath + ".fbx";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
            if (prefab != null)
            {
                var inst = Object.Instantiate(prefab, pos, Quaternion.identity, go.transform);
                inst.transform.localPosition = Vector3.zero;
                Debug.Log("[CapturaMundo] modelo cargado: " + modeloPath);
            }
            else Debug.LogWarning("[CapturaMundo] No se cargo: " + ruta);
        }
    }
}