using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using SlugOut.World;
using Debug = UnityEngine.Debug;

namespace SlugOut.Editor
{
    /// Genera las voces con System.Speech + ffmpeg (offline). El guion sale de CatalogoVoces:
    /// no hay una segunda copia de la prosa que se pueda desincronizar.
    public static class GeneradorVoces
    {
        private const string Guion = "Tools/Voces/_guion.json";
        private const string Manifiesto = "Tools/Voces/voces_manifest.json";
        private const string Pronunciacion = "Tools/Voces/pronunciacion.json";
        private const string Script = "Tools/Voces/generar_voces.ps1";
        private const string Salida = "Assets/Resources/Voces";
        private const string Muestras = "Tools/Voces/muestras";

        [Serializable] private class EntradaGuion
        {
            public string id;
            public string perfil;
            public string texto;
            public string texto_tts;
        }

        [Serializable] private class GuionVoces
        {
            public EntradaGuion[] entradas;
        }

        [Serializable] private class ParPronunciacion
        {
            public string de;
            public string a;
        }

        [Serializable] private class TablaPronunciacion
        {
            public ParPronunciacion[] pares;
        }

        [MenuItem("SLUG OUT/Voces/Generar muestra (3 clips)")]
        public static void GenerarMuestra()
        {
            List<EntradaGuion> muestra = new List<EntradaGuion>();
            foreach (EntradaGuion e in ArmarEntradas())
            {
                if (e.id == "campana/1" || e.id == "duelistas/dr-blakk/3" || e.id == "duelistas/trixie/0")
                    muestra.Add(e);
            }
            if (muestra.Count == 0)
            {
                Debug.LogError("[Voces] no encontré las entradas de muestra");
                return;
            }
            Ejecutar(muestra.ToArray(), Muestras, null, true);
        }

        [MenuItem("SLUG OUT/Voces/Generar todas")]
        public static void GenerarTodas()
        {
            Ejecutar(ArmarEntradas(), Salida, Manifiesto, false);
        }

        [MenuItem("SLUG OUT/Voces/Generar todas (forzar)")]
        public static void GenerarTodasForzado()
        {
            Ejecutar(ArmarEntradas(), Salida, Manifiesto, true);
        }

        private static EntradaGuion[] ArmarEntradas()
        {
            Dictionary<string, string> pronunciacion = CargarPronunciacion();
            EntradaVoz[] catalogo = CatalogoVoces.Entradas();
            EntradaGuion[] salida = new EntradaGuion[catalogo.Length];
            for (int i = 0; i < catalogo.Length; i++)
            {
                salida[i] = new EntradaGuion
                {
                    id = catalogo[i].id,
                    perfil = catalogo[i].perfil,
                    texto = catalogo[i].texto,
                    texto_tts = AplicarPronunciacion(catalogo[i].texto, pronunciacion)
                };
            }
            return salida;
        }

        private static Dictionary<string, string> CargarPronunciacion()
        {
            Dictionary<string, string> tabla = new Dictionary<string, string>();
            string ruta = Path.Combine(RaizProyecto(), Pronunciacion);
            if (!File.Exists(ruta))
            {
                Debug.LogWarning("[Voces] falta " + Pronunciacion + ": se sintetiza sin sustituciones");
                return tabla;
            }
            TablaPronunciacion t = JsonUtility.FromJson<TablaPronunciacion>(File.ReadAllText(ruta));
            if (t == null || t.pares == null) return tabla;
            foreach (ParPronunciacion p in t.pares)
                if (!string.IsNullOrEmpty(p.de)) tabla[p.de] = p.a;
            return tabla;
        }

        private static string AplicarPronunciacion(string texto, Dictionary<string, string> tabla)
        {
            string salida = texto;
            foreach (KeyValuePair<string, string> par in tabla)
            {
                salida = Regex.Replace(salida, @"\b" + Regex.Escape(par.Key) + @"\b",
                    par.Value, RegexOptions.IgnoreCase);
            }
            return salida;
        }

        private static void Ejecutar(EntradaGuion[] entradas, string salidaRel, string manifiestoRel, bool forzar)
        {
            string raiz = RaizProyecto();
            string guion = Path.Combine(raiz, Guion);
            Directory.CreateDirectory(Path.GetDirectoryName(guion));
            File.WriteAllText(guion, JsonUtility.ToJson(new GuionVoces { entradas = entradas }, true),
                new UTF8Encoding(false));

            string script = Path.Combine(raiz, Script);
            if (!File.Exists(script)) { Debug.LogError("[Voces] falta " + script); return; }

            StringBuilder args = new StringBuilder();
            args.Append("-NoProfile -ExecutionPolicy Bypass -File ");
            args.Append(Comillas(script));
            args.Append(" -Guion ").Append(Comillas(guion));
            args.Append(" -Salida ").Append(Comillas(Path.Combine(raiz, salidaRel)));
            if (!string.IsNullOrEmpty(manifiestoRel))
                args.Append(" -Manifiesto ").Append(Comillas(Path.Combine(raiz, manifiestoRel)));
            if (forzar) args.Append(" -Forzar");

            Debug.Log("[Voces] generando " + entradas.Length + " clips en " + salidaRel + "…");
            ProcessStartInfo psi = new ProcessStartInfo("powershell.exe", args.ToString());
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;

            try
            {
                using (Process p = Process.Start(psi))
                {
                    string stdout = p.StandardOutput.ReadToEnd();
                    string stderr = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    if (p.ExitCode != 0)
                    {
                        Debug.LogError("[Voces] falló la generación:\n" + stderr + "\n" + stdout);
                        EditorUtility.DisplayDialog("Voces",
                            "Falló la generación. Mirá la consola para el detalle.", "OK");
                        return;
                    }
                    Debug.Log("[Voces] " + stdout.Trim());
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[Voces] no pude ejecutar PowerShell: " + e.Message);
                return;
            }

            if (salidaRel.StartsWith("Assets/"))
            {
                AssetDatabase.Refresh();
                Reimportar(salidaRel);
            }
            else
            {
                Debug.Log("[Voces] muestras en " + Path.Combine(raiz, salidaRel));
            }
        }

        private static void Reimportar(string carpetaRel)
        {
            string absoluta = Path.Combine(RaizProyecto(), carpetaRel);
            if (!Directory.Exists(absoluta)) return;
            foreach (string archivo in Directory.GetFiles(absoluta, "*.ogg", SearchOption.AllDirectories))
            {
                string relativa = archivo.Replace('\\', '/');
                int i = relativa.IndexOf(carpetaRel, StringComparison.OrdinalIgnoreCase);
                if (i < 0) continue;
                relativa = relativa.Substring(i);
                AssetDatabase.ImportAsset(relativa, ImportAssetOptions.ForceUpdate);
            }
        }

        private static string RaizProyecto()
        {
            return Directory.GetParent(Application.dataPath).FullName;
        }

        private static string Comillas(string ruta)
        {
            return "\"" + ruta + "\"";
        }
    }
}
