using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SlugOut.World;
using UnityEditor;
using UnityEngine;

namespace SlugOut.EditModeTests
{
    /// Guardas de las voces: 55 clips presentes, rutas alineadas con el juego y, sobre todo,
    /// que el audio siga diciendo el texto actual (SHA-256 del manifiesto contra la prosa).
    public class VocesTests
    {
        [Serializable] private class EntradaManifiesto
        {
            public string id;
            public string perfil;
            public string voz;
            public string hash;
            public string texto_tts;
            public string clip;
        }

        [Serializable] private class Manifiesto
        {
            public string generado;
            public string voz_base;
            public EntradaManifiesto[] entradas;
        }

        private static Manifiesto LeerManifiesto()
        {
            string ruta = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "Voces",
                "voces_manifest.json"));
            Assert.That(File.Exists(ruta), Is.True,
                "falta Tools/Voces/voces_manifest.json: generá las voces desde SLUG OUT/Voces");
            Manifiesto m = JsonUtility.FromJson<Manifiesto>(File.ReadAllText(ruta));
            Assert.That(m, Is.Not.Null);
            Assert.That(m.entradas, Is.Not.Null.And.Not.Empty);
            return m;
        }

        [Test] public void ElCatalogoTieneLas55Entradas()
        {
            EntradaVoz[] entradas = CatalogoVoces.Entradas();
            Assert.That(entradas.Length, Is.EqualTo(CatalogoVoces.Total));

            HashSet<string> ids = new HashSet<string>();
            foreach (EntradaVoz e in entradas)
                Assert.That(ids.Add(e.id), Is.True, "id repetido: " + e.id);

            for (int n = 1; n <= SlugStory.TotalCampana; n++)
                Assert.That(ids.Contains("campana/" + n), Is.True, "falta campana/" + n);

            Assert.That(ids.Contains("epilogo"), Is.True, "falta el epílogo");

            foreach (SlugStory.Fragmento f in SlugStory.Infinito())
                Assert.That(ids.Contains("infinito/" + f.nivel), Is.True, "falta infinito/" + f.nivel);

            foreach (DuelistaVoces.Voz v in DuelistaVoces.Voces())
                for (int i = 0; i < v.frases.Length; i++)
                    Assert.That(ids.Contains("duelistas/" + v.id + "/" + i), Is.True,
                        "falta la frase " + i + " de " + v.id);
        }

        [Test] public void CadaEntradaTieneSuClip()
        {
            Manifiesto m = LeerManifiesto();
            Assert.That(m.entradas.Length, Is.EqualTo(CatalogoVoces.Total));

            foreach (EntradaManifiesto e in m.entradas)
            {
                string ruta = "Assets/Resources/" + e.clip + ".ogg";
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ruta);
                Assert.That(clip, Is.Not.Null, "falta " + ruta);
                Assert.That(clip.length, Is.GreaterThan(0.5f), e.id + " dura " + clip.length + " s");
            }
        }

        [Test] public void LosTextosNoCambiaronSinRegenerar()
        {
            Manifiesto m = LeerManifiesto();
            Dictionary<string, string> hashes = new Dictionary<string, string>();
            foreach (EntradaManifiesto e in m.entradas) hashes[e.id] = e.hash;

            foreach (EntradaVoz e in CatalogoVoces.Entradas())
            {
                string hash;
                Assert.That(hashes.TryGetValue(e.id, out hash), Is.True,
                    "el manifiesto no tiene " + e.id);
                Assert.That(hash, Is.EqualTo(CatalogoVoces.HashDe(e.texto)),
                    "editaste la historia de " + e.id + ": regenerá las voces");
            }
        }

        [Test] public void LasRutasCoincidenConElJuego()
        {
            Assert.That(CatalogoVoces.RutaDeFragmento(SlugStory.DeCampana(1), false),
                Is.EqualTo("Voces/campana/1"));
            Assert.That(CatalogoVoces.RutaDeFragmento(SlugStory.Epilogo(), false),
                Is.EqualTo("Voces/epilogo"));
            Assert.That(CatalogoVoces.RutaDeFragmento(SlugStory.Epilogo(), true),
                Is.EqualTo("Voces/epilogo"));
            Assert.That(CatalogoVoces.RutaDuelista("dr-blakk", 5),
                Is.EqualTo("Voces/duelistas/dr-blakk/5"));
            Assert.That(CatalogoVoces.TextoDe("campana/1"), Is.EqualTo(SlugStory.DeCampana(1).texto));
        }

        [Test] public void ElToggleDeVocesAlternaYSeGuarda()
        {
            bool antes = GameFlow.VocesActivas;
            int pref = PlayerPrefs.GetInt("slugout.voces", 1);

            GameObject go = new GameObject("VocesTest");
            go.SetActive(false); // sin Awake: AlternarVoces no depende de Play
            GameFlow gf = go.AddComponent<GameFlow>();

            gf.AlternarVoces();
            bool s1 = GameFlow.VocesActivas;
            int p1 = PlayerPrefs.GetInt("slugout.voces", -1);
            gf.AlternarVoces();
            bool s2 = GameFlow.VocesActivas;
            int p2 = PlayerPrefs.GetInt("slugout.voces", -1);

            PlayerPrefs.SetInt("slugout.voces", pref);
            PlayerPrefs.Save();
            UnityEngine.Object.DestroyImmediate(go);

            Assert.That(s1, Is.Not.EqualTo(antes), "no alternó");
            Assert.That(p1, Is.EqualTo(s1 ? 1 : 0), "no persistió al apagar");
            Assert.That(s2, Is.EqualTo(antes), "no volvió al estado original");
            Assert.That(p2, Is.EqualTo(s2 ? 1 : 0), "no persistió al reactivar");
        }
    }
}
