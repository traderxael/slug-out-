using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SlugOut.Slugs;
using SlugOut.World;
using UnityEngine;

namespace SlugOut.Tests
{
    public class ArtePixarTests
    {
        static readonly string[] Huesos =
        {
            "raiz", "cadera", "columna", "cuello", "cabeza",
            "brazo_der", "antebrazo_der", "mano_der",
            "brazo_izq", "antebrazo_izq", "mano_izq",
            "muslo_der", "pantorrilla_der", "pie_der",
            "muslo_izq", "pantorrilla_izq", "pie_izq"
        };

        public static readonly string[] Babosas =
        {
            "electroshock", "ascua", "copo",
            "corriente", "centella", "guijarro", "marea",
            "tenue", "roquito", "liana", "gotita"
        };

        public static readonly string[] BabosasRoster =
        {
            "bengala", "chispa", "elemental-fuego", "fenix", "fosforo", "ignia", "infierno", "vulcano",
            "abisal", "bubbaleone", "elemental-agua", "leviatan", "bloque", "carnero", "coloso",
            "demoledora", "elemental-tierra", "titan", "brisa", "elemental-aire", "huracan",
            "planeadora", "rafaga", "remolino", "tifon", "tornado", "magnetosa", "relampago",
            "tormenta", "zeus", "zumbido", "congelada", "escarcha", "esquirla-helada", "eterna",
            "glaciar", "helada", "polaro", "ventisca", "brote", "enredadera", "espina", "primigenia",
            "selva", "tejedora", "elemental-energia", "enigma", "fandango", "abismo", "ghoul", "mota",
            "oculta", "vacio"
        };

        [Test]
        public void HeroePixarCargaYRespetaElContrato()
        {
            GameObject prefab = Resources.Load<GameObject>("Shane/shane_pixar");
            bool fbx = false;
            if (prefab == null)
            {
                prefab = Resources.Load<GameObject>("Shane/shane_pixar_fbx");
                fbx = prefab != null;
            }
            if (prefab == null) Assert.Ignore("Pendiente: shane_pixar.glb ni shane_pixar_fbx importados en Unity");

            GameObject inst = Object.Instantiate(prefab);
            try
            {
                HashSet<string> nombres = new HashSet<string>();
                foreach (Transform t in inst.GetComponentsInChildren<Transform>(true)) nombres.Add(t.name);
                CollectionAssert.IsSubsetOf(Huesos, nombres, "faltan huesos del contrato en shane_pixar");

                MaterialesPixar.QuitarAnimadores(inst);
                Assert.IsNull(inst.GetComponentInChildren<Animator>(true), "shane_pixar no debe traer Animator");
                Assert.IsNull(inst.GetComponentInChildren<Animation>(true), "shane_pixar no debe traer Animation");
                if (fbx || !MaterialesPixar.EsModeloTexturizado(inst))
                    MaterialesPixar.PrepararHeroeFbx(inst, Color.cyan);

                int tris = ContarTris(inst);
                Assert.LessOrEqual(tris, 16000, "shane_pixar supera el presupuesto de triangulos");

                bool textura = false;
                bool tubos = false;
                foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        if (MaterialesPixar.TieneTextura(m)) textura = true;
                        if (m.name.IndexOf("tubos", System.StringComparison.OrdinalIgnoreCase) >= 0) tubos = true;
                    }
                }
                Assert.IsTrue(textura, "shane_pixar perdio la textura del cuerpo");
                Assert.IsTrue(tubos, "shane_pixar no trae el material _tubos");
            }
            finally
            {
                Object.DestroyImmediate(inst);
            }
        }

        [Test]
        public void BabosaPixarCargaEsEstaticaYConservaTextura(
            [ValueSource(nameof(Babosas))] string id)
        {
            GameObject prefab = Resources.Load<GameObject>("Slugs/" + id + "_pixar");
            if (prefab == null) Assert.Fail("Falta Resources/Slugs/" + id + "_pixar.glb");

            GameObject inst = Object.Instantiate(prefab);
            try
            {
                Assert.AreEqual(0, inst.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length,
                    id + "_pixar debe ser estatica para que la anime SlugAnimator");
                MaterialesPixar.QuitarAnimadores(inst);
                Assert.IsNull(inst.GetComponentInChildren<Animator>(true));
                Assert.LessOrEqual(ContarTris(inst), 7000, id + "_pixar supera el presupuesto");

                MeshRenderer[] mrs = inst.GetComponentsInChildren<MeshRenderer>(true);
                Assert.AreEqual(1, mrs.Length, id + "_pixar debe tener un solo MeshRenderer");
                Assert.That(mrs[0].bounds.size.y, Is.InRange(0.55f, 0.61f), id + ": altura fuera de rango");
                Assert.That(mrs[0].bounds.min.y, Is.EqualTo(0f).Within(0.02f), id + ": no esta en el piso");
                Assert.IsTrue(mrs[0].sharedMaterials.Length > 0 && mrs[0].sharedMaterials[0] != null &&
                    mrs[0].sharedMaterials[0].name.EndsWith("_cuerpo", System.StringComparison.OrdinalIgnoreCase),
                    id + ": la primitiva 0 no es _cuerpo");

                bool cuerpo = false;
                bool brillo = false;
                bool cresta = false;
                foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        if (m.name.EndsWith("_cuerpo", System.StringComparison.OrdinalIgnoreCase) &&
                            MaterialesPixar.TieneTextura(m)) cuerpo = true;
                        if (m.name.EndsWith("_brillo", System.StringComparison.OrdinalIgnoreCase)) brillo = true;
                        if (m.name.EndsWith("_cresta", System.StringComparison.OrdinalIgnoreCase)) cresta = true;
                    }
                }
                Assert.IsTrue(cuerpo, id + "_pixar no trae textura en <id>_cuerpo");
                Assert.IsTrue(brillo, id + "_pixar no trae el material <id>_brillo");
                Assert.IsTrue(cresta, id + "_pixar no trae el material <id>_cresta");
            }
            finally
            {
                Object.DestroyImmediate(inst);
            }
        }

        [Test]
        public void BabosaRosterCumpleContrato(
            [ValueSource(nameof(BabosasRoster))] string id)
        {
            GameObject prefab = Resources.Load<GameObject>("Slugs/" + id + "_pixar");
            if (prefab == null) Assert.Fail("falta Resources/Slugs/" + id + "_pixar.glb");

            GameObject inst = Object.Instantiate(prefab);
            try
            {
                Assert.AreEqual(0, inst.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length,
                    id + "_pixar debe ser estatica para que la anime SlugAnimator");
                MaterialesPixar.QuitarAnimadores(inst);
                Assert.IsNull(inst.GetComponentInChildren<Animator>(true));
                Assert.LessOrEqual(ContarTris(inst), 4500, id + "_pixar supera el presupuesto de roster");

                MeshRenderer[] mrs = inst.GetComponentsInChildren<MeshRenderer>(true);
                Assert.AreEqual(1, mrs.Length, id + "_pixar debe tener un solo MeshRenderer");
                Assert.That(mrs[0].bounds.size.y, Is.InRange(0.55f, 0.61f), id + ": altura fuera de rango");
                Assert.That(mrs[0].bounds.min.y, Is.EqualTo(0f).Within(0.02f), id + ": no esta en el piso");
                Assert.LessOrEqual(mrs[0].sharedMaterials.Length, 6, id + ": demasiados materiales");

                bool cuerpo = false;
                bool cresta = false;
                foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        if (m.name.EndsWith("_cuerpo", System.StringComparison.OrdinalIgnoreCase) &&
                            MaterialesPixar.TieneTextura(m)) cuerpo = true;
                        if (m.name.EndsWith("_cresta", System.StringComparison.OrdinalIgnoreCase)) cresta = true;
                    }
                }
                Assert.IsTrue(cuerpo, id + "_pixar no trae textura en <id>_cuerpo");
                Assert.IsTrue(cresta, id + "_pixar no trae el material <id>_cresta");
            }
            finally
            {
                Object.DestroyImmediate(inst);
            }
        }

        [Test]
        public void ApplyColorNoPisaUnModeloTexturizado()
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) Assert.Ignore("URP/Lit no disponible en este contexto");

            GameObject go = new GameObject("prueba_applycolor");
            Texture2D tex = new Texture2D(4, 4);
            Material mat = new Material(sh);
            mat.SetTexture("_BaseMap", tex);
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            try
            {
                MethodInfo metodo = typeof(SlugArenaGame).GetMethod(
                    "ApplyColor", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.IsNotNull(metodo, "ApplyColor cambio de firma");
                metodo.Invoke(null, new object[] { go, Color.red });
                Assert.AreSame(mat, mr.sharedMaterial, "ApplyColor reemplazo el material texturizado");
                Assert.AreSame(tex, mr.sharedMaterial.GetTexture("_BaseMap"));
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(mat);
                Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void AplicarTexturaNoPisaUnModeloTexturizado()
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) Assert.Ignore("URP/Lit no disponible en este contexto");

            GameObject go = new GameObject("prueba_aplicartextura");
            Texture2D original = new Texture2D(4, 4);
            Texture2D sprite = new Texture2D(4, 4);
            Material mat = new Material(sh);
            mat.SetTexture("_BaseMap", original);
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            try
            {
                MethodInfo metodo = typeof(SlugArenaGame).GetMethod(
                    "AplicarTextura", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.IsNotNull(metodo, "AplicarTextura cambio de firma");
                metodo.Invoke(null, new object[] { go, sprite, Color.cyan });
                Assert.AreSame(original, mr.sharedMaterial.GetTexture("_BaseMap"),
                    "AplicarTextura piso la textura 3D con el sprite 2D");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(mat);
                Object.DestroyImmediate(original);
                Object.DestroyImmediate(sprite);
            }
        }

        [Test]
        public void PrepararHeroeTintaSoloLosTubos()
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) Assert.Ignore("URP/Lit no disponible en este contexto");

            GameObject go = new GameObject("prueba_tubos");
            Material cuerpo = new Material(sh);
            cuerpo.name = "shane_cuerpo";
            Material tubos = new Material(sh);
            tubos.name = "shane_tubos";
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { cuerpo, tubos };
            try
            {
                MaterialesPixar.PrepararHeroe(go, Color.red);
                Assert.AreSame(cuerpo, mr.sharedMaterials[0], "el cuerpo no debe clonarse");
                Assert.AreNotSame(tubos, mr.sharedMaterials[1], "los tubos deben clonarse para tintarse");
                Color emision = MaterialesPixar.Emision(mr.sharedMaterials[1]);
                Assert.Greater(emision.r, 0.5f, "los tubos no quedaron emisivos");
                Assert.Less(emision.g, 0.5f, "los tubos no tomaron el color del elemento");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(cuerpo);
                Object.DestroyImmediate(tubos);
            }
        }

        [Test]
        public void AlinearPiesDejaElModeloEnElPiso()
        {
            GameObject padre = new GameObject("padre");
            padre.transform.position = new Vector3(0f, 1f, 0f);
            GameObject hijo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hijo.transform.SetParent(padre.transform, false);
            try
            {
                MaterialesPixar.AlinearPies(hijo.transform, -1f);
                Renderer r = hijo.GetComponent<Renderer>();
                Assert.That(r.bounds.min.y, Is.EqualTo(0f).Within(0.001f),
                    "los pies no quedaron en el piso del actor");
            }
            finally
            {
                Object.DestroyImmediate(padre);
            }
        }

        [Test]
        public void RetratosDeLasOchoSalvajesCargan()
        {
            string[] ids = { "centella", "corriente", "guijarro", "marea", "tenue", "roquito", "liana", "gotita" };
            string[] proto = { "Centella", "Corriente", "guijarro", "marea", "tenue", "roquito", "liana", "gotita" };
            string[] velo = { "centella", "corriente", "guijarro", "marea", "tenue", "roquito", "liana", "gotita" };
            for (int i = 0; i < ids.Length; i++)
            {
                Texture2D p = Resources.Load<Texture2D>("Sprites/Protoforms/" + proto[i]);
                Texture2D v = Resources.Load<Texture2D>("Sprites/Velocimorphs/" + velo[i]);
                Assert.IsNotNull(p, "falta Sprites/Protoforms/" + proto[i] + ".png");
                Assert.IsNotNull(v, "falta Sprites/Velocimorphs/" + velo[i] + ".png");

                SlugData d = SlugRoster.Buscar(ids[i]);
                Assert.IsNotNull(d, "falta el SlugData " + ids[i]);
                foreach (bool evolucionado in new[] { false, true })
                {
                    Texture2D tex = SlugArenaGame.LoadSprite(d, evolucionado);
                    Assert.IsNotNull(tex, ids[i] + ": LoadSprite devolvio null (evolucionado=" + evolucionado + ")");
                    Assert.IsFalse(tex.name.StartsWith("retrato_"),
                        ids[i] + ": cayo al retrato de color del elemento (evolucionado=" + evolucionado + ")");
                }
            }
        }

        [Test]
        public void RetratosDeLos64Cargan()
        {
            var ids = new System.Collections.Generic.List<string>(Babosas);
            ids.AddRange(BabosasRoster);
            for (int i = 0; i < ids.Count; i++)
            {
                SlugData d = SlugRoster.Buscar(ids[i]);
                Assert.IsNotNull(d, "falta el SlugData " + ids[i]);
                foreach (bool evolucionado in new[] { false, true })
                {
                    Texture2D tex = SlugArenaGame.LoadSprite(d, evolucionado);
                    Assert.IsNotNull(tex, ids[i] + ": LoadSprite devolvio null (evolucionado=" + evolucionado + ")");
                    Assert.IsFalse(tex.name.StartsWith("retrato_"),
                        ids[i] + ": cayo al retrato de color del elemento (evolucionado=" + evolucionado + ")");
                }
            }
        }

        static int ContarTris(GameObject raiz)
        {
            int tris = 0;
            foreach (SkinnedMeshRenderer s in raiz.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (s.sharedMesh != null) tris += s.sharedMesh.triangles.Length / 3;
            foreach (MeshFilter f in raiz.GetComponentsInChildren<MeshFilter>(true))
                if (f.sharedMesh != null) tris += f.sharedMesh.triangles.Length / 3;
            return tris;
        }
    }
}
