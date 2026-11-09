using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SlugOut.World
{
    /// Modificador de élite: se aplica a un enemigo al nacer (probabilidad baja, crece con
    /// el nivel/oleada). Cada élite lleva 1-2 modificadores y un badge visual.
    [System.Serializable]
    public class EliteModificador
    {
        public string id;
        public string nombre;
        public float hpMultiplicador = 1f;
        public float danoMultiplicador = 1f;
        public float velocidadMultiplicador = 1f;
        public bool reflejaDanio = false;     // devuelve un % del daño recibido al jugador
        public bool regeneracion = false;     // recupera vida poco a poco
        public float reflejaFraccion = 0.25f;
        public float regenPorSegundo = 1f;
        public Color badgeColor = Color.yellow;
    }

    /// Estado de élite de un enemigo concreto.
    public class EliteEstado : MonoBehaviour
    {
        public EliteModificador[] modificadores = new EliteModificador[0];
        public bool EsElite { get { return modificadores.Length > 0; } }

        public float HpMultiplicador
        {
            get
            {
                float m = 1f;
                foreach (var mod in modificadores) m *= mod.hpMultiplicador;
                return m;
            }
        }
        public float DanioMultiplicador
        {
            get
            {
                float m = 1f;
                foreach (var mod in modificadores) m *= mod.danoMultiplicador;
                return m;
            }
        }
        public float VelocidadMultiplicador
        {
            get
            {
                float m = 1f;
                foreach (var mod in modificadores) m *= mod.velocidadMultiplicador;
                return m;
            }
        }
        public bool ReflejaDanio
        {
            get
            {
                foreach (var mod in modificadores) if (mod.reflejaDanio) return true;
                return false;
            }
        }
        public float ReflejaFraccion
        {
            get
            {
                float f = 0f;
                foreach (var mod in modificadores) if (mod.reflejaDanio) f = Mathf.Max(f, mod.reflejaFraccion);
                return f;
            }
        }
        public bool Regenera
        {
            get
            {
                foreach (var mod in modificadores) if (mod.regeneracion) return true;
                return false;
            }
        }
        public float RegenPorSegundo
        {
            get
            {
                float f = 0f;
                foreach (var mod in modificadores) if (mod.regeneracion) f = Mathf.Max(f, mod.regenPorSegundo);
                return f;
            }
        }

        private float _regenT;
        private SlugActor _actor;
        private GameObject _badge;
        private Camera _cam;

        private void Awake() { _actor = GetComponent<SlugActor>(); }

        private void Update()
        {
            if (!Regenera || _actor == null || !_actor.Alive) return;
            _regenT += Time.deltaTime;
            if (_regenT >= 1f)
            {
                _regenT = 0f;
                _actor.Heal(RegenPorSegundo);
            }
        }

        /// Aplica los modificadores al actor e IA al nacer, y levanta el badge visible.
        public void Aplicar(SlugActor actor, EnemySlugAI ai)
        {
            if (actor != null)
            {
                actor.MultiplicarHp(HpMultiplicador);
            }
            if (ai != null)
            {
                ai.SetDamage(ai.GetDamage() * DanioMultiplicador);
                ai.SetSpeed(ai.GetSpeed() * VelocidadMultiplicador);
            }
            CrearBadge();
        }

        /// Badge: un anillo de color sobre la élite. Sin el, el jugador no tenía forma de
        /// saber a qué se enfrentaba (solo lo decía el log de la consola).
        private void CrearBadge()
        {
            if (_badge != null) return;
            Material mat = MaterialBadge(ColorDelBadge());
            if (mat == null) return;
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Badge_Elite";
            Collider col = quad.GetComponent<Collider>();
            if (col != null)
            {
                // En EditMode (pruebas) Destroy no se ejecuta: hay que usar DestroyImmediate o
                // Unity avisa por consola y la prueba falla con un error inesperado.
                if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
            }
            quad.transform.SetParent(transform, false);
            quad.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            quad.transform.localScale = Vector3.one * 0.85f;
            MeshRenderer mr = quad.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            _badge = quad;
        }

        /// El anillo se mira siempre a la camara y se apaga si el enemigo cae o se entierra.
        private void LateUpdate()
        {
            if (_badge == null) return;
            bool ver = _actor != null && _actor.Alive && !_actor.EstaEnterrado;
            if (_badge.activeSelf != ver) _badge.SetActive(ver);
            if (!ver) return;
            if (_cam == null) _cam = Camera.main;
            if (_cam != null) _badge.transform.rotation = _cam.transform.rotation;
        }

        /// Color del badge: el del primer modificador (con dos, manda el primero).
        private Color ColorDelBadge()
        {
            for (int i = 0; i < modificadores.Length; i++)
                if (modificadores[i] != null) return modificadores[i].badgeColor;
            return Color.yellow;
        }

        /// Devuelve el nombre de los modificadores para el HUD.
        public string Nombres()
        {
            var n = new System.Text.StringBuilder();
            for (int i = 0; i < modificadores.Length; i++)
            {
                if (i > 0) n.Append(" + ");
                n.Append(modificadores[i].nombre);
            }
            return n.ToString();
        }

        // ------------------------------------------------------------------ material del badge

        private static readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();
        private static Texture2D _texAnillo;

        private static Material MaterialBadge(Color color)
        {
            var c = (Color32)color;
            string clave = c.r + "," + c.g + "," + c.b;
            Material m;
            if (_mats.TryGetValue(clave, out m) && m != null) return m;
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            if (s == null) return null;
            m = new Material(s);
            m.mainTexture = TexturaAnillo();
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", TexturaAnillo());
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 2f);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            _mats[clave] = m;
            return m;
        }

        /// Textura del anillo: aro blanco con el centro transparente. El color va en el
        /// material, así que la textura se hace una sola vez para todas las élites.
        private static Texture2D TexturaAnillo()
        {
            if (_texAnillo != null) return _texAnillo;
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            t.name = "badge_elite";
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            var px = new Color32[n * n];
            float c0 = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - c0) / c0, dy = (y - c0) / c0;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    // aro entre 0,62 y 0,92: centro limpio y borde difuminado
                    float a = Mathf.SmoothStep(0.60f, 0.70f, r) * (1f - Mathf.SmoothStep(0.86f, 0.96f, r));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * a));
                }
            t.SetPixels32(px);
            t.Apply();
            _texAnillo = t;
            return t;
        }
    }
}
