using UnityEditor;
using UnityEngine;

namespace SlugOut.Editor
{
    /// Las voces largas se streamean desde disco (GT 640, 2 GB); las frases de duelistas son
    /// cortas y se descomprimen en memoria para que suenen al instante. Mono y Vorbis para todo.
    public class ImportadorVoces : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            string ruta = assetPath.Replace('\\', '/');
            if (!ruta.Contains("/Resources/Voces/")) return;

            AudioImporter importer = (AudioImporter)assetImporter;
            AudioImporterSampleSettings ajustes = importer.defaultSampleSettings;
            ajustes.compressionFormat = AudioCompressionFormat.Vorbis;
            ajustes.quality = 0.5f;

            bool bark = ruta.Contains("/duelistas/");
            ajustes.loadType = bark ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.Streaming;
            ajustes.preloadAudioData = bark;
            importer.defaultSampleSettings = ajustes;
            importer.forceToMono = true;
        }
    }
}
