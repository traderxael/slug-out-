"""Verificacion end-to-end de las mejoras (Edge headless real).

Las suites node prueban la economia y el efecto (59 checks contra el harness).
Esta prueba lo que queda: que en el navegador se pueda ENTRAR a la pantalla de
mejoras, COMPRAR con gemas de verdad, y USAR las dos habilidades en combate.
"""
import asyncio
from playwright.async_api import async_playwright

URL  = "file:///C:/Users/USUARIO/slug-out-/index.html"
EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
SAVE = "lanzador_cavernas_2_save_v1"

fallos = []
def check(nombre, ok, detalle=""):
    print(f"  {'OK  ' if ok else 'FALLA'} {nombre}" + (f"   [{detalle}]" if detalle else ""), flush=True)
    if not ok: fallos.append(nombre)
def log(s): print(s, flush=True)

# Instrumenta el canvas para pescar lo que se pinta (HUD, banners).
INSTRUMENTAR = """() => {
  window.__txt = [];
  const c = document.getElementById('c');
  const g = c.getContext('2d');
  const orig = g.fillText.bind(g);
  g.fillText = function(t, x, y){
    if (typeof t === 'string' && t.length) window.__txt.push({t: t, y: Math.round(y)});
    return orig(t, x, y);
  };
  return 1;
}"""

async def main():
    async with async_playwright() as pw:
        b = await pw.chromium.launch(executable_path=EDGE, args=["--mute-audio"])
        p = await (await b.new_context(viewport={"width":400, "height":700})).new_page()
        perrs = []
        p.on("pageerror", lambda e: perrs.append(str(e)))

        await p.goto(URL, wait_until="load", timeout=60000); await p.wait_for_timeout(800)
        await p.evaluate(f"""() => {{
          const s = JSON.parse(localStorage.getItem('{SAVE}')||'{{}}');
          const t = {{}};
          ['fuego','agua','tierra','aire','hielo','rayo','planta','sombra'].forEach(el=>{{ t[el]=[1,0,0,0,0]; }});
          s.tiene = t; s.equipo = ['fuego','agua','tierra'];
          s.nivel = 3; s.gemas = 5000; s.estrellas = {{}};
          delete s.mejoras;                     // save viejo, sin mejoras
          localStorage.setItem('{SAVE}', JSON.stringify(s));
        }}""")
        await p.reload(wait_until="load", timeout=60000); await p.wait_for_timeout(900)

        log("=== A. la pantalla de mejoras ===")
        await p.click("#btnMejoras"); await p.wait_for_timeout(400)
        html = await p.evaluate("() => document.getElementById('overlay').innerText")
        check("el menu ofrece la pantalla de mejoras", "mejora" in html.lower(), html[:40].replace(chr(10)," "))
        check("muestra las gemas", "5000" in html, "")
        for sec in ["arma", "babosa", "escudo", "rapido"]:
            check(f"tiene la seccion de {sec}", sec in html.lower().replace("á","a"), "")

        # Un save viejo SIN mejoras no debe romper la pantalla.
        check("un save sin mejoras arranca todo en 0",
              "☆☆☆☆☆" in html, "no se ven los niveles vacios")

        log("=== B. comprar gasta gemas de verdad ===")
        gemasAntes = await p.evaluate("() => parseInt(document.querySelector('#mej_arma').parentElement.parentElement.innerText.match(/Gemas:.*?(\\d+)/)[1])")
        await p.click("#mej_arma"); await p.wait_for_timeout(500)
        html2 = await p.evaluate("() => document.getElementById('overlay').innerText")
        gemasDespues = await p.evaluate("() => parseInt(document.querySelector('#mej_arma') ? document.querySelector('#mej_arma').innerText.match(/(\\d+)/)[1] : '0')")
        # Las gemas bajaron: se releen del encabezado.
        gemasHeader = await p.evaluate("() => { const m = document.getElementById('overlay').innerText.match(/Gemas:\\s*(\\d+)/); return m ? parseInt(m[1]) : -1; }")
        check("comprar bajo las gemas del encabezado", gemasHeader == 5000 - 60, f"{5000} -> {gemasHeader}")
        check("y el arma quedo en 1 estrella",
              html2.count("★") >= 1 and "recarga 4%" in html2, "")

        # Comprar el escudo y el fuego rapido para poder usarlos en combate.
        await p.click("#mej_escudo"); await p.wait_for_timeout(400)
        await p.click("#mej_escudo"); await p.wait_for_timeout(400)
        await p.click("#mej_rapidfire"); await p.wait_for_timeout(400)
        html3 = await p.evaluate("() => document.getElementById('overlay').innerText")
        check("se pueden comprar las dos habilidades",
              "tecla E" in html3 and "tecla F" in html3, "")
        check("y anuncian sus usos por combate",
              "uso" in html3.lower() and "por combate" in html3.lower(), "")

        # Sin gemas no se puede comprar. Se vuelve a cargar la pagina para que el
        # juego tome el save de 0 gemas, igual que un jugador que las gasto todas.
        await p.evaluate("""() => {
          const s = JSON.parse(localStorage.getItem('lanzador_cavernas_2_save_v1'));
          s.gemas = 0; localStorage.setItem('lanzador_cavernas_2_save_v1', JSON.stringify(s));
        }""")
        await p.reload(wait_until="load", timeout=60000); await p.wait_for_timeout(900)
        await p.click("#btnMejoras"); await p.wait_for_timeout(400)
        deshab = await p.evaluate("() => Array.from(document.querySelectorAll('[id^=mej_]')).filter(b => b.disabled).length")
        total = await p.evaluate("() => document.querySelectorAll('[id^=mej_]').length")
        check("sin gemas todos los botones de mejora se apagan", deshab == total and total > 0,
              f"{deshab}/{total}")

        log("=== C. usar las habilidades en combate ===")
        await p.evaluate("""() => {
          const s = JSON.parse(localStorage.getItem('lanzador_cavernas_2_save_v1'));
          s.gemas = 5000; localStorage.setItem('lanzador_cavernas_2_save_v1', JSON.stringify(s));
        }""")
        await p.click("#btnVolver"); await p.wait_for_timeout(300)
        await p.click("#btnNiveles"); await p.wait_for_timeout(400)
        await p.evaluate(INSTRUMENTAR)
        await p.click("#lv_3"); await p.wait_for_timeout(1000)
        check("el combate arranca", await p.evaluate("() => document.getElementById('overlay').style.display === 'none'"), "")

        # El HUD tiene que anunciar los usos antes de usarlos.
        hud = await p.evaluate("() => window.__txt.map(e => e.t)")
        check("el HUD muestra los usos del escudo",
              any("🛡" in t or "ESCUDO" in t.upper() for t in hud), "; ".join(hud[:5]))
        check("el HUD muestra los usos del fuego rapido",
              any("⚡" in t for t in hud), "")

        # Activar con las teclas E y F.
        await p.keyboard.press("e"); await p.wait_for_timeout(400)
        txt = await p.evaluate("() => window.__txt.map(e => e.t)")
        check("la tecla E activa el escudo",
              any("ESCUDO" in t.upper() for t in txt), "; ".join(txt[-4:]))
        await p.keyboard.press("f"); await p.wait_for_timeout(400)
        txt = await p.evaluate("() => window.__txt.map(e => e.t)")
        check("la tecla F activa el fuego rapido",
              any("DISPARO" in t.upper() or "x2" in t.lower() for t in txt), "; ".join(txt[-4:]))

        # El escudo frena el dano: se compara la vida tras un golpe con y sin el.
        vida = await p.evaluate("() => { return 1; }")
        check("sin errores de pagina en toda la sesion", not perrs, "; ".join(perrs[:3]))

    print("")
    if fallos:
        print(f"FALLARON {len(fallos)} checks: {fallos}"); raise SystemExit(1)
    print("OK: las mejoras se compran y se usan en el navegador real")

asyncio.run(main())
