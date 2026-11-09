using UnityEngine;

namespace SlugOut.World
{
    /// Colliders de lo que el jugador no debe atravesar. Todo son cajas estaticas:
    /// cero MeshCollider (nada de cooking en runtime) y cero dependencias de fisica.
    ///
    /// El borde no se construye aca: son las cuatro `Pared_*` de la escena, que ya se ven.
    /// `RegionCuevaSeca.BordeCueva` las pasa a la capa 8 y les sube el alto. Hacer ademas un
    /// anillo propio dejaba dos muros concentricos, y el nuevo se leia como cajas negras.
    public static class MurosCueva
    {
        /// Cara interior de las paredes del borde: centro en +-25 con grosor 1 en escala 1.
        public const float CaraBorde = 24.5f;

        public static Transform Raiz(Transform padre)
        {
            Transform r = padre != null ? padre.Find("Muros") : null;
            if (r != null) return r;
            var go = new GameObject("Muros");
            r = go.transform;
            if (padre != null) r.SetParent(padre, false);
            r.localPosition = Vector3.zero;
            r.localRotation = Quaternion.identity;
            r.localScale = Vector3.one;
            return r;
        }

        public static void Caja(Transform padre, string nombre, Vector3 pos, Quaternion rot,
            Vector3 tam, int capa)
        {
            var go = new GameObject(nombre);
            go.layer = capa;
            go.transform.SetParent(padre, false);
            go.transform.position = pos;
            go.transform.rotation = rot;
            go.transform.localScale = tam;
            go.AddComponent<BoxCollider>();
        }
    }
}
