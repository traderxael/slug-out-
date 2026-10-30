from pathlib import Path
"""
Verificacion end-to-end del epilogo (Edge headless real).

Las suites node prueban el flujo con clics simulados. Esta prueba lo que queda:
que el epilogo se VEA de verdad en el navegador, con su prosa y su boton, y que
se pueda volver a leer desde la "Ruta de la cueva" una vez viviste la verdad.

No se juega el nivel 12 entero (son 7 rivales y varios minutos): el epilogo se
abre desde la ruta, que es el mismo `mostrarEpilogo` que usa el avance de
nivel. El encadenamiento 12 -> 13 lo cubre la suite node.
"""
import asyncio
import sys
from playwright.async_api import async_playwright

URL = Path(__file__).resolve().with_name("index.html").as_uri()
EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
SAVE = "lanzador_cavernas_2_save_v1"
SHOT = Path(__file__).resolve().parent / "screenshots"

def log(s):
    # La consola de Windows es cp1252 y no tiene los emojis del menu: se
    # reemplazan en vez de romper la salida.
    enc = sys.stdout.encoding or 'utf-8'
    print(s.encode(enc, 'replace').decode(enc), flush=True)

async def main():
    async with async_playwright() as pw:
        b = await pw.chromium.launch(executable_path=EDGE, args=["--mute-audio"])
        p = await (await b.new_context(viewport={"width": 400, "height": 700})).new_page()
        perrs = []
        p.on("pageerror", lambda e: perrs.append(str(e)))

        await p.goto(URL, wait_until="load", timeout=60000); await p.wait_for_timeout(800)

        # --- la verdad ya vivida: el epilogo esta desbloqueado ---
        await p.evaluate(f"""() => {{
          const s = JSON.parse(localStorage.getItem('{SAVE}') || '{{}}');
          s.nivel = 13; s.estrellas = Object.assign({{}}, s.estrellas, {{12: 3}});
          localStorage.setItem('{SAVE}', JSON.stringify(s));
        }}""")
        await p.reload(wait_until="load", timeout=60000); await p.wait_for_timeout(800)

        ok = True
        # --- 1. el menu ofrece la ruta y ya cuenta el epilogo ---
        btn = await p.evaluate("""() => {
          const b = document.getElementById('btnHistoria');
          return b ? b.textContent.trim() : '';
        }""")
        log(f"  boton de la ruta: {btn!r}")
        if '29' not in btn:
            ok = False; log("  FALLO: la ruta no cuenta el epilogo (28 fragmentos + epilogo)")
        else:
            log("  OK: la ruta cuenta 28 fragmentos + el epilogo")

        # --- 2. la ruta muestra el epilogo ---
        await p.click("#btnHistoria"); await p.wait_for_timeout(400)
        entrada = await p.evaluate("""() => {
          const n = document.querySelector('[data-hist="epilogo"]');
          return n ? n.textContent.trim() : '';
        }""")
        log(f"  entrada en la ruta: {entrada!r}")
        if 'EP' not in entrada.upper():
            ok = False; log("  FALLO: la ruta no muestra el epilogo")
        else:
            log("  OK: la ruta muestra el epilogo")

        # --- 3. se abre y se lee ---
        await p.click('[data-hist="epilogo"]'); await p.wait_for_timeout(400)
        vis = await p.evaluate("() => document.getElementById('overlay').style.display")
        titulo = await p.evaluate("() => {const h=document.querySelector('.histTit'); return h?h.textContent.trim():'';}")
        prosa = await p.evaluate("() => {const h=document.querySelector('.histP');  return h?h.textContent.trim():'';}")
        tag = await p.evaluate("() => {const h=document.querySelector('.histTag'); return h?h.textContent.trim():'';}")
        log(f"  tag: {tag!r}   titulo: {titulo!r}")
        log(f"  prosa ({len(prosa)} chars): {prosa[:72]}...")
        if vis != 'flex' or not titulo or len(prosa) < 120:
            ok = False; log("  FALLO: el epilogo no se mostro bien")
        else:
            log("  OK: el epilogo se muestra con su prosa")

        SHOT.mkdir(exist_ok=True)
        await p.screenshot(path=str(SHOT / "slug-epilogo.png"))
        log(f"  captura: {SHOT / 'slug-epilogo.png'}")

        # --- 4. "Continuar" devuelve a la ruta (no al epilogo) ---
        await p.evaluate("""() => {
          const bs=[...document.querySelectorAll('#overlay button')];
          const b=bs.find(x=>x.textContent.includes('Continuar'));
          if(b) b.click();
        }""")
        await p.wait_for_timeout(400)
        vis2 = await p.evaluate("() => document.getElementById('overlay').style.display")
        tit2 = await p.evaluate("() => {const h=document.querySelector('.histTit'); return h?h.textContent.trim():'';}")
        lista = await p.evaluate("() => {const h=document.getElementById('histLista'); return !!h;}")
        if vis2 == 'flex' and lista and tit2 != titulo:
            log("  OK: 'Continuar' vuelve a la ruta de la cueva")
        else:
            ok = False; log(f"  FALLO: tras Continuar no se vuelve a la ruta (vis={vis2}, lista={lista})")

        log(f"\n  errores de pagina: {perrs[:2] if perrs else 'ninguno'}")
        if perrs: ok = False
        log(f"  {'OK - la campana cierra con un epilogo' if ok else 'FALLO'}")
        await b.close()
        raise SystemExit(0 if ok else 1)

asyncio.run(main())
