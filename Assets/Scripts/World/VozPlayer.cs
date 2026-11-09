using System.Collections.Generic;
using UnityEngine;

namespace SlugOut.World
{
    /// Reproduce la narracion del fragmento visible y las frases de los duelistas.
    /// Un solo clip a la vez, en un AudioSource hijo para no compartir el de ArenaMusic;
    /// libera el anterior al cambiar para no acumular voces en la GT 640 de 2 GB.
    public class VozPlayer : MonoBehaviour
    {
        public static VozPlayer Instancia { get; private set; }

        public string RutaActual { get { return _ruta; } }
        public float DuracionActual { get { return _clip != null ? _clip.length : 0f; } }
        public bool Sonando { get { return _src != null && _src.isPlaying; } }

        [SerializeField] private float volumen = 0.95f;

        private AudioSource _src;
        private AudioClip _clip;
        private string _ruta;
        private readonly HashSet<string> _avisadas = new HashSet<string>();

        private void Awake()
        {
            if (Instancia != null && Instancia != this) { Destroy(this); return; }
            Instancia = this;

            GameObject hijo = new GameObject("Voz");
            hijo.transform.SetParent(transform, false);
            _src = hijo.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.loop = false;
            _src.spatialBlend = 0f;
            _src.volume = volumen;
        }

        private void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
        }

        /// Deja el clip cargado antes de que la tarjeta lo muestre, para no cargar en el primer cuadro.
        public void PrecargarFragmento(SlugStory.Fragmento f, bool infinito)
        {
            if (!GameFlow.VocesActivas) return;
            Cargar(CatalogoVoces.RutaDeFragmento(f, infinito));
        }

        /// Devuelve la duracion real del clip (0 si no hay voz o falta el archivo), que es la que
        /// usa StoryPanel para estirar la tarjeta y decidir la pausa narrativa.
        public float PlayFragmento(SlugStory.Fragmento f, bool infinito)
        {
            if (!GameFlow.VocesActivas) { Stop(); return 0f; }
            AudioClip clip = Cargar(CatalogoVoces.RutaDeFragmento(f, infinito));
            if (clip == null) return 0f;
            _src.clip = clip;
            _src.Play();
            return clip.length;
        }

        /// Devuelve la duracion del clip para que el HUD mantenga la frase en pantalla lo que dura.
        public float PlayDuelista(string id, int indice)
        {
            if (!GameFlow.VocesActivas) return 0f;
            AudioClip clip = Cargar(CatalogoVoces.RutaDuelista(id, indice));
            if (clip == null) return 0f;
            _src.clip = clip;
            _src.Play();
            return clip.length;
        }

        public void Pausar(bool pausa)
        {
            if (_src == null) return;
            if (pausa) _src.Pause();
            else _src.UnPause();
        }

        public void Stop()
        {
            if (_src != null)
            {
                _src.Stop();
                _src.clip = null;
            }
            if (_clip != null)
            {
                Resources.UnloadAsset(_clip);
                _clip = null;
            }
            _ruta = null;
        }

        private AudioClip Cargar(string ruta)
        {
            if (ruta == _ruta && _clip != null) return _clip;

            if (_src != null) _src.Stop();
            if (_clip != null)
            {
                Resources.UnloadAsset(_clip);
                _clip = null;
            }

            _ruta = ruta;
            _clip = Resources.Load<AudioClip>(ruta);
            if (_clip == null && _avisadas.Add(ruta))
                Debug.LogWarning("[Voz] falta Resources/" + ruta + " (regenerá las voces desde SLUG OUT/Voces)");
            return _clip;
        }
    }
}
