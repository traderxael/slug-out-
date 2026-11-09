using UnityEngine;
using SlugOut.Slugs;

namespace SlugOut.World
{
    /// El disparador del fondo del tunel. Entrar con el jugador cruza a la region destino y lo
    /// deja del lado de adentro del tunel de vuelta, para que el viaje se lea como pasaje y no
    /// como teletransporte, y para que no vuelva a disparar al frame siguiente.
    public class TunelPasadizo : MonoBehaviour
    {
        public int destino = -1;
        /// Que tan cerca de la boca hay que pararse para cruzar.
        public float radio = 1.7f;
        public string NombreDestino
        {
            get { return destino >= 0 ? Regiones.De(destino).nombre : ""; }
        }
        SlugActor _jugador;
        static float _ultimoViaje = -999f;
        /// Por que se nego el ultimo viaje: sin esto un false se lee como "el tunel no funciona"
        /// y puede ser cualquier guarda.
        public static string UltimoMotivo { get; private set; }
        const float Enfriamiento = 1.5f;

        void Awake()
        {
            // La region se construye en Awake del manager y el jugador ya esta en escena: si el
            // collider arranca dentro del volumen (spawn cerca del muro) habria un viaje fantasma
            // antes de que el jugador se moviera. OnDisable lo vuelve a abrir, asi que cada
            // reconstruccion paga su propia espera.
            _ultimoViaje = Time.unscaledTime;
        }

        void OnTriggerEnter(Collider other)
        {
            if (destino < 0) return;
            SlugActor a = other.GetComponentInParent<SlugActor>();
            if (a == null || !a.IsPlayer) return;
            Viajar(a.transform);
        }

        /// El camino real. `PlayerController` mueve al jugador escribiendo transform.position y el
        /// jugador no tiene Rigidbody ni CharacterController: sin un Rigidbody en alguno de los dos
        /// lados, Unity no entrega OnTriggerEnter ni aunque el collider se encime, asi que el
        /// trigger de arriba nunca dispara (medido: teletransportado adentro del volumen, el viaje
        /// no ocurria; invocado a mano, si). Se mide distancia en Update, que no depende de fisica.
        void Update()
        {
            if (destino < 0) return;
            // Antes: `_jugador = FindAnyObjectByType<SlugActor>()` y despues `if (!IsPlayer) return`.
            // Con enemigos vivos, FindFirst devuelve un enemigo cualquiera (medido en Play: cacheo
            // "Ascua (enemigo)" con el jugador parado a 0,00 m de la boca), y como el cache no se
            // volvia a resolver, la boca quedaba muerta para el resto de la partida. Se elige el
            // jugador por IsPlayer, y un cache que no es el jugador se descarta.
            if (_jugador == null || !_jugador.IsPlayer) _jugador = ElegirJugador(SlugActor.Vivos);
            SlugActor a = _jugador;
            if (a == null) return;
            Vector3 d = a.transform.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude <= radio * radio) Viajar(a.transform);
        }

        /// El primer actor marcado como jugador, o null. Publico y puro para que la prueba lo
        /// alimente con una lista donde el enemigo va primero, que es justo el caso que rompia.
        public static SlugActor ElegirJugador(System.Collections.Generic.IEnumerable<SlugActor> actores)
        {
            if (actores == null) return null;
            foreach (SlugActor s in actores)
                if (s != null && s.IsPlayer) return s;
            return null;
        }

        void OnDisable() { _ultimoViaje = -999f; }

        /// Publico y sin depender del collider: el arnes de prueba entra por aca, porque un
        /// trigger depende del orden de fisicas del frame y eso no es lo que se quiere verificar.
        public bool Viajar(Transform jugador)
        {
            if (Time.unscaledTime - _ultimoViaje < Enfriamiento)
            {
                UltimoMotivo = "enfriamiento " + (Time.unscaledTime - _ultimoViaje).ToString("F2") + "s";
                return false;
            }
            RegionCuevaSeca region = Object.FindAnyObjectByType<RegionCuevaSeca>();
            if (region == null) { UltimoMotivo = "sin RegionCuevaSeca en escena"; return false; }
            if (jugador == null) { UltimoMotivo = "sin jugador"; return false; }
            if (destino == RegionCuevaSeca.Actual) { UltimoMotivo = "destino es la region actual"; return false; }
            UltimoMotivo = "ok";

            int desde = RegionCuevaSeca.Actual;
            Transform boca;
            // Se le pide la boca que apunta de vuelta a la cueva de origen: con dos bocas por cueva,
            // "la primera" ya no es necesariamente el camino por donde uno entro.
            region.Reconstruir(destino, out boca, desde);
            _ultimoViaje = Time.unscaledTime;

            if (boca != null)
            {
                // Del lado de adentro de la nueva cueva: 3 m hacia el centro desde la boca, para
                // no quedar dentro del tunel que apunta a la region de donde uno viene.
                Vector3 haciaCentro = -boca.position.normalized;
                if (haciaCentro.sqrMagnitude < 0.001f) haciaCentro = Vector3.forward;
                Vector3 p = boca.position + haciaCentro.normalized * 3f;
                PlayerController pc = jugador.GetComponent<PlayerController>();
                if (pc != null) pc.Colocar(p);
                else
                {
                    p.y = Terrain3D.Altura(p.x, p.z) + 1f;
                    jugador.position = p;
                }
            }
            Debug.Log("[Region] viaje " + RegionCuevaSeca.NombreDe(desde) + " -> "
                + RegionCuevaSeca.NombreDe(destino) + " por " + name);
            return true;
        }
    }
}
