from pathlib import Path
"""Verificacion end-to-end del selector de babosas (Edge headless real).

Las suites node prueban la mecanica (equipoActual, toggleEquipo, normalizeSave).
Esta prueba lo que queda: que en el navegador de verdad se pueda ABRIR el
selector, marcar y desmarcar con el mouse, que el contador y los bordes
acompañen, que el equipo elegido quede guardado, y que el combate arranque con
esas 3 y no con las 8 de la mochila.

Fase A trabaja sobre el DOM (la pantalla es HTML). Fase B instrumenta
ctx.fillText, porque el selector de combate se dibuja en el canvas: las babosas
del equipo se pintan con su nombre en y=548.
"""
import asyncio
from playwright.async_api import async_playwright

URL = Path(__file__).resolve().with_name("index.html").as_uri()
EDGE = r"C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe"
SAVE = "lanzador_cavernas_2_save_v1"

# Las 8 a rareza Comun (la unica que arranca desbloqueada).
EQUIPO_INICIAL = ["rayo", "planta", "sombra"]
# Nombre que pinta el selector para cada elemento a rareza Comun.
NOMBRE = {"fuego":"Ascua","agua":"Gotita","tierra":"Guijarro","aire":"Brisa",
          "hielo":"Copo","rayo":"Zumbido","planta":"Brote","sombra":"Mota"}

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

        # Un save con las 8 babosas y un equipo de 3 ya elegido.
        await p.goto(URL, wait_until="load", timeout=60000); await p.wait_for_timeout(800)
        await p.evaluate(f"""() => {{
          const s = JSON.parse(localStorage.getItem('{SAVE}')||'{{}}');
          const todo = {{}};
          ['fuego','agua','tierra','aire','hielo','rayo','planta','sombra'].forEach(el=>{{ todo[el]=[1,0,0,0,0]; }});
          s.tiene = todo; s.equipo = {EQUIPO_INICIAL!r}; s.nivel = 1;
          localStorage.setItem('{SAVE}', JSON.stringify(s));
        }}""")
        await p.reload(wait_until="load", timeout=60000); await p.wait_for_timeout(900)

        # ---------- Fase A: la pantalla del selector, con clics reales ----------
        log("=== A. la pantalla del selector responde al mouse ===")
        await p.click("#btnEquipo"); await p.wait_for_timeout(500)
        ov = await p.evaluate("() => ({vis:document.getElementById('overlay').style.display,"
                              "txt:document.getElementById('overlay').innerText||''})")
        check("el boton Equipo abre el selector", ov["vis"] == "flex", f"display={ov['vis']}")
        check("el selector se anuncia como equipo de combate",
              "Equipo de combate" in ov["txt"], ov["txt"][:60].replace(chr(10)," "))
        check("el contador arranca en 3/3", "3/3" in ov["txt"], ov["txt"][:80].replace(chr(10)," "))

        # Las marcadas llevan el borde de su color; las que no, borde transparente.
        celdas = await p.evaluate("""() => Array.from(document.querySelectorAll('[data-el]')).map(n => ({
            el: n.getAttribute('data-el'),
            marcada: !n.getAttribute('style').includes('transparent'),
            opaca: n.getAttribute('style').includes('opacity:0.45')
        }))""")
        marcadas = [c["el"] for c in celdas if c["marcada"]]
        check("estan marcadas las 3 del equipo y ninguna mas",
              marcadas == EQUIPO_INICIAL, f"marcadas={marcadas}")
        check("se muestran las 8 babosas de la mochila", len(celdas) == 8, f"hay {len(celdas)}")
        check("con el equipo lleno las desmarcadas se apagan",
              all(c["opaca"] for c in celdas if not c["marcada"]),
              f"apagadas={sum(1 for c in celdas if c['opaca'])}/5")

        # Desmarcar una: libera lugar y el contador baja.
        await p.click('[data-el="rayo"]'); await p.wait_for_timeout(400)
        txt = await p.evaluate("() => document.getElementById('overlay').innerText||''")
        check("desmarcar una babosa baja el contador a 2/3", "2/3" in txt, txt[:60].replace(chr(10)," "))
        celdas = await p.evaluate("() => Array.from(document.querySelectorAll('[data-el]')).map(n =>"
                                  "({el:n.getAttribute('data-el'), opaca:n.getAttribute('style').includes('opacity:0.45')}))")
        check("y con lugar libre las otras se vuelven a encender",
              not any(c["opaca"] for c in celdas), "siguen apagadas")

        # Marcar otra: ocupa el lugar que se libero.
        await p.click('[data-el="fuego"]'); await p.wait_for_timeout(400)
        txt = await p.evaluate("() => document.getElementById('overlay').innerText||''")
        check("marcar otra babosa sube el contador a 3/3", "3/3" in txt, txt[:60].replace(chr(10)," "))
        guardado = await p.evaluate(f"() => JSON.parse(localStorage.getItem('{SAVE}')||'{{}}').equipo")
        check("el equipo elegido queda guardado",
              guardado == ["planta","sombra","fuego"], f"guardado={guardado}")

        await p.click("#btnVolver"); await p.wait_for_timeout(400)

        # ---------- Fase B: el combate entra con las 3 elegidas ----------
        log("=== B. el combate arranca con las 3 del equipo ===")
        # El selector de combate es canvas: se instrumenta fillText y se mira lo
        # que pinta en y=548 (la linea de nombres de abajo).
        await p.evaluate("""() => {
          window.__sel = [];
          const orig = CanvasRenderingContext2D.prototype.fillText;
          CanvasRenderingContext2D.prototype.fillText = function(t, x, y){
            if (y > 535 && y < 565) window.__sel.push(String(t));
            return orig.apply(this, arguments);
          };
        }""")
        await p.click("#btnNiveles"); await p.wait_for_timeout(400)
        await p.click("#lv_1");     await p.wait_for_timeout(1200)

        nombres = await p.evaluate("() => Array.from(new Set(window.__sel))")
        check("el selector de combate muestra 3 babosas", len(nombres) == 3,
              f"muestra {len(nombres)}: {nombres}")
        check("y son justo las del equipo elegido",
              set(nombres) == {NOMBRE["planta"], NOMBRE["sombra"], NOMBRE["fuego"]},
              f"nombres={nombres}")
        fuera = [n for n in nombres if n in {NOMBRE[e] for e in ("agua","tierra","aire","hielo","rayo")}]
        check("ninguna babosa de la mochila se cuela al combate", not fuera, f"coladas={fuera}")

        check("sin errores de pagina en toda la sesion", not perrs, "; ".join(perrs[:3]))

    print("")
    if fallos:
        print(f"FALLARON {len(fallos)} checks: {fallos}"); raise SystemExit(1)
    print("OK: el selector de babosas funciona en el navegador real")

asyncio.run(main())
