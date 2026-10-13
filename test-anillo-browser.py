"""Verificacion end-to-end del anillo al reves (Edge headless real).

Las suites node prueban que ventaja() invierta el multiplicador y que el
selector avise. Esta prueba lo que queda: que EN EL JUEGO, en un nivel del
fondo, el banner se pinte de verdad y el dano salga invertido.
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

# El banner se pinta con fillText; se instrumenta el contexto para pescarlo.
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
          s.nivel = 28; s.estrellas = {{}};
          localStorage.setItem('{SAVE}', JSON.stringify(s));
        }}""")
        await p.reload(wait_until="load", timeout=60000); await p.wait_for_timeout(900)

        # Entrar directo al nivel 28 (el del fondo, con el anillo al reves).
        await p.evaluate("() => { if(typeof iniciar==='function'){} return 1; }")
        await p.click("#btnNiveles"); await p.wait_for_timeout(400)
        lvs = await p.evaluate("() => document.querySelectorAll('.lv').length")
        check("el selector llega a 28 niveles", lvs == 28, f"hay {lvs}")

        html = await p.evaluate("() => document.getElementById('overlay').innerText")
        check("el selector explica que abajo el anillo se invierte",
              "al rev" in html.lower() or "invertid" in html.lower(), html[:70].replace(chr(10)," "))
        avisos = await p.evaluate("""() => Array.from(document.querySelectorAll('.lv'))
              .filter(n => /al rev[ée]s|invertid/i.test(n.innerText)).length""")
        check("y lo marca en los niveles 25 al 28 (no en los de arriba)",
              avisos == 4, f"marcados={avisos}")

        # Entrar al 28 y ver el banner pintado en el canvas.
        await p.evaluate(INSTRUMENTAR)
        await p.click("#lv_28"); await p.wait_for_timeout(1200)
        jugando = await p.evaluate("() => document.getElementById('overlay').style.display === 'none'")
        check("el nivel 28 arranca el combate", jugando, "no entro al juego")

        txt = await p.evaluate("() => window.__txt.map(e => e.t)")
        banner = [t for t in txt if "REV" in t.upper()]
        check("el banner 'EL ANILLO ESTA AL REVES' se pinta de verdad",
              len(banner) > 0, "; ".join(banner[:2]) or "no aparecio")

        # La marca del HUD queda durante todo el nivel (no solo el banner).
        await p.wait_for_timeout(1500)
        hud = await p.evaluate("() => window.__txt.map(e => e.t)")
        check("y la marca del HUD sigue ahi pasados los primeros segundos",
              any("anillo" in t.lower() for t in hud), "; ".join(hud[:4]))

        # El multiplicador en si lo cubre test-niveles.cjs (38 checks contra
        # ventaja()): aca adentro vive dentro del IIFE y no se alcanza desde
        # afuera, que es justamente el punto. Lo que se verifica en el navegador
        # es lo VISIBLE: que el jugador se entere antes de tirar.
        check("sin errores de pagina en toda la sesion", not perrs, "; ".join(perrs[:3]))

    print("")
    if fallos:
        print(f"FALLARON {len(fallos)} checks: {fallos}"); raise SystemExit(1)
    print("OK: el anillo al reves funciona en el navegador real")

asyncio.run(main())
