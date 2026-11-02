using UnityEngine;

namespace SlugOut.World
{
    /// Musica de fondo 100% procedural (sin assets): loop de 4 acordes
    /// (Am - F - C - G, el tono de la cueva) con bajo, arpegio y colchon.
    /// Se genera una vez por clase y se comparte; volumen suave para no
    /// tapar los pips de disparo.
    public class ArenaMusic : MonoBehaviour
    {
        [Header("Mezcla")]
        [SerializeField] private float volumen = 0.3f;
        [SerializeField] private bool arrancar = true;

        [Header("Estado (solo lectura)")]
        [SerializeField] private bool sonando = false;

        private AudioSource _src;
        private static AudioClip _loop;
        private const int SR = 22050;
        private const float BAR = 2.4f;   // 4 compases de 2.4s = loop de 9.6s

        private void Awake()
        {
            _src = GetComponent<AudioSource>();
            if (_src == null) _src = gameObject.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.loop = true;
            _src.spatialBlend = 0f;
            _src.volume = volumen;
        }

        private void Start()
        {
            if (!arrancar) return;
            EnsureLoop();
            if (_loop != null && _src != null)
            {
                _src.clip = _loop;
                _src.Play();
                sonando = true;
            }
        }

        private static void EnsureLoop()
        {
            if (_loop != null) return;

            // Am, F, C, G (frecuencias de la fundamental y las notas del acorde)
            float[][] acordes = new float[][]
            {
                new[] { 110.00f, 130.81f, 164.81f },   // Am: A2 C3 E3
                new[] {  87.31f, 110.00f, 130.81f },   // F:  F2 A2 C3
                new[] { 130.81f, 164.81f, 196.00f },   // C:  C3 E3 G3
                new[] {  98.00f, 123.47f, 146.83f },   // G:  G2 B2 D3
            };

            int n = (int)(4 * BAR * SR);
            float[] d = new float[n];
            int porBarra = (int)(BAR * SR);

            for (int barra = 0; barra < 4; barra++)
            {
                float[] ac = acordes[barra];
                float raiz = ac[0];
                int ini = barra * porBarra;
                // El patron del arpegio NO cambia dentro del compas: estaba declarado dentro del
                // bucle por muestra y alojava un array nuevo en cada una (decenas de miles por
                // generacion), que es la congelacion al arrancar la musica.
                float[] ciclo = { ac[0], ac[2], ac[1], ac[2], ac[0] * 2f, ac[2], ac[1], ac[2] };

                for (int i = 0; i < porBarra; i++)
                {
                    float t = (float)i / SR;
                    float tb = (float)i / porBarra;          // 0..1 dentro del compas
                    float env = Mathf.Exp(-t * 1.8f);        // decaimiento lento del compas

                    // bajo: fundamental una octava abajo del primer tono + brillo
                    float bajo = Mathf.Sin(2f * Mathf.PI * raiz * 0.5f * t)
                               + 0.3f * Mathf.Sin(2f * Mathf.PI * raiz * t);
                    d[ini + i] += bajo * 0.16f * env;

                    // arpegio: corcheas recorriendo el acorde
                    float corchea = BAR / 8f;
                    int paso = Mathf.Min(7, (int)(t / corchea));
                    float tn = t - paso * corchea;
                    float envNota = Mathf.Exp(-tn * 9f);
                    float nota = Mathf.Sin(2f * Mathf.PI * ciclo[paso] * tn)
                               + 0.25f * Mathf.Sin(4f * Mathf.PI * ciclo[paso] * tn);
                    d[ini + i] += nota * 0.10f * envNota;

                    // colchon: los tres tonos sostenidos con ventana sin() (continua entre compases)
                    float ventana = Mathf.Sin(Mathf.PI * tb);
                    float pad = 0f;
                    for (int k = 0; k < ac.Length; k++)
                        pad += Mathf.Sin(2f * Mathf.PI * ac[k] * t + k);
                    d[ini + i] += pad * 0.028f * ventana;
                }
            }

            _loop = AudioClip.Create("musica_cueva", n, 1, SR, false);
            _loop.SetData(d, 0);
        }
    }
}
