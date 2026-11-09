using NUnit.Framework;
using SlugOut.World;
using UnityEngine;

namespace SlugOut.EditModeTests
{
    /// Los clips horneados en Blender llegan como JSON y el muestreador los lee sin
    /// Animator ni AnimationClip. Esta guarda comprueba estructura y muestreo puro.
    public class ClipsPiernasTests
    {
        [Test] public void ElJsonTieneCaminarYCarreraConLosSeisHuesos()
        {
            ClipsProcedurales.Cargar();
            Assert.IsTrue(ClipsProcedurales.HayClips, "falta Resources/Animacion/shane_piernas.json");
            ClipsProcedurales.Clip caminar = ClipsProcedurales.Buscar("caminar");
            ClipsProcedurales.Clip carrera = ClipsProcedurales.Buscar("carrera");
            Assert.IsNotNull(caminar, "no hay clip caminar");
            Assert.IsNotNull(carrera, "no hay clip carrera");
            Assert.Greater(caminar.frames.Length, 10);
            Assert.Greater(carrera.frames.Length, 10);
            Assert.AreEqual(6, caminar.frames[0].bones.Length);
            Assert.AreEqual(30, ClipsProcedurales.Fps);

            Assert.IsTrue(ClipsProcedurales.Muestrear(caminar, 0.25f, "muslo_izq", out Quaternion q));
            Assert.Greater(Mathf.Abs(1f - Mathf.Abs(q.w)), 0.001f,
                "el hueso no se movio en el clip");
            Assert.IsFalse(ClipsProcedurales.Muestrear(caminar, 0.25f, "hueso_que_no_existe", out _));
        }

        [Test] public void ImpactoYMuerteSonClipsDeUnaSolaPasada()
        {
            ClipsProcedurales.Clip impacto = ClipsProcedurales.Buscar("impacto");
            ClipsProcedurales.Clip muerte = ClipsProcedurales.Buscar("muerte");
            Assert.IsNotNull(impacto, "no hay clip impacto");
            Assert.IsNotNull(muerte, "no hay clip muerte");
            Assert.GreaterOrEqual(impacto.frames.Length, 8);
            Assert.GreaterOrEqual(muerte.frames.Length, 12);
            Assert.AreEqual(6, impacto.frames[0].bones.Length);
            Assert.AreEqual(12, muerte.frames[0].bones.Length);

            // El impacto mueve brazos: el procedural de impacto nunca los toca, asi que
            // esto solo puede venir del clip.
            Assert.IsTrue(ClipsProcedurales.MuestrearUnaVez(impacto, 0.5f, "brazo_izq", out Quaternion brazo));
            Assert.Greater(Quaternion.Angle(Quaternion.identity, brazo), 3f,
                "el clip de impacto no levanta el brazo");

            // La muerte no empalma: en 1 se queda en la ultima pose, no vuelve al inicio.
            Assert.IsTrue(ClipsProcedurales.MuestrearUnaVez(muerte, 1f, "columna", out Quaternion fin));
            Assert.IsTrue(ClipsProcedurales.MuestrearUnaVez(muerte, 2f, "columna", out Quaternion pasado));
            Assert.Less(Quaternion.Angle(fin, pasado), 0.01f, "el clip de muerte volvio al primer cuadro");
            Assert.Greater(Quaternion.Angle(Quaternion.identity, fin), 20f,
                "la muerte no llega a la pose final");

            Assert.IsTrue(ClipsProcedurales.MuestrearHipUnaVez(muerte, 1f, out float hip));
            Assert.AreEqual(-0.45f, hip, 0.001f, "la cadera no baja al final de la muerte");
            Assert.IsFalse(ClipsProcedurales.MuestrearHipUnaVez(null, 0f, out _));
        }
    }
}
