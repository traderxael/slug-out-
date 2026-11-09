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
        [SerializeField] private int rollsPlayed = 0;

        public int RollsPlayed { get { return rollsPlayed; } }

        private AudioSource _src;
        private static AudioClip _shotClip, _hitClip, _captureClip, _victoryClip, _defeatClip, _rollClip;
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
            // Chime distinto por elemento: cada uno tiene su propia altura de arpegio.
            float tono = TonoPorElemento(elemento);
            Play(_captureClip, 0.8f, tono);
            Burst(elemento, 26, 0.13f, 3.2f);
            capturesPlayed++;
        }

        /// Jingle al transformar el tiro: velocomorph (fase 1) o megamorph (fase 2).
        public void Transformacion(int fase)
        {
            EnsureClips();
            float tono = fase >= 2 ? 1.4f : 1.15f;
            Play(_captureClip, 0.9f, tono);
            Burst(new Color(1f, 0.8f, 0.2f), 14, 0.1f, 4f);
        }

        /// Altura del chime de captura según el elemento, para que cada uno suene distinto.
        private static float TonoPorElemento(Color c)
        {
            float gris = (c.r + c.g + c.b) / 3f;
            return 0.8f + gris * 0.6f; // 0.8..1.4
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

        /// Rodada: soplo corto y polvo bajo, sin destello. No usa Hit() para no contaminar el
        /// contador de impactos cuando lo que pasó fue que el cuerpo se salio del corredor.
        public void Rodada()
        {
            EnsureClips();
            Play(_rollClip, 0.5f, 1.1f);
            Burst(new Color(0.62f, 0.68f, 0.72f), 8, 0.08f, 1.6f);
            rollsPlayed++;
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
            Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (s == null) s = Shader.Find("Universal Render Pipeline/Particles/Lit");
            if (s == null) s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            Material m = new Material(s);
            // Sin textura el ParticleSystem dibuja cuadrados planos. Un radial suave convierte
            // el mismo burst en chispas redondas sin sumar un asset al proyecto.
            Texture2D gota = Gota();
            m.mainTexture = gota;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", gota);
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_EmissionColor"))
            {
                m.SetColor("_EmissionColor", color * 1.2f);
                m.EnableKeyword("_EMISSION");
            }
            // Blend aditivo + sin escritura de profundidad: se lee como luz, no como plastico.
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 2f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.renderQueue = 3000;
            pr.sharedMaterial = m;

            ps.Play();
            Destroy(go, 2.5f);
        }

        static Texture2D _gota;

        /// Radial de 32 px, blanco con alfa suave. Se comparte entre todos los bursts.
        private static Texture2D Gota()
        {
            if (_gota != null) return _gota;
            const int n = 32;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.name = "fx_gota";
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            var px = new Color32[n * n];
            float c0 = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - c0) / c0, dy = (y - c0) / c0;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.25f, 1f, r));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            t.SetPixels32(px);
            t.Apply(false, true);
            _gota = t;
            return t;
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
            _rollClip = Whoosh();
        }

        /// Ruido con envoltura de campana: el roce de una rodada, corto y sin pico.
        private static AudioClip Whoosh()
        {
            int n = (int)(0.18f * SR);
            float[] d = new float[n];
            float r = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                r = Mathf.Lerp(r, Random.Range(-1f, 1f), 0.18f);
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.18f)) * Mathf.Exp(-t * 6f);
                d[i] = r * env * 0.55f;
            }
            AudioClip c = AudioClip.Create("fx_whoosh", n, 1, SR, false);
            c.SetData(d, 0);
            return c;
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
