using UnityEngine;

namespace SlugOut.World
{
    /// Desplaza la textura de las aguas de la cueva. Un solo material compartido anima
    /// el arroyo, la cascada y el charco; no hay particles ni shader propio.
    public class FlujoAgua : MonoBehaviour
    {
        public Material material;
        public Vector2 velocidad = new Vector2(0f, -0.35f);

        private void Update()
        {
            if (material == null) return;
            float dt = Time.deltaTime;
            // _MainTex: el shader activo es el Standard del pipeline integrado. Preguntar
            // por _BaseMap (propiedad URP) tira una excepcion por cuadro.
            Vector2 off = material.GetTextureOffset("_MainTex") + velocidad * dt;
            material.SetTextureOffset("_MainTex", off);
        }
    }
}
