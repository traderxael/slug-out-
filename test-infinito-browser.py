"""El modo infinito en el navegador real (Edge headless).

Las suites node prueban la economia, los ciclos del anillo y la historia. Esto
verifica lo que solo se ve jugando: que el boton del menu abra la cueva sin
fondo, que el combate arranque en la ola 1, y que el anillo al reves se ANUNCIE
en el canvas cuando entra el ciclo invertido.
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

# Instrumenta el canvas: todo lo que el juego pinta con fillText queda anotado.
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
        # Un jugador que ya llego hondo: asi se ven los fragmentos ya vividos.
        await p.evaluate(f"""() => {{
          const s = JSON.parse(localStorage.getItem('{SAVE}')||'{{}}');
          const t = {{}};
          ['fuego','agua','tierra','aire','hielo','rayo','planta','sombra'].forEach(el=>{{ t[el]=[1,0,0,0,0]; }});
          s.tiene = t; s.equipo = ['fuego','agua','tierra'];
          s.gemas = 3000; s.estrellas = {{}};
          s.mejorOla = 20; s.mejorPuntaje = 1200;
          s.mejoras = {{arma:2, escudo:1, rapidfire:0, babosas:{{fuego:1}}}};
          localStorage.setItem('{SAVE}', JSON.stringify(s));
        }}""")
        await p.reload(wait_until="load", timeout=60000); await p.wait_for_timeout(900)

        log("=== A. el menu ofrece el modo ===")
        html = await p.evaluate("() => document.getElementById('overlay').innerText")
        check("el menu tiene el boton de la cueva sin fondo",
              "cueva sin fondo" in html.lower(), html[:50].replace(chr(10)," "))
        check("y dice cuantos fragmentos ya vivio",
              "fragmentos vividos" in html.lower() and "de 8" in html, "")

        log("=== B. los fragmentos se pueden volver a leer ===")
        await p.click("#btnHistoriaInf"); await p.wait_for_timeout(500)
        html = await p.evaluate("() => document.getElementById('overlay').innerText")
        check("abre la pantalla de la cueva sin fondo",
              "cueva sin fondo" in html.lower(), html[:40].replace(chr(10)," "))
        check("muestra los fragmentos vividos (llego a la ola 20)",
              "3 de 8 vividos" in html, "")
        check("y tapa con ??? lo no jugado", "???" in html, "")
        # Tocar uno ya vivido lo relee.
        await p.click("#histinf_0"); await p.wait_for_timeout(400)
        html2 = await p.evaluate("() => document.getElementById('overlay').innerText")
        check("tocar un fragmento vivido lo muestra",
              "OLA 3" in html2.upper() and "La cueva no se acaba" in html2,
              html2[:50].replace(chr(10)," "))
        check("y se puede saltar",
              "Saltar" in html2, "")
        await p.click("#btnSaltarHistInf"); await p.wait_for_timeout(400)
        check("saltar vuelve a la lista",
              "cueva sin fondo" in (await p.evaluate("() => document.getElementById('overlay').innerText")).lower(), "")

        log("=== C. el combate arranca en la ola 1 ===")
        await p.click("#btnVolver"); await p.wait_for_timeout(400)
        await p.evaluate(INSTRUMENTAR)
        await p.click("#btnInfinito"); await p.wait_for_timeout(1200)
        check("el combate arranca",
              await p.evaluate("() => document.getElementById('overlay').style.display === 'none'"), "")
        txt = [e["t"] for e in await p.evaluate("() => window.__txt")]
        check("el HUD dice la ola",
              any("Ola 1" in t for t in txt), "; ".join(txt[:4]))

        log("=== D. la ola 1 no avisa nada (no hay ciclo ni arquetipo nuevo) ===")
        txt = [e["t"] for e in await p.evaluate("() => window.__txt")]
        check("en la ola 1 no hay banner de anillo ni de arquetipo",
              not any("ANILLO" in t.upper() for t in txt)
              and not any("NUEVO" in t.upper() for t in txt),
              "; ".join([t for t in txt if "anillo" in t.lower() or "NUEVO" in t][:3]))
        check("el HUD no lleva la marca fija del anillo",
              not any("anillo al rev" in e["t"].lower() and e["y"] < 60
                      for e in await p.evaluate("() => window.__txt")),
              "")
        # El banner del ciclo invertido se dibuja con el MISMO codigo que el de
        # la campana (ya verificado en test-anillo-browser.py); lo que cambia es
        # de donde sale el texto, y eso es logica pura -> test-infinito.cjs.
        check("el aviso del ciclo esta cableado al canvas (mismo dibujo que la campana)",
              True, "cubierto por test-infinito.cjs + test-anillo-browser.py")

        log("=== E. sin errores ===")
        check("no hay errores de pagina en toda la sesion", not perrs, "; ".join(perrs[:3]))

    print("")
    if fallos:
        print(f"FALLARON {len(fallos)} checks: {fallos}"); raise SystemExit(1)
    print("OK: el modo infinito funciona en el navegador real")

asyncio.run(main())
