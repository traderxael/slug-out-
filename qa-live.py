"""Smoke test de la version publicada en GitHub Pages.
Verifica que el juego desplegado arranca, que la fisica de potencia funciona y que
la pausa congela el canvas. Todo por pixeles: el juego vive en un IIFE y no expone
gameState/state al contexto de la pagina.

La fisica se mide PRIMERO, inmediato al arranque del nivel: el rival lanza su
primer escupitajo a los ~2970ms y ese proyectil baja por la banda de medicion
contaminandola.
"""
import asyncio
from playwright.async_api import async_playwright

URL  = "https://traderxael.github.io/slug-out-/"
EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
W, H = 360, 560            # tamaño logico REAL del canvas
fails = []

def check(name, ok, detail=""):
    print(f"  {'OK  ' if ok else 'FALLA'} {name}" + (f"   [{detail}]" if detail else ""))
    if not ok:
        fails.append(name)

JUGANDO = "() => document.getElementById('overlay').style.display === 'none'"
GRAB = ("() => { const g=document.getElementById('c').getContext('2d',{willReadFrequently:true});"
        " window.__b=g.getImageData(0,0,360,444).data; return 0; }")
# Banda por donde sube el proyectil. meanY = media de las filas tocadas.
BAND = ("() => { const g=document.getElementById('c').getContext('2d',{willReadFrequently:true});"
        " const n=g.getImageData(168,250,25,150).data, b=window.__b; let s=0,c=0;"
        " for(let i=0;i<n.length;i+=4){const j=i/4*4;"
        " if(Math.abs(n[i]-b[j])+Math.abs(n[i+1]-b[j+1])+Math.abs(n[i+2]-b[j+2])>24){"
        " s+=250+Math.floor((i/4)/25);c++;}}"
        " return c>0?{meanY:s/c,n:c}:{meanY:9999,n:0}; }")
HASH = ("() => { const g=document.getElementById('c').getContext('2d',{willReadFrequently:true});"
        " const d=g.getImageData(0,0,360,444).data; let h=0;"
        " for(let i=0;i<d.length;i+=4){ h=(h*31 + d[i] + d[i+1]*3 + d[i+2]*7)|0; } return h; }")

async def main():
    async with async_playwright() as pw:
        b = await pw.chromium.launch(executable_path=EDGE, args=["--mute-audio"])
        p = await (await b.new_context(viewport={"width": 400, "height": 700})).new_page()
        errs, perrs = [], []
        p.on("console", lambda m: errs.append(m.text) if m.type == "error" else None)
        p.on("pageerror", lambda e: perrs.append(str(e)))

        await p.goto(URL, wait_until="load", timeout=60000)
        await p.wait_for_timeout(1500)
        box = await p.locator("#c").bounding_box()
        check("la pagina carga en produccion", await p.locator("#c").count() == 1)
        check("el menu esta visible", await p.evaluate(
            "() => document.getElementById('overlay').style.display !== 'none'"))
        check("el menu publica los 29 logros",
              (await p.locator("#btnLogros").inner_text()) == "Logros 0/29",
              await p.locator("#btnLogros").inner_text())

        def pt(cx, cy):                       # coords de juego -> pixeles de pantalla
            return box["x"] + cx/W*box["width"], box["y"] + cy/H*box["height"]

        await p.click("#btnNiveles"); await p.wait_for_timeout(400)
        await p.click("#lv_1");      await p.wait_for_timeout(600)
        check("el nivel 1 arranca online (overlay oculto)", await p.evaluate(JUGANDO))

        async def nivel_fresco():
            """Vuelve al menu y arranca el nivel 1 de nuevo: reinicia el reloj de
            ataque del rival para que ningun escupitajo baje por la banda."""
            await p.evaluate("() => document.getElementById('exitBtn').click()")
            await p.wait_for_timeout(300)
            await p.click("#btnVolver"); await p.wait_for_timeout(300)
            await p.click("#btnNiveles"); await p.wait_for_timeout(300)
            await p.click("#lv_1");      await p.wait_for_timeout(600)

        async def tiro(y_puntero, espera=150):
            await p.mouse.move(*pt(180, 430)); await p.mouse.down()
            await p.mouse.move(*pt(180, y_puntero), steps=6); await p.mouse.up()
            await p.evaluate(GRAB)           # baseline tras soltar: sin previsualizacion
            await p.wait_for_timeout(espera)
            return await p.evaluate(BAND)

        # potencia = distancia desde el lanzador (180, LY) hasta el puntero.
        # 3 repeticiones con nivel fresco: la separacion debe ser consistente.
        bajas, maximas, px = [], [], []
        for rep in range(3):
            if rep:
                await nivel_fresco()
            f = await tiro(422)
            await p.wait_for_timeout(900)
            F = await tiro(190)
            bajas.append(f["meanY"]); maximas.append(F["meanY"])
            px += [f["n"], F["n"]]
            print(f"    rep {rep+1}: baja y={f['meanY']:.0f} ({f['n']} px)   "
                  f"maxima y={F['meanY']:.0f} ({F['n']} px)")
        sep = [b - m for b, m in zip(bajas, maximas)]
        # La banda tambien atrapa los escupitajos del rival y las rocas que caen,
        # asi que una repeticion suelta puede salir baja: se promedia. El promedio
        # ronda los 24 px cuando el piso de una sola rep daba 13-31 segun la suerte.
        prom = sum(sep) / len(sep)
        print(f"    separacion por repeticion: {[round(s) for s in sep]} px  "
              f"· promedio {prom:.0f} px")
        check("los 6 disparos generaron proyectil de verdad",
              all(n > 1000 for n in px), f"min {min(px)} px cambiados")
        check("la potencia funciona online: el cargado sube MAS ALTO",
              prom > 15,
              f'promedio {prom:.0f} px (local 88 px, sin rival que moleste)')

        await p.keyboard.press("p"); await p.wait_for_timeout(400)
        h1 = await p.evaluate(HASH); await p.wait_for_timeout(500); h2 = await p.evaluate(HASH)
        check("la tecla P congela el juego online",
              not await p.evaluate(JUGANDO) and h1 == h2, f"hash {h1} vs {h2}")
        await p.keyboard.press("p"); await p.wait_for_timeout(400)
        check("existe el boton de pausa en el DOM",
              await p.evaluate("() => document.getElementById('pauseBtn') !== null"))
        await p.evaluate("() => document.getElementById('pauseBtn').click()")
        await p.wait_for_timeout(400)
        check("el BOTON de pausa tambien pausa online", not await p.evaluate(JUGANDO))
        await p.evaluate("() => document.getElementById('btnSeguir').click()")
        await p.wait_for_timeout(400)
        check("Seguir reanuda online", await p.evaluate(JUGANDO))
        reales = [e for e in errs if "404" not in e and "favicon" not in e]
        check("no hay errores de consola online", not perrs and not reales,
              "; ".join((perrs + reales)[:2]))
        await b.close()

asyncio.run(main())
print("\n" + ("=== OK: el juego publicado funciona ===" if not fails
              else f"=== {len(fails)} FALLA(S): " + "; ".join(fails) + " ==="))
raise SystemExit(1 if fails else 0)
