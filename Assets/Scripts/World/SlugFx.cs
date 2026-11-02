using UnityEngine;

namespace SlugOut.World
{
    /// FX de babosa: audio procedural (clips generados por codigo, sin assets)
    /// y particulas de evento con el color elemental. Los clips se crean una vez
    /// por clase y se comparten entre todas las babosas.
    public class SlugFx : MonoBehaviour
    {
        [Header("Volumen")]
        [SerializeField] private float volumen = 0.6f;

        [Header("Contadores (solo lectura)")]
        [SerializeField] private int shotsPlayed = 0;
        [SerializeField] private int hitsPlayed = 0;
        [SerializeField] private int capturesPlayed = 0;
        [SerializeField] private int victoryPlayed = 0;
        [SerializeField] private int defeatPlayed = 0;

        private AudioSource _src;
        private static AudioClip _shotClip, _hitClip, _captureClip, _victoryClip, _defeatClip;
        private const int SR = 22050;

        private void Awake()
        {
            _src = GetComponent<AudioSource>();
            if (_src == null) _src = gameObject.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.spatialBlend = 0f;   // 2D: suena igual sin importar la camara
            _src.volume = volumen;
        }

        // ---------- API ----------

        public void Shot()
        {
            EnsureClips();
            Play(_shotClip, 0.7f, 1f);
            shotsPlayed++;
        }

        /// mult = multiplicador elemental del golpe: fuerte suena mas agudo y salpica mas.
        public void Hit(Color elemento, float mult = 1f)
        {
            EnsureClips();
            bool fuerte = mult > 1f, debil = mult < 1f;
            Play(_hitClip, 0.9f, fuerte ? 1.25f : (debil ? 0.8f : 1f));
            Burst(elemento, fuerte ? 18 : (debil ? 6 : 10), 0.09f, fuerte ? 3f : 2.2f);
            hitsPlayed++;
        }

        public void Capture(Color elemento)
        {
            EnsureClips();
            Play(_captureClip, 0.8f, 1f);
            Burst(elemento, 26, 0.13f, 3.2f);
            capturesPlayed++;
        }

        public void Victory(Color elemento)
        {
            EnsureClips();
            Play(_victoryClip, 0.9f, 1f);
            Burst(elemento, 40, 0.15f, 4.0f);
            victoryPlayed++;
        }

        public void Defeat(Color elemento)
        {
            EnsureClips();
            Play(_defeatClip, 0.9f, 1f);
            defeatPlayed++;
        }

        private void Play(AudioClip clip, float volumen, float tono)
        {
            if (_src == null || clip == null) return;
            _src.pitch = tono;
            _src.PlayOneShot(clip, volumen);
        }

        // ---------- particulas ----------

        private void Burst(Color color, int cantidad, float tamano, float velocidad)
        {
            GameObject go = new GameObject("FX_" + gameObject.name);
            go.transform.position = transform.position + Vector3.up * 0.5f;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);   // el chorro sale hacia arriba

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            // nace reproduciendo: parar antes de escribir en main/emission o Unity rechaza los setters
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.duration = 0.9f;
            main.loop = false;
            main.startLifetime = 0.7f;
            main.startSpeed = velocidad;
            main.startSize = tamano;
            main.startColor = color;
            main.gravityModifier = 0.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)cantidad) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;

            ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
            Shader s = Shader.Find("Universal Render Pipeline/Particles/Lit");
            if (s == null) s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            Material m = new Material(s);
            m.color = color;
            if (m.HasProperty("_EmissionColor"))
            {
                m.SetColor("_EmissionColor", color * 1.2f);
                m.EnableKeyword("_EMISSION");
            }
            pr.sharedMaterial = m;

            ps.Play();
            Destroy(go, 2.5f);
        }

        // ---------- audio procedural ----------

        private static void EnsureClips()
        {
            if (_shotClip != null) return;
            _shotClip = Pip();
            _hitClip = RuidoGolpe();
            _captureClip = Arpegio(new[] { 880f, 660f, 880f, 1174f }, 0.09f, 0.9f);
            _victoryClip = Arpegio(new[] { 523f, 659f, 784f, 1046f }, 0.14f, 1f);
            _defeatClip = Glide(420f, 110f, 0.7f);
        }

        /// Pip corto que sube de tono: el disparo.
        private static AudioClip Pip()
        {
            int n = (int)(0.09f * SR);
            float[] d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                float f = 900f + 500f * (t / 0.09f);
                float env = Mathf.Exp(-t * 30f);
                d[i] = Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.8f;
            }
            AudioClip c = AudioClip.Create("fx_pip", n, 1, SR, false);
            c.SetData(d, 0);
            return c;
        }

        /// Ruido suavizado con decaimiento: el golpe.
        private static AudioClip RuidoGolpe()
        {
            int n = (int)(0.13f * SR);
            float[] d = new float[n];
            float r = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                r = Mathf.Lerp(r, Random.Range(-1f, 1f), 0.35f);
                float env = Mathf.Exp(-t * 26f);
                d[i] = r * env * 0.9f;
            }
            AudioClip c = AudioClip.Create("fx_golpe", n, 1, SR, false);
            c.SetData(d, 0);
            return c;
        }

        /// Notas cortas con brillo (armonico): captura y victoria.
        private static AudioClip Arpegio(float[] notas, float durNota, float gain)
        {
            int n = (int)(notas.Length * durNota * SR);
            float[] d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                int nota = Mathf.Min(notas.Length - 1, (int)(t / durNota));
                float tn = t - nota * durNota;
                float env = Mathf.Exp(-tn * 14f);
                d[i] = (Mathf.Sin(2f * Mathf.PI * notas[nota] * tn)
                        + 0.4f * Mathf.Sin(4f * Mathf.PI * notas[nota] * tn)) * env * 0.5f * gain;
            }
            AudioClip c = AudioClip.Create("fx_arpegio", n, 1, SR, false);
            c.SetData(d, 0);
            return c;
        }

        /// Tono que cae: la derrota.
        private static AudioClip Glide(float f0, float f1, float dur)
        {
            int n = (int)(dur * SR);
            float[] d = new float[n];
            float fase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float f = Mathf.Lerp(f0, f1, t);
                fase += 2f * Mathf.PI * f / SR;
                float env = Mathf.Exp(-t * 3.5f) * (t < 0.85f ? 1f : (1f - t) / 0.15f);
                d[i] = Mathf.Sin(fase) * env * 0.7f;
            }
            AudioClip c = AudioClip.Create("fx_glide", n, 1, SR, false);
            c.SetData(d, 0);
            return c;
        }
    }
}
