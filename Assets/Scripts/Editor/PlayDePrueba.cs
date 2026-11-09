using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SlugOut.Editor
{
    /// Entra en Play sin pasar por el mouse.
    /// - Menu: SLUG OUT/Entrar en Play.
    /// - Linea de comandos: -executeMethod SlugOut.Editor.PlayDePrueba.Entrar
    /// - Bandera: si existe Temp/qoder_play.flag al cargar el Editor, entra solo y borra la bandera.
    public static class PlayDePrueba
    {
        [MenuItem("SLUG OUT/Entrar en Play")]
        public static void Entrar()
        {
            // Si ya estamos en Play, abrir la escena lanza InvalidOperationException.
            if (EditorApplication.isPlaying) return;

            EditorSceneManager.OpenScene("Assets/Scenes/Mapa3D.unity");
            EditorApplication.isPlaying = true;
            Debug.Log("[Qoder] Entrando en Play sobre Mapa3D");
        }

        [InitializeOnLoadMethod]
        private static void VigilarBandera()
        {
            EditorApplication.update -= RevisarBandera;
            EditorApplication.update += RevisarBandera;
        }

        private static void RevisarBandera()
        {
            string raiz = Directory.GetParent(Application.dataPath).FullName;
            string bandera = Path.Combine(raiz, "Temp", "qoder_play.flag");
            if (!File.Exists(bandera)) return;

            File.Delete(bandera);
            EditorApplication.update -= RevisarBandera;
            Entrar();
        }
    }
}
