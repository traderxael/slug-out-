using System.Text;
using NUnit.Framework;
using UnityEngine;
using SlugOut.World;

/// El pedido pide un HUD "sin solapamientos". Las dos pruebas de layout que habia cubrian el menu
/// y la ficha de la coleccion; esta cubre la pantalla de combate en tiempo real, que es la que mas
/// elementos fijos tiene (columna de vida + retrato + carga + MPH + equipada,
/// y abajo ayuda + tubos + rodada). Barre resoluciones y capacidades de tubos sobre los MISMO
/// `GameHUD.Rect...` que usa el dibujo, asi que la guarda se rompe cuando alguien mueve un numero
/// y tapa a un vecino, no cuando alguien relee un comentario.
public class HudCombateLayoutTests
{
    static readonly float[] Anchos = { 1024f, 1152f, 1280f, 1360f, 1600f, 1920f };
    static readonly float[] Altos = { 576f, 720f, 768f, 900f, 1080f };
    // 6 es la capacidad del player en la escena; se barre 4..8 porque es un [SerializeField] que
    // alguien puede mover. Con 9 o mas la fila de ayuda ya tocari los tubos a 1024 de ancho.
    static readonly int[] Capacidades = { 4, 6, 8 };

    /// UnityEngine.Rect no expone OverlapRect: el cruce se prueba a mano con el testeo AABB de
    /// siempre. Que funcione lo comprueba LaGuardaDetectaUnaFilaDemasiadoLlena.
    static bool SePisan(Rect a, Rect b)
    {
        return a.xMin < b.xMax && b.xMin < a.xMax && a.yMin < b.yMax && b.yMin < a.yMax;
    }

    static string Que(Rect a, string nombreA, Rect b, string nombreB)
    {
        return nombreA + " [" + a.x.ToString("F0") + "," + a.y.ToString("F0") + " .. "
            + (a.xMax).ToString("F0") + "," + (a.yMax).ToString("F0") + "] pisa "
            + nombreB + " [" + b.x.ToString("F0") + "," + b.y.ToString("F0") + " .. "
            + (b.xMax).ToString("F0") + "," + (b.yMax).ToString("F0") + "]";
    }

    [Test] public void LaFilaInferiorNoSePisa()
    {
        var fallas = new StringBuilder();
        foreach (float w in Anchos)
            foreach (float h in Altos)
                foreach (int cap in Capacidades)
                {
                    Rect ayuda = GameHUD.RectAyuda(w, h);
                    Rect tubos = GameHUD.RectTubos(w, h, cap);
                    Rect rodada = GameHUD.RectRodada(w, h);
                    if (SePisan(ayuda, tubos))
                        fallas.AppendLine("  " + (int)w + "x" + (int)h + " cap" + cap + ": "
                            + Que(ayuda, "ayuda", tubos, "tubos"));
                    if (SePisan(tubos, rodada))
                        fallas.AppendLine("  " + (int)w + "x" + (int)h + " cap" + cap + ": "
                            + Que(tubos, "tubos", rodada, "rodada"));
                    if (SePisan(ayuda, rodada))
                        fallas.AppendLine("  " + (int)w + "x" + (int)h + ": "
                            + Que(ayuda, "ayuda", rodada, "rodada"));
                }
        Assert.That(fallas.ToString(), Is.Empty, "la fila de abajo se solapa:\n" + fallas);
    }

    /// Ningun rectangulo que el HUD de combate dibuja se toca con otro, en ninguna de las
    /// resoluciones barridas. Es pareado (no solo vecinas): la columna tiene bandas que comparten
    /// altura a proposito —cuenta y estado—, y la barrita de energia comparte altura con el titulo
    /// de la babosa equipada; si alguien estira una, esta prueba la nombra.
    [Test] public void LasBandasDelHudDeCombateNoSePisan()
    {
        var fallas = new StringBuilder();
        foreach (float w in Anchos)
            foreach (float h in Altos)
                foreach (bool retrato in new[] { false, true })
                {
                    var banda = new System.Collections.Generic.List<string>
                        { "estado", "equipada", "energia", "mph", "carga",
                          "vida", "tubos", "etiquetaTubos", "ayuda", "rodada" };
                    var rect = new System.Collections.Generic.List<Rect>
                    {
                        GameHUD.RectEstado(w, h, retrato),
                        GameHUD.RectEquipada(w, h, retrato),
                        GameHUD.RectEnergia(w, h, retrato),
                        GameHUD.RectMPH(w, h, retrato),
                        GameHUD.RectCarga(w, h, retrato),
                        GameHUD.RectVida(w, h, retrato),
                        GameHUD.RectTubos(w, h, 6),
                        GameHUD.RectTubosEtiqueta(w, h, 6),
                        GameHUD.RectAyuda(w, h),
                        GameHUD.RectRodada(w, h),
                    };
                    if (retrato)
                    {
                        banda.Add("retrato");
                        rect.Add(GameHUD.RectRetrato(w, h));
                    }
                    for (int i = 0; i < rect.Count; i++)
                        for (int j = i + 1; j < rect.Count; j++)
                            if (SePisan(rect[i], rect[j]))
                                fallas.AppendLine((int)w + "x" + (int)h + (retrato ? " con" : " sin")
                                    + " retrato: " + Que(rect[i], banda[i], rect[j], banda[j]));
                }
        Assert.That(fallas.ToString(), Is.Empty, "el HUD de combate se solapa:\n" + fallas);
    }

    [Test] public void NingunaBandaSeSaleDelCliente()
    {
        foreach (float w in Anchos)
            foreach (float h in Altos)
                foreach (bool retrato in new[] { false, true })
                {
                    Rect[] bandas = {
                        GameHUD.RectEstado(w, h, retrato), GameHUD.RectEquipada(w, h, retrato),
                        GameHUD.RectMPH(w, h, retrato), GameHUD.RectCarga(w, h, retrato),
                        GameHUD.RectVida(w, h, retrato), GameHUD.RectRetrato(w, h),
                        GameHUD.RectTubos(w, h, 6), GameHUD.RectTubosEtiqueta(w, h, 6),
                        GameHUD.RectAyuda(w, h), GameHUD.RectRodada(w, h),
                    };
                    for (int i = 0; i < bandas.Length; i++)
                    {
                        Rect r = bandas[i];
                        Assert.That(r.yMin, Is.GreaterThanOrEqualTo(0f),
                            "la banda " + i + " arranca arriba del cliente a " + (int)w + "x" + (int)h);
                        Assert.That(r.yMax, Is.LessThanOrEqualTo(h),
                            "la banda " + i + " se sale por abajo a " + (int)w + "x" + (int)h);
                        Assert.That(r.xMin, Is.GreaterThanOrEqualTo(0f),
                            "la banda " + i + " arranca fuera de la izquierda a " + (int)w + "x" + (int)h);
                        Assert.That(r.xMax, Is.LessThanOrEqualTo(w),
                            "la banda " + i + " se sale por la derecha a " + (int)w + "x" + (int)h);
                    }
                }
    }

    /// Que la guarda puede fallar: si la ayuda se estira hasta tocar los tubos, o si los tubos se
    /// corren al centro con una capacidad grande, esta prueba lo tiene que ver. Se simula con la
    /// misma formula a 1024 de ancho y una capacidad que si choca (9), para que el archivo no
    /// declare un verde que no sabe medir.
    [Test] public void LaGuardaDetectaUnaFilaDemasiadoLlena()
    {
        Rect ayuda = GameHUD.RectAyuda(1024f, 768f);
        Rect tubosNueve = GameHUD.RectTubos(1024f, 768f, 9);
        Assert.That(SePisan(ayuda, tubosNueve), Is.True,
            "con 9 tubos a 1024 de ancho la ayuda y los tubos YA se tocan: si esto no falla, la "
            + "prueba de arriba no esta mirando la geometria real");
        Assert.That(SePisan(GameHUD.RectTubos(1024f, 768f, 8), ayuda), Is.False,
            "con 8 tubos todavía no se tocan: si esto falla, el margen que barre la prueba de "
            + "arriba (4..8) no es real");
    }
}
