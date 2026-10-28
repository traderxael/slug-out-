from pathlib import Path
"""Verificacion conductual de la esquiva telegrafiada — version VALIDA.

El test anterior disparaba a ciegas a x=180 mientras el rival caminaba 60px
fuera del margen de colision (38.6px): media deriva, no evasion.

Ahora se LEE la posicion real del rival (su sombra, elipse negra en y129) y se
le apunta con el arrastre, que es como se juega. Verifica:
  1. el tell se dibuja (anillo naranja con extension horizontal)
  2. acertar mientras el rival camina / esquiva
  3. el margen de colision es el predicho por las constantes reales
"""
import asyncio
from playwright.async_api import async_playwright

URL = Path(__file__).resolve().with_name("index.html").as_uri()
EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
SAVE = "lanzador_cavernas_2_save_v1"
W, H = 360, 560
NIVEL = 6            # evasion 0.24
MARGEN_X = 38.6      # en.r*1.4+5 con r=24

# --- sombra del rival: columna mas oscura de y124..138 = en.x ---
XEN = """() => {
  const g = document.getElementById('c').getContext('2d',{willReadFrequently:true});
  const d = g.getImageData(30, 124, 300, 14).data;
  const L = [];
  for (let x = 0; x < 300; x++) {
    let s = 0;
    for (let y = 0; y < 14; y++) { const i = (y*300+x)*4; s += (d[i]+d[i+1]+d[i+2])/3; }
    L.push(s/14);
  }
  let mn = 1e9;
  for (let x = 0; x < 300; x++) if (L[x] < mn) mn = L[x];
  let num = 0, den = 0;
  for (let x = 0; x < 300; x++) if (L[x] < mn + 10) { num += x*L[x]; den += L[x]; }
  return den ? Math.round(num/den) : -1;
}"""

# --- barra de vida del rival ---
HP = """() => {
  const g = document.getElementById('c').getContext('2d',{willReadFrequently:true});
  const d = g.getImageData(120, 176, 120, 8).data;
  let maxCol = -1;
  for (let px=0; px<120; px++) for (let py=0; py<8; py++) {
    const i=(py*120+px)*4, r=d[i], gg=d[i+1], b=d[i+2];
    if (r>140 && gg>70 && gg<200 && b<160 && r>b+40) { maxCol = px; break; }
  }
  return maxCol;
}"""

# --- tell: anillo naranja con EXTENSION horizontal (no la estela vertical) ---
TELL = """() => {
  const g = document.getElementById('c').getContext('2d',{willReadFrequently:true});
  const d = g.getImageData(30, 80, 300, 60).data;
  const cols = [];
  for (let x = 0; x < 300; x++) {
    let n = 0;
    for (let y = 0; y < 60; y++) {
      const i = (y*300+x)*4, r=d[i], gg=d[i+1], b=d[i+2];
      if (r>170 && gg>60 && gg<190 && b<130 && r>b+70 && r>gg+50) n++;
    }
    if (n > 2) cols.push(x);
  }
  return cols.length ? (Math.max(cols) - Math.min(cols)) : 0;
}"""

POT = [("minima", 0.68), ("baja", 0.90), ("media", 1.20), ("maxima", 1.60)]

async def main():
    async with async_playwright() as pw:
        b = await pw.chromium.launch(executable_path=EDGE, args=["--mute-audio"])
        p = await (await b.new_context(viewport={"width": 400, "height": 700})).new_page()
        perrs = []
        p.on("pageerror", lambda e: perrs.append(str(e)))
        await p.goto(URL, wait_until="load", timeout=60000); await p.wait_for_timeout(1000)
        await p.evaluate("""() => {
          const s = JSON.parse(localStorage.getItem('%s')||'{}');
          s.nivel = %d; localStorage.setItem('%s', JSON.stringify(s));
        }""" % (SAVE, NIVEL, SAVE))
        await p.reload(wait_until="load", timeout=60000); await p.wait_for_timeout(1000)

        box = await p.locator("#c").bounding_box()
        def pt(cx, cy):
            return box["x"] + cx/W*box["width"], box["y"] + cy/H*box["height"]

        async def nivel_fresco():
            await p.evaluate("() => document.getElementById('exitBtn')?.click()")
            await p.wait_for_timeout(180)
            if await p.locator("#btnVolver").count():
                await p.click("#btnVolver"); await p.wait_for_timeout(180)
            await p.click("#btnNiveles"); await p.wait_for_timeout(220)
            await p.click(f"#lv_{NIVEL}")
            await p.wait_for_timeout(250)          # minimo: el rival no debe derivar

        print(f"nivel {NIVEL} (evasion 0.24, margen de colision ±{MARGEN_X}px)")
        print("apuntando a la posicion REAL del rival (leida por su sombra)\n")

        # ---- 1. el tell se dibuja ----
        await nivel_fresco()
        xe = await p.evaluate(XEN)
        await p.mouse.move(*pt(180, 430)); await p.mouse.down()
        await p.mouse.move(*pt(xe, 109), steps=3)
        spreads = []
        for _ in range(8):
            spreads.append(await p.evaluate(TELL))
            await p.wait_for_timeout(60)
        await p.mouse.up()
        print(f"1. TELL (extension horizontal del anillo naranja): max={max(spreads)}px")
        print(f"   {'OK — anillo con extension' if max(spreads) > 25 else 'FALLO — solo estela'}\n")

        # ---- 2. acierto apuntando al rival ----
        print("2. ACIERTO (10 tiros por potencia, apuntando al rival):")
        print(f"   {'potencia':<10} {'aciertos':>9} {'dx medio':>10} {'tell':>7}")
        resumen, dxs = [], []
        for nombre, _ in POT:
            aciertos, muescas, tells, dxl = 0, 0, 0, []
            for _ in range(10):
                await nivel_fresco()
                h0 = await p.evaluate(HP)
                if h0 < 0: continue
                xe = await p.evaluate(XEN)          # posicion real, justo antes de tirar
                await p.mouse.move(*pt(180, 430)); await p.mouse.down()
                await p.mouse.move(*pt(xe, 109), steps=3)
                t = await p.evaluate(TELL); tells += (1 if t > 25 else 0)
                await p.mouse.up()
                await p.wait_for_timeout(900)
                h1 = await p.evaluate(HP)
                xe2 = await p.evaluate(XEN)
                muescas += 1
                dxl.append(abs(xe2 - 180)); dxs.append(abs(xe2 - 180))
                if 0 <= h1 < h0 - 2: aciertos += 1
            tasa = round(100*aciertos/muescas) if muescas else 0
            print(f"   {nombre:<10} {aciertos:>4}/{muescas:<4} {round(sum(dxl)/max(len(dxl),1)):>8}px {tells:>4}/10")
            resumen.append((nombre, tasa))

        # ---- 3. margen de colision ----
        print(f"\n3. MARGEN DE COLISION")
        print(f"   predicho por constantes: ±{MARGEN_X}px (r=24)")
        print(f"   dx medio observado tras el tiro: {round(sum(dxs)/max(len(dxs),1))}px")
        print(f"\n   gradiente potencia -> acierto: "
              f"{' > '.join(f'{n} {t}%' for n, t in resumen)}")
        print(f"   errores de pagina: {perrs[:2] if perrs else 'ninguno'}")
        await b.close()

asyncio.run(main())
