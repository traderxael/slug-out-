using UnityEngine;
using SlugOut.World;

namespace SlugOut.World
{
    /// Mejoras de nivel (skill level-design): agrega coberturas extra por region sin tocar
    /// las definiciones. Componente en el raiz del terreno, inicializacion perezosa.
    public class NivelMejoras : MonoBehaviour
    {
        [SerializeField] private int pilaresExtra = 3;
        private GameObject _root;

        private void Awake() { Aplicar(); }

        public void Aplicar()
        {
            if (_root != null) return;
            RegionDefinicion d = null;
            try {
                d = Regiones.De(RegionCuevaSeca.Actual);
            } catch { d = null; }
            if (d == null) d = Regiones.Todas != null && Regiones.Todas.Length > 0 ? Regiones.Todas[0] : null;
            if (d == null) return;
            _root = new GameObject("NivelMejoras_Root");
            _root.transform.SetParent(transform, false);
            Shader sh = Shaders.Lit();
            if (sh == null) return;
            var mat = new Material(sh);
            mat.color = d.rocaClara;
            for (int i = 0; i < pilaresExtra; i++)
            {
                float ang = 30f + i * 100f;
                float rad = 12f + i * 3f;
                Vector3 p = new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad) * rad, 0f, Mathf.Sin(ang * Mathf.Deg2Rad) * rad);
                p.y = Terrain3D.Altura(p.x, p.z);
                // Cubo simple via GameObject.CreatePrimitive para no depender de assets
                GameObject cubo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cubo.name = "PilarExtra_" + i;
                cubo.transform.SetParent(_root.transform, false);
                cubo.transform.position = p + new Vector3(0f, 1.5f, 0f);
                cubo.transform.localScale = new Vector3(1.2f, 3f, 1.2f);
                cubo.GetComponent<Renderer>().material = mat;
            }
            Debug.Log("[NivelMejoras] " + pilaresExtra + " pilares extra en " + d.nombre);
        }
    }
}
