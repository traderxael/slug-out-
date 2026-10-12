"""
Verificacion visual de las rocas en el juego REAL (Edge headless).

Las suites node prueban la mecanica por estado. Esta prueba lo que el jugador
ve: que el marcador del tell aparece en el techo, que la roca se ve cayendo, y
que ambos son distinguibles del fondo. Sin esto la mecanica podria funcionar y
ser invisible.
"""
import asyncio
from playwright.async_api import async_playwright

URL  = "file:///C:/Users/USUARIO/slug-out-/index.html"
EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
SAVE = "lanzador_cavernas_2_save_v1"
W, H = 360, 560

# Cuenta pixeles del color de la roca (#a08b6d), separando el marcador del tell
# (techo, y<46) de la roca cayendo (y 46..445, sobre el fondo oscuro).
ROCA = """() => {
  const g = document.getElementById('c').getContext('2d',{willReadFrequently:true});
  const d = g.getImageData(0, 0, 360, 560).data;
  const es = (i) => {
    const r=d[i], gg=d[i+1], b=d[i+2];
    return r>125 && r<215 && gg>105 && gg<180 && b>75 && b<160 && r>b+28 && r>gg+8;
  };
  let tell = 0, cayendo = 0;
  for (let y = 0; y < 560; y++) {
    for (let x = 0; x < 360; x++) {
      if (es((y*360+x)*4)) { if (y < 46) tell++; else if (y < 445) cayendo++; }
    }
  }
  return {tell: tell, cayendo: cayendo};
}"""

async def main():
    async with async_playwright() as pw:
        b = await pw.chromium.launch(executable_path=EDGE, args=["--mute-audio"])
        p = await (await b.new_context(viewport={"width": 400, "height": 700})).new_page()
        perrs = []
        p.on("pageerror", lambda e: perrs.append(str(e)))
        await p.goto(URL, wait_until="load", timeout=60000); await p.wait_for_timeout(1000)
        await p.evaluate("""() => {
          const s = JSON.parse(localStorage.getItem('%s')||'{}');
          s.nivel = 6; localStorage.setItem('%s', JSON.stringify(s));
        }""" % (SAVE, SAVE))
        await p.reload(wait_until="load", timeout=60000); await p.wait_for_timeout(1000)

        await p.click("#btnNiveles"); await p.wait_for_timeout(300)
        await p.click("#lv_6");      await p.wait_for_timeout(200)

        print("rocas visibles en el juego real (nivel 6):")
        print(f"  {'t':>6} {'marcador tell':>15} {'roca cayendo':>14} {'delta':>8}")
        vioTell, maxTell, maxCaer, base = False, 0, 0, None
        for k in range(70):                      # ~11s: da tiempo a ver tell + caida
            await p.wait_for_timeout(150)
            r = await p.evaluate(ROCA)
            # La primera muestra es la linea de BASE: la cornisa y el fondo ya
            # son marrones y suman pixeles aunque no haya ninguna roca (la
            # primera aparece a ~2.6s). Medir el delta contra esa base es lo que
            # separa "hay una roca" de "el escenario es marron".
            if base is None: base = r["cayendo"]
            delta = r["cayendo"] - base
            if r["tell"]   > maxTell: maxTell = r["tell"]
            if delta > maxCaer:      maxCaer = delta
            if r["tell"] > 0:    vioTell = True
            if delta > 300:      vioCaer = True
            if k % 5 == 0 or delta > 300:
                print(f"  {k*150+200:5d}ms {r['tell']:>15} {r['cayendo']:>14} {delta:>+8}")

        print(f"\n  marcador del tell visto: {vioTell} (max {maxTell}px)")
        print(f"  roca cayendo vista:      {vioCaer} (max {maxCaer}px)")
        ok = vioTell and maxTell > 20 and maxCaer > 300
        print(f"\n  {'OK — el tell y la roca se ven' if ok else 'FALLO — algo no se dibuja'}")
        print(f"  errores de pagina: {perrs[:2] if perrs else 'ninguno'}")
        await b.close()
        raise SystemExit(0 if ok else 1)

asyncio.run(main())
