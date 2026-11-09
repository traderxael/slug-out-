using UnityEngine;
using UnityEngine.Rendering;

namespace SlugOut.World
{
    /// Acceso a materiales URP y glTF sin pisar texturas. Los modelos glTFast no tienen
    /// `_BaseColor`, `_BaseMap` ni `_EmissionColor`; usan `baseColorFactor`, `baseColorTexture`
    /// y `emissiveFactor`. Escribir las propiedades de URP a ciegas dejaba el material sin color
    /// o lanzaba warnings en cada frame.
    public static class MaterialesPixar
    {
        public static bool EsModeloTexturizado(GameObject raiz)
        {
            if (raiz == null) return false;
            Renderer[] rs = raiz.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++)
            {
                Material[] mats = rs[i].sharedMaterials;
                if (mats == null) continue;
                for (int j = 0; j < mats.Length; j++)
                {
                    Material m = mats[j];
                    if (m == null) continue;
                    if (TieneTextura(m)) return true;
                    string sh = m.shader != null ? m.shader.name : "";
                    if (sh.IndexOf("glTF", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
                }
            }
            return false;
        }

        public static bool TieneTextura(Material m)
        {
            if (m == null) return false;
            if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null) return true;
            if (m.HasProperty("baseColorTexture") && m.GetTexture("baseColorTexture") != null) return true;
            if (m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null) return true;
            return false;
        }

        public static Color ColorBase(Material m)
        {
            if (m == null) return Color.white;
            if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
            if (m.HasProperty("baseColorFactor"))
            {
                Vector4 v = m.GetVector("baseColorFactor");
                return new Color(v.x, v.y, v.z, v.w);
            }
            if (m.HasProperty("_Color")) return m.GetColor("_Color");
            return Color.white;
        }

        public static void SetColorBase(Material m, Color c)
        {
            if (m == null) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            else if (m.HasProperty("baseColorFactor")) m.SetVector("baseColorFactor", c);
            else if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        public static Color Emision(Material m)
        {
            if (m == null) return Color.black;
            if (m.HasProperty("_EmissionColor")) return m.GetColor("_EmissionColor");
            if (m.HasProperty("emissiveFactor"))
            {
                Vector4 v = m.GetVector("emissiveFactor");
                return new Color(v.x, v.y, v.z, v.w);
            }
            return Color.black;
        }

        public static void SetEmision(Material m, Color c)
        {
            if (m == null) return;
            if (m.HasProperty("_EmissionColor"))
            {
                m.SetColor("_EmissionColor", c);
                m.EnableKeyword("_EMISSION");
            }
            else if (m.HasProperty("emissiveFactor"))
            {
                m.SetVector("emissiveFactor", c);
            }
        }

        public static void PrepararHeroe(GameObject raiz, Color colorTubo)
        {
            if (raiz == null) return;
            Renderer[] rs = raiz.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++)
            {
                rs[i].shadowCastingMode = ShadowCastingMode.Off;
                rs[i].receiveShadows = false;
                Material[] mats = rs[i].sharedMaterials;
                if (mats == null || mats.Length == 0) continue;
                Material[] copia = new Material[mats.Length];
                for (int j = 0; j < mats.Length; j++)
                {
                    Material m = mats[j];
                    if (m != null && TerminaEn(m.name, "_tubos"))
                    {
                        Material t = new Material(m);
                        SetColorBase(t, colorTubo);
                        SetEmision(t, colorTubo * 1.1f);
                        m = t;
                    }
                    copia[j] = m;
                }
                rs[i].sharedMaterials = copia;
            }
        }

        public static void PrepararBabosa(GameObject raiz, string slugId, Color colorElemento)
        {
            if (raiz == null) return;
            Renderer[] rs = raiz.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++)
            {
                rs[i].shadowCastingMode = ShadowCastingMode.Off;
                rs[i].receiveShadows = false;
                Material[] mats = rs[i].sharedMaterials;
                if (mats == null || mats.Length == 0) continue;
                Material[] copia = new Material[mats.Length];
                for (int j = 0; j < mats.Length; j++)
                {
                    Material m = mats[j];
                    if (m == null) { copia[j] = null; continue; }
                    Material t = new Material(m);
                    if (TerminaEn(m.name, "_brillo"))
                    {
                        SetColorBase(t, colorElemento);
                        SetEmision(t, colorElemento * 1.2f);
                    }
                    else if (t.HasProperty("_EmissionMap"))
                    {
                        // Mismo autobrillo que el heroe: las babosas salvajes se leian
                        // como siluetas oscuras contra el piso azul de la cueva.
                        Texture mapa = t.HasProperty("_BaseMap") ? t.GetTexture("_BaseMap") : null;
                        if (mapa == null && t.HasProperty("_MainTex")) mapa = t.GetTexture("_MainTex");
                        if (mapa != null)
                        {
                            t.SetTexture("_EmissionMap", mapa);
                            SetEmision(t, new Color(0.28f, 0.30f, 0.34f));
                        }
                    }
                    copia[j] = t;
                }
                rs[i].sharedMaterials = copia;
            }
        }

        /// El FBX exportado por Blender llega con los materiales correctos pero sin las
        /// texturas embebidas que Unity no extrae. Las texturas viven en Resources y acá
        /// se rehacen los materiales URP por rol: cuerpo/blaster texturizados, botas planas,
        /// tubos emisivos.
        public static void PrepararHeroeFbx(GameObject raiz, Color colorTubo)
        {
            if (raiz == null) return;
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            if (sh == null) return;
            Texture2D baseTex = Resources.Load<Texture2D>("Shane/Texturas/shane_base");
            Texture2D normalTex = Resources.Load<Texture2D>("Shane/Texturas/shane_normal");
            Material cuerpoAsset = Resources.Load<Material>("Shane/Materiales/ShaneCuerpo");
            Material blasterAsset = Resources.Load<Material>("Shane/Materiales/ShaneBlaster");
            Material botasAsset = Resources.Load<Material>("Shane/Materiales/ShaneBotas");
            Material tubosAsset = Resources.Load<Material>("Shane/Materiales/ShaneTubos");
            if (baseTex == null || cuerpoAsset == null)
                Debug.LogWarning("[MaterialesPixar] faltan recursos: baseTex=" + (baseTex != null)
                    + " cuerpo=" + (cuerpoAsset != null) + " blaster=" + (blasterAsset != null)
                    + " botas=" + (botasAsset != null) + " tubos=" + (tubosAsset != null));
            int texturizados = 0;
            Renderer[] rs = raiz.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++)
            {
                rs[i].shadowCastingMode = ShadowCastingMode.Off;
                rs[i].receiveShadows = false;
                Material[] mats = rs[i].sharedMaterials;
                if (mats == null || mats.Length == 0) continue;
                Material[] copia = new Material[mats.Length];
                for (int j = 0; j < mats.Length; j++)
                {
                    Material original = mats[j];
                    string nombre = original != null ? original.name.ToLowerInvariant() : "";
                    bool tubos = nombre.Contains("tubos");
                    bool texturado = nombre.Contains("cuerpo") || nombre.Contains("blaster");
                    bool botas = nombre.Contains("botas");
                    Material fuente = tubos ? tubosAsset
                        : texturado ? (nombre.Contains("blaster") ? blasterAsset : cuerpoAsset)
                        : botas ? botasAsset : cuerpoAsset;
                    Material m = fuente != null ? new Material(fuente) : new Material(sh);
                    m.name = (original != null ? original.name : "shane") + "_pixar";

                    if (tubos)
                    {
                        m.color = colorTubo;
                        SetEmision(m, colorTubo * 1.1f);
                    }
                    else if (fuente == null)
                    {
                        if (texturado && baseTex != null)
                        {
                            m.color = Color.white;
                            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", baseTex);
                            if (normalTex != null && m.HasProperty("_BumpMap")) m.SetTexture("_BumpMap", normalTex);
                            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.45f);
                        }
                        else if (botas)
                        {
                            m.color = original != null && original.HasProperty("_Color")
                                ? original.GetColor("_Color")
                                : new Color(0.18f, 0.20f, 0.25f);
                            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.3f);
                        }
                        else if (baseTex != null)
                        {
                            m.color = Color.white;
                            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", baseTex);
                        }
                        else
                        {
                            m.color = new Color(0.4f, 0.42f, 0.46f);
                        }
                    }
                    copia[j] = m;
                    if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null) texturizados++;
                    // Autobrillo leve sobre la textura: en la cueva el fill frio dejaba al
                    // heroe como silueta. Es emision de la propia albedo, no una luz extra.
                    if (!tubos && m.HasProperty("_EmissionMap"))
                    {
                        Texture mapa = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                        if (mapa == null && m.HasProperty("_MainTex")) mapa = m.GetTexture("_MainTex");
                        if (mapa != null)
                        {
                            m.SetTexture("_EmissionMap", mapa);
                            SetEmision(m, new Color(0.30f, 0.32f, 0.36f));
                        }
                    }
                }
                rs[i].sharedMaterials = copia;
            }
            Debug.Log("[MaterialesPixar] heroe: " + rs.Length + " renderer(s), "
                + texturizados + " submesh(es) con textura");
        }

        public static void QuitarAnimadores(GameObject raiz)
        {
            if (raiz == null) return;
            Animator[] animators = raiz.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++) Destruir(animators[i]);
            Animation[] animations = raiz.GetComponentsInChildren<Animation>(true);
            for (int i = 0; i < animations.Length; i++) Destruir(animations[i]);
        }

        /// Corrige la altura del modelo para que su bounds.min.y quede en yObjetivoLocal
        /// respecto del padre. El pivote del jugador esta a 1 m del piso, no en los pies.
        public static void AlinearPies(Transform modelo, float yObjetivoLocal)
        {
            if (modelo == null || modelo.parent == null) return;
            Renderer[] rs = modelo.GetComponentsInChildren<Renderer>(true);
            bool hay = false;
            Bounds b = new Bounds();
            for (int i = 0; i < rs.Length; i++)
            {
                if (rs[i] == null) continue;
                if (!hay) { b = rs[i].bounds; hay = true; }
                else b.Encapsulate(rs[i].bounds);
            }
            if (!hay) return;
            float objetivoMundo = modelo.parent.position.y + yObjetivoLocal;
            modelo.localPosition += new Vector3(0f, objetivoMundo - b.min.y, 0f);
        }

        private static bool TerminaEn(string nombre, string sufijo)
        {
            return !string.IsNullOrEmpty(nombre) &&
                   nombre.EndsWith(sufijo, System.StringComparison.OrdinalIgnoreCase);
        }

        private static void Destruir(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
