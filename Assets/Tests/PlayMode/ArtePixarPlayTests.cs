using System.Collections;
using System.Reflection;
using NUnit.Framework;
using SlugOut.Slugs;
using SlugOut.World;
using UnityEngine;
using UnityEngine.TestTools;

namespace SlugOut.PlayModeTests
{
    public class ArtePixarPlayTests
    {
        static readonly string[] Babosas =
        {
            "electroshock", "ascua", "copo",
            "corriente", "centella", "guijarro", "marea",
            "tenue", "roquito", "liana", "gotita"
        };

        // Una muestra del roster de 5 materiales: dos por elemento, para montar en
        // runtime sin cargar las 53 en cada corrida.
        static readonly string[] BabosasRoster =
        {
            "bengala", "leviatan", "bloque", "tornado", "zeus", "ventisca",
            "selva", "enigma", "vacio", "chispa", "huracan", "elemental-agua"
        };
        GameObject _actor;

        [TearDown]
        public void Limpiar()
        {
            if (_actor != null) Object.Destroy(_actor);
            _actor = null;
        }

        [UnityTest]
        public IEnumerator HeroeSeMontaEnRuntime()
        {
            SlugData d = SlugRoster.Buscar("electroshock");
            Assert.IsNotNull(d, "falta el SlugData electroshock");
            _actor = new GameObject("actor_heroe");
            _actor.transform.position = new Vector3(0f, 1f, 0f);

            MethodInfo metodo = typeof(SlugArenaGame).GetMethod(
                "AttachShane", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(metodo, "AttachShane cambio de firma");
            try { metodo.Invoke(null, new object[] { _actor, d }); }
            catch (TargetInvocationException e) { Assert.Fail(e.InnerException != null ? e.InnerException.ToString() : e.ToString()); }
            yield return null;

            Transform cuerpo = _actor.transform.Find("Cuerpo_Shane");
            Assert.IsNotNull(cuerpo, "AttachShane no monto Cuerpo_Shane");
            SkinnedMeshRenderer smr = cuerpo.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.IsNotNull(smr, "el heroe no quedo skinned");
            Assert.That(smr.bounds.size.y, Is.InRange(1.5f, 2.2f), "altura del heroe fuera de rango");
            Assert.That(smr.bounds.min.y, Is.EqualTo(0f).Within(0.08f), "los pies del heroe no estan en el piso");
            Assert.IsNull(cuerpo.GetComponentInChildren<Animator>(true), "el heroe conserva un Animator");

            bool tubos = false;
            foreach (Material m in smr.sharedMaterials)
                if (m != null && m.name.ToLowerInvariant().Contains("tubos")) tubos = true;
            Assert.IsTrue(tubos, "el heroe no tiene material _tubos");
        }

        [UnityTest]
        public IEnumerator BabosasSeMontanEnRuntime()
        {
            yield return MontarYComprobar(Babosas, false);
        }

        [UnityTest]
        public IEnumerator BabosasRosterSeMontanEnRuntime()
        {
            yield return MontarYComprobar(BabosasRoster, true);
        }

        IEnumerator MontarYComprobar(string[] ids, bool roster)
        {
            MethodInfo metodo = typeof(SlugArenaGame).GetMethod(
                "AttachModel", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(metodo, "AttachModel cambio de firma");

            foreach (string id in ids)
            {
                SlugData d = SlugRoster.Buscar(id);
                Assert.IsNotNull(d, "falta el SlugData " + id);
                _actor = new GameObject("actor_" + id);
                _actor.transform.position = new Vector3(0f, 1f, 0f);
                object[] args = { _actor, d, new Vector3(0f, -1f, 0f), false };
                bool montado = false;
                try { montado = (bool)metodo.Invoke(null, args); }
                catch (TargetInvocationException e) { Assert.Fail(e.InnerException != null ? e.InnerException.ToString() : e.ToString()); }
                yield return null;

                Assert.IsTrue(montado, id + ": AttachModel no encontro el GLB _pixar");
                Transform cuerpo = _actor.transform.Find("Cuerpo_" + id);
                Assert.IsNotNull(cuerpo, id + ": no monto el cuerpo");
                Assert.AreEqual(0, cuerpo.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length,
                    id + ": debe ser estatica");
                MeshRenderer mr = cuerpo.GetComponentInChildren<MeshRenderer>(true);
                Assert.IsNotNull(mr, id + ": sin renderer");
                Assert.AreEqual(1, cuerpo.GetComponentsInChildren<MeshRenderer>(true).Length,
                    id + ": debe tener un solo MeshRenderer");
                Assert.That(mr.bounds.size.y, Is.InRange(0.55f, 0.61f), id + ": altura fuera de rango");
                Assert.That(mr.bounds.min.y, Is.EqualTo(0f).Within(0.03f), id + ": no esta en el piso");
                Assert.IsTrue(mr.sharedMaterials.Length > 0 && mr.sharedMaterials[0] != null &&
                    mr.sharedMaterials[0].name.EndsWith("_cuerpo", System.StringComparison.OrdinalIgnoreCase),
                    id + ": la primitiva 0 no es _cuerpo");
                if (roster)
                    Assert.LessOrEqual(mr.sharedMaterials.Length, 6, id + ": demasiados materiales");

                bool cuerpoOk = false;
                bool brillo = false;
                bool brilloEmisivo = false;
                bool cresta = false;
                foreach (Material m in mr.sharedMaterials)
                {
                    if (m == null) continue;
                    if (m.name.ToLowerInvariant().Contains("_cuerpo") && MaterialesPixar.TieneTextura(m)) cuerpoOk = true;
                    if (m.name.ToLowerInvariant().Contains("_cresta")) cresta = true;
                    if (m.name.ToLowerInvariant().Contains("_brillo"))
                    {
                        brillo = true;
                        Color e = MaterialesPixar.Emision(m);
                        brilloEmisivo = e.r + e.g + e.b > 0.05f;
                    }
                }
                Assert.IsTrue(cuerpoOk, id + ": el cuerpo perdio la textura");
                Assert.IsTrue(cresta, id + ": falta el material _cresta");
                if (!roster)
                {
                    Assert.IsTrue(brillo, id + ": falta el material _brillo");
                    Assert.IsTrue(brilloEmisivo, id + ": el _brillo no quedo emisivo");
                }

                Object.Destroy(_actor);
                _actor = null;
                yield return null;
            }
        }
    }
}
