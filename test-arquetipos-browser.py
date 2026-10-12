"""
Verificacion end-to-end de los arquetipos en el juego REAL (Edge headless).

Las suites node prueban la mecanica por estado y el log de dibujo. Esta prueba lo
que queda: que el juego CORRE en un navegador de verdad con el canvas real, sin
errores, y que la marca del arquetipo aparece durante el juego.

Deteccion del aura de furia: rgba(255,95,109) sobre fondo oscuro da ~(208,79,91).
El fuego (#e05a30 -> 224,90,48) se descarta por el azul: la furia tiene b>85 y el
fuego b<60, asi que el filtro no los confunde.

El jefe de TODO nivel es frenetico, asi que alcanza con jugar el nivel 1 (3
enemigos) para ver el aura al 50% de su vida.
"""
import asyncio
from playwright.async_api import async_playwright

URL  = "file:///C:/Users/USUARIO/slug-out-/index.html"
EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
SAVE = "lanzador_cavernas_2_save_v1"

# Cuenta pixeles del aura roja de furia en la banda donde vive el rival.
FURIA = """() => {
  const g = document.getElementById('c').getContext('2d',{willReadFrequently:true});
  const d = g.getImageData(0, 0, 360, 200).data;   // banda superior: ahi esta el rival
  let n = 0;
  for (let i = 0; i < d.length; i += 4){
    const r=d[i], gg=d[i+1], b=d[i+2];
    if (r>200 && b>85 && b<145 && gg<130 && r>b+60 && r>gg+70) n++;
  }
  return n;
}"""

# El juego sigue vivo si el canvas cambia entre muestras.
HUELLA = """() => {
  const g = document.getElementById('c').getContext('2d',{willReadFrequently:true});
  const d = g.getImageData(0, 0, 360, 560).data;
  let h = 0;
  for (let i = 0; i < d.length; i += 400) h = (h*31 + d[i]) % 1000003;
  return h;
}"""

async def main():
    async with async_playwright() as pw:
        b = await pw.chromium.launch(executable_path=EDGE, args=["--mute-audio"])
        p = await (await b.new_context(viewport={"width": 400, "height": 700})).new_page()
        perrs = []
        p.on("pageerror", lambda e: perrs.append(str(e)))

        await p.goto(URL, wait_until="load", timeout=60000); await p.wait_for_timeout(800)
        await p.evaluate("""() => {
          const s = JSON.parse(localStorage.getItem('%s')||'{}');
          s.nivel = 1; localStorage.setItem('%s', JSON.stringify(s));
        }""" % (SAVE, SAVE))
        await p.reload(wait_until="load", timeout=60000); await p.wait_for_timeout(800)
        await p.click("#btnNiveles"); await p.wait_for_timeout(300)
        await p.click("#lv_1");      await p.wait_for_timeout(300)

        print("jugando el nivel 1 en Edge real (jefe = frenetico):")
        huellas, maxFuria, vioFuria = set(), 0, False
        for k in range(150):                        # ~90s de juego real
            # disparar: el angulo por defecto apunta al centro, donde esta el rival
            await p.keyboard.press("Space")
            await p.wait_for_timeout(420)
            huellas.add(await p.evaluate(HUELLA))
            f = await p.evaluate(FURIA)
            if f > maxFuria: maxFuria = f
            if f > 25: vioFuria = True
            if k % 20 == 0:
                print(f"  {k*420//1000:3d}s  aura furia: {f:5d}px   (max {maxFuria})")

        print(f"\n  errores de pagina:      {perrs[:2] if perrs else 'ninguno'}")
        print(f"  cuadros distintos:      {len(huellas)} (el juego corre y se redibuja)")
        print(f"  aura de furia maxima:   {maxFuria}px")
        ok = (not perrs) and len(huellas) > 20 and vioFuria
        print(f"\n  {'OK — corre sin errores y el frenetico se ve en juego' if ok else 'FALLO'}")
        await b.close()
        raise SystemExit(0 if ok else 1)

asyncio.run(main())
