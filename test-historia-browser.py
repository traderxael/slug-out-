"""
Verificacion end-to-end de la mini historia (Edge headless real).

Las suites node prueban la mecanica y el flujo con clics simulados. Esta prueba
lo que queda: que GANANDO de verdad en el navegador aparezca la mini historia,
que se pueda leer, y que "Continuar" arranque el nivel siguiente. La historia es
HTML, asi que se verifica el texto visible, no pixeles.
"""
import asyncio
from playwright.async_api import async_playwright

URL  = "file:///C:/Users/USUARIO/slug-out-/index.html"
EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
SAVE = "lanzador_cavernas_2_save_v1"

def log(s): print(s, flush=True)

async def main():
    async with async_playwright() as pw:
        b = await pw.chromium.launch(executable_path=EDGE, args=["--mute-audio"])
        p = await (await b.new_context(viewport={"width": 400, "height": 700})).new_page()
        perrs = []
        p.on("pageerror", lambda e: perrs.append(str(e)))

        await p.goto(URL, wait_until="load", timeout=60000); await p.wait_for_timeout(800)
        await p.evaluate(f"""() => {{
          const s = JSON.parse(localStorage.getItem('{SAVE}')||'{{}}');
          s.nivel = 1; localStorage.setItem('{SAVE}', JSON.stringify(s));
        }}""")
        await p.reload(wait_until="load", timeout=60000); await p.wait_for_timeout(800)
        await p.click("#btnNiveles"); await p.wait_for_timeout(300)
        await p.click("#lv_1");      await p.wait_for_timeout(600)

        # El rival oscila entre x=50 y x=310: cruza el centro (x=180), que es
        # justo donde caen los tiros rectos. A potencia maxima pega mas por tiro,
        # asi que necesita menos impactos. Barriendo el puntero se falla mas: el
        # angulo se pasa de largo y el tiro ni siquiera llega al rival.
        await p.keyboard.down("ArrowUp")
        await p.wait_for_timeout(950)          # sube la potencia al maximo (1.6)
        await p.keyboard.up("ArrowUp")
        log("potencia al maximo; disparando recto mientras el rival cruza el centro:")
        victoria = False
        for i in range(60):                    # ~53s de juego real
            await p.keyboard.press("Space")
            await p.wait_for_timeout(880)      # cooldown 812ms a potencia maxima
            est = await p.evaluate("""() => {
              const o = document.getElementById('overlay');
              return {vis:o.style.display, txt:(o.innerText||'').slice(0,80)};
            }""")
            if 'superado' in est['txt']:
                victoria = True; log(f"  ganado en ~{(i+1)*880//1000}s"); break
            if 'alcanzaron' in est['txt'] or est['vis'] == 'flex':
                log(f"  el combate termino sin victoria: {est['txt']!r}"); break
            if i % 12 == 0:
                log(f"  {(i+1)*880//1000:3d}s  sigue el combate")

        ok = True
        if not victoria:
            ok = False
            log("  FALLO: no gane el nivel 1 (no puedo probar la historia sin ganar)")
        else:
            # ---- 1. la mini historia aparece tras ganar ----
            await p.evaluate("""() => {
              const bs=[...document.querySelectorAll('#overlay button')];
              const b=bs.find(x=>x.textContent.includes('Siguiente nivel'));
              if(b) b.click();
            }""")
            await p.wait_for_timeout(400)
            titulo = await p.evaluate("() => {const h=document.querySelector('.histTit'); return h?h.textContent.trim():'';}")
            prosa  = await p.evaluate("() => {const h=document.querySelector('.histP');  return h?h.textContent.trim():'';}")
            vis    = await p.evaluate("() => document.getElementById('overlay').style.display")
            log(f"  titulo: {titulo!r}")
            log(f"  prosa ({len(prosa)} chars): {prosa[:72]}...")
            if not titulo or len(prosa) < 60 or vis != 'flex':
                ok = False; log("  FALLO: la historia no se mostro bien")
            else:
                log("  OK: la historia se muestra tras ganar el combate")

            # ---- 2. "Continuar" arranca el nivel siguiente ----
            await p.evaluate("""() => {
              const bs=[...document.querySelectorAll('#overlay button')];
              const b=bs.find(x=>x.textContent.includes('Continuar'));
              if(b) b.click();
            }""")
            await p.wait_for_timeout(400)
            vis2 = await p.evaluate("() => document.getElementById('overlay').style.display")
            if vis2 == 'none':
                log("  OK: 'Continuar' arranca el nivel siguiente")
            else:
                ok = False; log(f"  FALLO: tras Continuar el overlay sigue visible ({vis2})")

        log(f"\n  errores de pagina: {perrs[:2] if perrs else 'ninguno'}")
        if perrs: ok = False
        log(f"  {'OK — la campana cuenta su historia entre combates' if ok else 'FALLO'}")
        await b.close()
        raise SystemExit(0 if ok else 1)

asyncio.run(main())
