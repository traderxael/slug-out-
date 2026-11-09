using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SlugOut.Editor
{
    public static class BuildArtePixar
    {
        const string Escena = "Assets/Scenes/Mapa3D.unity";
        const string Exe = "Builds/ArtePixar/SLUG OUT Arte.exe";

        [MenuItem("SlugOut/Arte/Build benchmark FPS")]
        public static void Menu()
        {
            Build();
        }

        public static void RunBatch()
        {
            Build();
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        static void Build()
        {
            Directory.CreateDirectory("Builds/ArtePixar");
            BuildPlayerOptions opciones = new BuildPlayerOptions
            {
                scenes = new[] { Escena },
                locationPathName = Exe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            BuildReport reporte = BuildPipeline.BuildPlayer(opciones);
            Debug.Log("BUILD_ARTE result=" + reporte.summary.result + " bytes=" + reporte.summary.totalSize);
            if (reporte.summary.result != BuildResult.Succeeded)
                throw new System.Exception("build fallo: " + reporte.summary.result);
        }
    }
}
