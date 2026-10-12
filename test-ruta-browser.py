"""Verificacion end-to-end de la ruta de historia (Edge headless real).

Las suites node prueban los datos (20 niveles, un fragmento por nivel, el arco
que cierra) y el armado del HTML. Esta prueba lo que queda: que en el navegador
se pueda ABRIR la ruta, que se vea solo lo ya vivido, que tocar un fragmento lo
relea, y que el selector de niveles llegue a 20.
"""
import asyncio
from playwright.async_api import async_playwright

URL  = "file:///C:/Users/USUARIO/slug-out-/index.html"
EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
SAVE = "lanzador_cavernas_2_save_v1"
TOTAL = 20

fallos = []
def check(nombre, ok, detalle=""):
    print(f"  {'OK  ' if ok else 'FALLA'} {nombre}" + (f"   [{detalle}]" if detalle else ""), flush=True)
    if not ok: fallos.append(nombre)
def log(s): print(s, flush=True)

async def main():
    async with async_playwright() as pw:
        b = await pw.chromium.launch(executable_path=EDGE, args=["--mute-audio"])
        p = await (await b.new_context(viewport={"width":400, "height":700})).new_page()
        perrs = []
        p.on("pageerror", lambda e: perrs.append(str(e)))

        # Un save que ya gano los 5 primeros niveles: 5 fragmentos vividos.
        await p.goto(URL, wait_until="load", timeout=60000); await p.wait_for_timeout(800)
        await p.evaluate(f"""() => {{
          const s = JSON.parse(localStorage.getItem('{SAVE}')||'{{}}');
          const t = {{}};
          ['fuego','agua','tierra','aire','hielo','rayo','planta','sombra'].forEach(el=>{{ t[el]=[1,0,0,0,0]; }});
          s.tiene = t; s.equipo = ['fuego','agua','tierra']; s.nivel = 6;
          s.estrellas = {{1:3, 2:2, 3:3, 4:1, 5:2}};
          localStorage.setItem('{SAVE}', JSON.stringify(s));
        }}""")
        await p.reload(wait_until="load", timeout=60000); await p.wait_for_timeout(900)

        ov = lambda: p.evaluate("() => document.getElementById('overlay').innerText||''")

        log("=== A. la ruta muestra lo ya vivido y tapa el resto ===")
        await p.click("#btnHistoria"); await p.wait_for_timeout(500)
        txt = await ov()
        check("el boton abre la ruta de la cueva", "ruta de la cueva" in txt.lower(), txt[:50].replace(chr(10)," "))
        check("el contador dice 5 de 20 vividos", "5 de 20" in txt, txt[:70].replace(chr(10)," "))

        # Los 5 vividos muestran su titulo; los 15 tapados muestran ???.
        titulos = await p.evaluate("""() => {
          const out = {vistos: [], tapados: 0, total: 0};
          document.querySelectorAll('[data-hist]').forEach(n => {
            out.total++;
            const tit = n.querySelectorAll('div')[1];
            if (tit && tit.innerText.trim() === '???') out.tapados++;
            else if (tit) out.vistos.push(tit.innerText.trim());
          });
          return out;
        }""")
        check("la ruta lista los 20 fragmentos", titulos["total"] == TOTAL, f"hay {titulos['total']}")
        check("5 estan vividos y 15 tapados",
              len(titulos["vistos"]) == 5 and titulos["tapados"] == 15,
              f"vistos={len(titulos['vistos'])}, tapados={titulos['tapados']}")

        # Los tapados estan visualmente apagados (opacidad), no son un hueco.
        opacos = await p.evaluate("""() => Array.from(document.querySelectorAll('[data-hist]'))
              .filter(n => n.getAttribute('style').includes('opacity')).length""")
        check("lo no jugado se ve apagado, no como hueco", opacos == 15, f"apagados={opacos}")

        log("=== B. tocar un fragmento lo vuelve a mostrar ===")
        await p.click('[data-hist="3"]'); await p.wait_for_timeout(500)
        txt = await ov()
        check("tocar un fragmento vivido lo relee",
              "NIVEL 3" in txt.upper() and ("No piden permiso" in txt),
              txt[:60].replace(chr(10)," "))
        check("el fragmento se puede saltar", "Saltar" in txt, "")
        await p.click("#btnSaltarHist"); await p.wait_for_timeout(500)
        txt = await ov()
        check("saltar devuelve a la ruta", "ruta de la cueva" in txt.lower(), txt[:40].replace(chr(10)," "))

        # Un fragmento tapado NO se puede abrir (no adelanta nada).
        await p.click('[data-hist="9"]'); await p.wait_for_timeout(400)
        txt = await ov()
        check("un fragmento no jugado no se puede abrir",
              "ruta de la cueva" in txt.lower(), txt[:40].replace(chr(10)," "))

        log("=== C. el selector de niveles llega a 20 ===")
        await p.click("#btnVolver"); await p.wait_for_timeout(400)
        await p.click("#btnNiveles"); await p.wait_for_timeout(500)
        lvs = await p.evaluate("() => document.querySelectorAll('.lv').length")
        check("hay 20 niveles en el selector", lvs == TOTAL, f"hay {lvs}")
        desbloq = await p.evaluate("""() => Array.from(document.querySelectorAll('.lv'))
              .filter(n => !n.disabled).length""")
        check("y estan desbloqueados hasta el 6 (donde llego el save)",
              desbloq == 6, f"desbloqueados={desbloq}")

        check("sin errores de pagina en toda la sesion", not perrs, "; ".join(perrs[:3]))

    print("")
    if fallos:
        print(f"FALLARON {len(fallos)} checks: {fallos}"); raise SystemExit(1)
    print("OK: la ruta de historia funciona en el navegador real")

asyncio.run(main())
