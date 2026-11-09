using UnityEngine;

namespace SlugOut.World
{
    /// Marca runtime del duelista montado en un enemigo. El HUD la lee para mostrar el nombre
    /// propio y una frase de presentacion; no hay serializados, asi que no toca la escena.
    public class DuelistaMarca : MonoBehaviour
    {
        public string Id { get; private set; }
        public string Nombre { get; private set; }
        public string Frase { get; private set; }
        public int IndiceFrase { get; private set; }
        public float DuracionFrase { get; private set; }
        public float Nace { get; private set; }

        public void FijarDuracionFrase(float segundos)
        {
            DuracionFrase = segundos;
        }

        public void Configurar(string id, string frase, int indiceFrase)
        {
            Id = id;
            Nombre = DuelistaVoces.NombreDe(id);
            Frase = frase;
            IndiceFrase = indiceFrase;
            Nace = Time.time;
        }
    }
}
