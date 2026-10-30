from pathlib import Path
"""
Verificacion end-to-end del nivel 1 como tutorial (Edge headless real).

Las suites node prueban la mecanica con el DOM stubeado. Esta prueba lo que
queda: que en el NAVEGADOR REAL el nivel 1 no tenga rocas (se las ve caer), que
se avise a si mismo que es el tutorial, y que las rocas vuelvan en el nivel 2.
El tutorial no puede ser solo "un nivel mas facil": si las rocas no vuelven
despues, el juego perdio una mecanica.

El nivel 1 se gana de verdad (son 3 rivales sin habilidades) para comprobar que
el flujo de victoria -> fragmento -> nivel siguiente sigue entero.
"""
import asyncio
from playwright.async_api import async_playwright

URL = Path(__file__).resolve().with_name("index.html").as_uri()
EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
SAVE = "lanzador_cavernas_2_save_v1"

# '#a08b6d' solo lo usan las rocas y sus particulas: ningun elemento del juego
# tiene ese color, asi que es un detector limpio de "cayo una roca".
ROCA = (160, 139, 109)

# El banner del aviso se dibuja en '#ff9ad5' (rosa) en la banda y=142..158.
# Durante el combate el rosa no aparece en ningun otro lado.
BANNER = "r>230 && gg>120 && gg<195 && bb>190"

def log(s): print(s, flush=True)

async def main():
    async with async_playwright() as pw:
        b = await pw.chromium.launch(executable_path=EDGE, args=["--mute-audio"])
        p = await (await b.new_context(viewport={"width": 400, "height": 700})).new_page()
        perrs = []
        p.on("pageerror", lambda e: perrs.append(str(e)))

        await p.goto(URL, wait_until="load", timeout=60000); await p.wait_for_timeout(800)
        await p.evaluate(f"""() => {{ localStorage.removeItem('{SAVE}'); }}""")
        await p.reload(wait_until="load", timeout=60000); await p.wait_for_timeout(800)

        # --- entrar al nivel 1 ---
        await p.click("#btnNiveles"); await p.wait_for_timeout(300)
        await p.click("#lv_1");      await p.wait_for_timeout(400)

        # --- 1. en el nivel 1: sin rocas y con el aviso del tutorial ---
        async def pixeles():
            return await p.evaluate("""() => {
              const g = document.getElementById('c').getContext('2d');
              const d = g.getImageData(0,0,360,560).data;
              let rocas = 0, banner = 0;
              for (let y=0; y<560; y++){
                for (let x=0; x<360; x++){
                  const i = (y*360+x)*4, r = d[i], gg = d[i+1], bb = d[i+2];
                  if (r===160 && gg===139 && bb===109) rocas++;
                  if (y>=142 && y<=158 && x>=40 && x<=320 && %s) banner++;
                }
              }
              return { rocas: rocas, banner: banner };
            }""" % BANNER)

        max_rocas, max_banner = 0, 0
        for i in range(45):                      # ~18s de combate
            px = await pixeles()
            max_rocas = max(max_rocas, px["rocas"])
            max_banner = max(max_banner, px["banner"])
            if i < 8 and i % 2 == 0:
                log(f"  {(i+1)*0.4:4.1f}s  rocas:{px['rocas']:5d}  banner:{px['banner']:4d}")
            await p.wait_for_timeout(400)

        ok = True
        log(f"  maximo de pixeles de roca en el nivel 1: {max_rocas}")
        log(f"  maximo de pixeles del banner del tutorial: {max_banner}")
        if max_rocas > 0:
            ok = False; log("  FALLO: en el nivel 1 cayo una roca (no es un tutorial)")
        else:
            log("  OK: en el nivel 1 no cae ninguna roca")
        if max_banner <= 0:
            ok = False; log("  FALLO: el nivel 1 no se aviso a si mismo")
        else:
            log("  OK: el nivel 1 se avisa (banner del tutorial)")

        # --- 2. ganar el nivel 1 de verdad: el flujo sigue entero ---
        await p.keyboard.down("ArrowUp")
        await p.wait_for_timeout(950)
        await p.keyboard.up("ArrowUp")
        victoria = False
        for i in range(60):
            await p.keyboard.press("Space")
            await p.wait_for_timeout(880)
            est = await p.evaluate("""() => {
              const o = document.getElementById('overlay');
              return { vis:o.style.display, txt:(o.innerText||'').slice(0,80) };
            }""")
            if 'superado' in est['txt']:
                victoria = True; log(f"  nivel 1 ganado en ~{(i+1)*880//1000}s"); break
            if 'alcanzaron' in est['txt'] or est['vis'] == 'flex':
                log(f"  el combate termino sin victoria: {est['txt']!r}"); break

        if not victoria:
            ok = False; log("  FALLO: no se gano el nivel 1")
        else:
            await p.evaluate("""() => {
              const bs=[...document.querySelectorAll('#overlay button')];
              const b=bs.find(x=>x.textContent.includes('Siguiente nivel'));
              if(b) b.click();
            }""")
            await p.wait_for_timeout(400)
            titulo = await p.evaluate("() => {const h=document.querySelector('.histTit'); return h?h.textContent.trim():'';}")
            if not titulo:
                ok = False; log("  FALLO: no aparecio el fragmento del nivel 1")
            else:
                log(f"  OK: el fragmento del nivel 1 aparecio ({titulo!r})")
            await p.evaluate("""() => {
              const bs=[...document.querySelectorAll('#overlay button')];
              const b=bs.find(x=>x.textContent.includes('Continuar'));
              if(b) b.click();
            }""")
            await p.wait_for_timeout(600)

            # --- 3. en el nivel 2 las rocas vuelven ---
            max_rocas2 = 0
            for i in range(32):                  # ~13s: la primera roca cae a los ~5s
                px = await pixeles()
                max_rocas2 = max(max_rocas2, px["rocas"])
                if i == 14:
                    log(f"  pixeles de roca en el nivel 2: {max_rocas2}")
                await p.wait_for_timeout(400)
            log(f"  maximo de pixeles de roca en el nivel 2: {max_rocas2}")
            if max_rocas2 <= 0:
                ok = False; log("  FALLO: en el nivel 2 no cae ninguna roca (el tutorial se comio la mecanica)")
            else:
                log("  OK: en el nivel 2 las rocas vuelven")

        log(f"\n  errores de pagina: {perrs[:2] if perrs else 'ninguno'}")
        if perrs: ok = False
        log(f"  {'OK - el nivel 1 es un tutorial de verdad' if ok else 'FALLO'}")
        await b.close()
        raise SystemExit(0 if ok else 1)

asyncio.run(main())
