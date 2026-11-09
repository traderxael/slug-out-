using SlugOut.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlugOut.Editor
{
    /// Deja la camara de Mapa3D en tercera persona sobre el hombro. Es una escena
    /// authoring: el offset serializado manda sobre el default del componente.
    public static class ConfigurarTerceraPersona
    {
        [MenuItem("SlugOut/Arte/Configurar camara tercera persona")]
        public static void Menu()
        {
            Configurar();
            EditorUtility.DisplayDialog("Tercera persona", "Camara de Mapa3D configurada.", "OK");
        }

        public static void RunBatch()
        {
            try
            {
                Configurar();
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError("TERCERA_PERSONA_ERROR " + e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        static void Configurar()
        {
            Scene escena = EditorSceneManager.OpenScene("Assets/Scenes/Mapa3D.unity");
            CameraFollow cam = Object.FindAnyObjectByType<CameraFollow>();
            PlayerController pc = Object.FindAnyObjectByType<PlayerController>();
            if (cam == null) throw new System.Exception("Mapa3D no tiene CameraFollow");
            if (pc == null) throw new System.Exception("Mapa3D no tiene PlayerController");

            SerializedObject so = new SerializedObject(cam);
            so.FindProperty("target").objectReferenceValue = pc.transform;
            so.FindProperty("offset").vector3Value = new Vector3(0.7f, 2.7f, -3.6f);
            so.FindProperty("terceraPersona").boolValue = true;
            so.FindProperty("seguirOrientacion").boolValue = true;
            so.FindProperty("lookAtTarget").boolValue = true;
            so.FindProperty("alturaFoco").floatValue = 0.85f;
            so.FindProperty("paredes").intValue = ~0;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
            Debug.Log("TERCERA_PERSONA_OK offset=" + so.FindProperty("offset").vector3Value +
                      " target=" + pc.name);
        }
    }
}
