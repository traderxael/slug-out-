using UnityEngine;

namespace SlugOut.World
{
    /// Barrido cinemático para el jugador: mueve por transform pero consulta una capsula
    /// contra los colliders de muros antes de avanzar. No usa Rigidbody ni CharacterController,
    /// asi que no compite con Terrain3D.Altura (la Y la fija PlayerController.ApoyarEnPiso).
    public static class ColisionJugador
    {
        public const int CapaMuros = 8;
        public const float RadioDefecto = 0.5f;
        public const float AltoDefecto = 2f;
        public const float Skin = 0.02f;
        public const int Iteraciones = 3;

        public const int Mascara = 1 << CapaMuros;

        static readonly Collider[] _buffer = new Collider[8];

        public static Vector3 Deslizar(Vector3 centro, Vector3 delta, float radio, float alto,
            out bool toco, out Vector3 normal)
        {
            toco = false;
            normal = Vector3.zero;
            delta.y = 0f;
            Vector3 p = centro;
            Vector3 resto = delta;
            float semi = Mathf.Max(0f, alto * 0.5f - radio);

            for (int i = 0; i < Iteraciones; i++)
            {
                float d = resto.magnitude;
                if (d < 0.0001f) break;
                Vector3 dir = resto / d;
                Vector3 abajo = p - Vector3.up * semi;
                Vector3 arriba = p + Vector3.up * semi;
                if (!Physics.CapsuleCast(abajo, arriba, radio, dir, out RaycastHit hit,
                        d + Skin, Mascara, QueryTriggerInteraction.Ignore))
                {
                    p += resto;
                    break;
                }
                toco = true;
                normal = hit.normal;
                float avance = Mathf.Max(0f, hit.distance - Skin);
                p += dir * avance;
                Vector3 resto2 = resto - dir * avance;
                if (hit.normal.y > 0.7f)
                {
                    // Un canto casi horizontal no debe frenar el avance horizontal:
                    // la altura la resuelve Altura, no el barrido.
                    resto = resto2;
                    continue;
                }
                resto = Vector3.ProjectOnPlane(resto2, hit.normal);
                if (resto.sqrMagnitude < 0.000001f) break;
            }
            return p;
        }

        public static bool SobreMuro(Vector3 centro, float radio = RadioDefecto,
            float alto = AltoDefecto)
        {
            float semi = Mathf.Max(0f, alto * 0.5f - radio);
            int n = Physics.OverlapCapsuleNonAlloc(centro - Vector3.up * semi,
                centro + Vector3.up * semi, radio, _buffer, Mascara,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (_buffer[i] != null) return true;
            return false;
        }

        /// Busca un punto cercano sin solape. Se usa al aparecer de un tunel o al
        /// reaparecer: si el punto pedido cae dentro de una roca, prueba alrededor.
        ///
        /// El radio de busqueda tiene que ser MAS ANCHO que el solido: medido en Play, seis
        /// de las 28 posiciones de aparicion caian dentro de un pilar o una roca (spawn 03,
        /// 13, 14, 16, 24 y 26). Con un radio de 3,2 m la busqueda no llegaba a salir del
        /// pilar, el enemigo nacia atrapado, la IA no lo movia y el nivel no se terminaba
        /// nunca. Ahora el abanico llega hasta 9 m: un pilar grande serodea entero.
        public static Vector3 PuntoLibre(Vector3 deseado, Vector3 haciaCentro,
            float radio = RadioDefecto, float alto = AltoDefecto)
        {
            deseado.y = Terrain3D.Altura(deseado.x, deseado.z) + 1f;
            if (!SobreMuro(deseado, radio, alto)) return deseado;
            Vector3 centro = haciaCentro;
            centro.y = 0f;
            if (centro.sqrMagnitude < 0.0001f) centro = -new Vector3(deseado.x, 0f, deseado.z);
            centro.Normalize();
            Vector3 lateral = Vector3.Cross(Vector3.up, centro);
            float[] radios = { 0.6f, 1.2f, 1.8f, 2.4f, 3.2f, 4.2f, 5.4f, 7f, 9f };
            for (int r = 0; r < radios.Length; r++)
            {
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI * 0.25f;
                    Vector3 candidato = deseado + (centro * Mathf.Cos(a) + lateral * Mathf.Sin(a))
                        * radios[r];
                    candidato.y = Terrain3D.Altura(candidato.x, candidato.z) + 1f;
                    if (!SobreMuro(candidato, radio, alto)) return candidato;
                }
            }
            return deseado;
        }
    }
}
