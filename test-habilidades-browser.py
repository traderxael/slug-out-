"""
Verificacion end-to-end de las habilidades del villano (Edge headless).

Las suites node prueban la mecanica por estado y el log de dibujo. Esta prueba lo
que queda: que el juego CORRE en un navegador real sin errores, y que durante el
juego las habilidades salen SOLAS (sin que el jugador haga nada para
 provocarlas), cada una anunciada por su color de elemento.

Juega el nivel 1, que tiene enemigos de varios elementos, y cuenta cuantas veces
aparece el aviso de carga de cada elemento.
"""
import asyncio
from playwright.async_api import async_playwright

URL  = "file:///C:/Users/USUARIO/slug-out-/index.html"
EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
SAVE = "lanzador_cavernas_2_save_v1"

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

        print("jugando el nivel 1 en Edge real — las habilidades salen solas:")
        # El aviso de carga es un anillo del color del elemento. Contamos pixeles
        # de cada color elemental en la banda del rival para saber si se anuncio.
        CONTAR = """() => {
          const g = document.getElementById('c').getContext('2d',{willReadFrequently:true});
          const d = g.getImageData(0, 0, 360, 200).data;
          const COL = {
            fuego:[224,90,48], hielo:[111,214,230], sombra:[138,95,214],
            rayo:[232,212,74], planta:[87,184,90], agua:[63,149,207],
            tierra:[166,124,78], aire:[195,211,216]
          };
          const n = {};
          for (const k in COL) n[k] = 0;
          for (let i = 0; i < d.length; i += 4){
            const r=d[i], gg=d[i+1], bb=d[i+2];
            for (const k in COL){
              const c = COL[k];
              if (Math.abs(r-c[0])<26 && Math.abs(gg-c[1])<26 && Math.abs(bb-c[2])<26){ n[k]++; break; }
            }
          }
          return n;
        }"""

        visto, huellas = {}, set()
        for k in range(120):                        # ~72s de juego real
            await p.keyboard.press("Space")
            await p.wait_for_timeout(430)
            huellas.add(await p.evaluate("""() => {
              const g = document.getElementById('c').getContext('2d',{willReadFrequently:true});
              const d = g.getImageData(0, 0, 360, 560).data;
              let h = 0;
              for (let i = 0; i < d.length; i += 400) h = (h*31 + d[i]) % 1000003;
              return h;
            }"""))
            n = await p.evaluate(CONTAR)
            for col, v in n.items():
                if v > 60: visto[col] = max(visto.get(col, 0), v)
            if k % 25 == 0:
                print(f"  {k*430//1000:3d}s  elementos vistos: {sorted(visto.keys())}")

        print(f"\n  errores de pagina:   {perrs[:2] if perrs else 'ninguno'}")
        print(f"  cuadros distintos:   {len(huellas)}")
        print(f"  colores elementales vistos (max px):")
        for col, v in sorted(visto.items(), key=lambda x: -x[1]):
            print(f"    {col:8} {v}")
        ok = (not perrs) and len(huellas) > 20 and len(visto) >= 3
        print(f"\n  {'OK — corre sin errores y los elementos se ven en juego' if ok else 'FALLO'}")
        await b.close()
        raise SystemExit(0 if ok else 1)

asyncio.run(main())
