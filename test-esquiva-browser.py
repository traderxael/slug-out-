"""
Verificacion end-to-end de la esquiva determinista (Edge headless).

Las suites node prueban que la reaccion sea determinista y que el tradeoff de
potencia se sostenga por calculo. Esta prueba cubre lo que ninguna puede: que el
tell se DIBUJE de verdad jugando, en un navegador real, sin errores.

En vez de contar pixeles (el rival de fuego tambien es naranja y contaminaba el
conteo) se instrumenta `ctx.ellipse`: el tell es la UNICA elipse que se dibuja en
la franja de arriba, con el trazo rgba(255,120,90,...). Se registra cada llamada
con su color y su centro, y se cuenta solo la que coincide.

Se juega el nivel 6, el primero donde la evasion (0.24) alcanza para que la
potencia MINIMA ya no baste: el rival ve venir el tiro, avisa, y esquiva.
"""

import asyncio
from playwright.async_api import async_playwright

URL  = "file:///C:/Users/USUARIO/slug-out-/index.html"
EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
SAVE = "lanzador_cavernas_2_save_v1"

# Envolvemos ellipse para anotar cada trazo: el tell es el unico de arriba con
# este naranja. El charco de fuego usa ellipse pero esta abajo, en los pies del
# jugador, asi que el filtro por altura lo descarta.
#
# Ademas envolvemos fillText para cazar el "¡Esquivó!": esa particula solo sale
# cuando la esquiva COMPLETO (el viento se agoto sin que ningun tiro la corte),
# que es la prueba de que la reaccion entera funciona, no solo el dibujo.
INSTRUMENTAR = """() => {
  window.__tell = [];
  window.__esquivo = [];
  const origE = CanvasRenderingContext2D.prototype.ellipse;
  CanvasRenderingContext2D.prototype.ellipse = function(x, y, rx, ry, ...r){
    try {
      // El trazo sale como 'rgba(255, 120, 90, 0.35)' — CON espacios. Comparar
      // contra '255,120,90' a secas no matchea nunca y el test da 0 avisos
      // aunque el tell se este dibujando: hay que sacar los espacios antes.
      const c = String(this.strokeStyle).replace(/\\s/g, '');
      if (c.indexOf('255,120,90') >= 0 && y < 200)
        window.__tell.push(1);
    } catch(e){}
    return origE.call(this, x, y, rx, ry, ...r);
  };
  const origF = CanvasRenderingContext2D.prototype.fillText;
  CanvasRenderingContext2D.prototype.fillText = function(txt, x, y, ...r){
    try {
      // La particula sale SOBRE el rival (y~65). La etiqueta del HUD tambien
      // dice "esquiva" pero esta en (180,172): sin el filtro por altura el
      // test cuenta el HUD en vez de las esquivas reales.
      if (typeof txt === 'string' && txt.indexOf('squiv') >= 0 && y < 100)
        window.__esquivo.push({x: Math.round(x), y: Math.round(y)});
    } catch(e){}
    return origF.call(this, txt, x, y, ...r);
  };
}"""

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
          s.nivel = 6; localStorage.setItem('%s', JSON.stringify(s));
        }""" % (SAVE, SAVE))
        await p.reload(wait_until="load", timeout=60000); await p.wait_for_timeout(800)
        await p.click("#btnNiveles"); await p.wait_for_timeout(300)
        await p.click("#lv_6");      await p.wait_for_timeout(500)
        # Envolvemos ellipse recien ahora: alcanza con que este puesto antes del
        # primer cuadro que dibuje el tell, no antes de que cargue el juego.
        # (`add_init_script` no corre en file://, por eso se inyecta aca.)
        await p.evaluate(INSTRUMENTAR)

        print("nivel 6 en Edge real — tirando a potencia baja para que el tell complete:\n")

        # La potencia sube 0.014 por frame mientras se mantiene ArrowUp, asi que
        # 500ms da ~0.97 (el tiro LLEGA y corta la esquiva) y 250ms da ~0.76
        # (el tiro llega DESPUES: la esquiva completa). Con 200ms la potencia
        # queda en el minimo 0.55 y el tiro no alcanza a subir los 335px que
        # separan al jugador del rival.
        #
        # Cada fase REARRANCA el nivel: con 25 tiros el jugador se lo termina y
        # el juego sale de 'playing', con lo que Space deja de disparar y la
        # segunda fase mediria 0 sin que nada este roto.
        async def empezar():
            await p.goto(URL, wait_until="load", timeout=60000); await p.wait_for_timeout(600)
            await p.evaluate("""() => {
              const s = JSON.parse(localStorage.getItem('%s')||'{}');
              s.nivel = 6; localStorage.setItem('%s', JSON.stringify(s));
            }""" % (SAVE, SAVE))
            await p.reload(wait_until="load", timeout=60000); await p.wait_for_timeout(600)
            await p.click("#btnNiveles"); await p.wait_for_timeout(300)
            await p.click("#lv_6");      await p.wait_for_timeout(400)
            await p.evaluate(INSTRUMENTAR)

        async def tirar(hold, tiros):
            for k in range(tiros):
                await p.keyboard.down("ArrowUp"); await p.wait_for_timeout(hold)
                await p.keyboard.up("ArrowUp")
                await p.keyboard.press("Space")
                await p.wait_for_timeout(650)

        print("fase A — carga media (~0.97): el tiro corta la esquiva\n")
        await empezar()
        await tirar(500, 12)
        tellsA = await p.evaluate("() => window.__tell.length")
        print(f"  avisos dibujados: {tellsA}\n")

        print("fase B — carga baja (~0.76): el tiro llega tarde, la esquiva completa\n")
        await empezar()
        await tirar(250, 12)
        tellsB = await p.evaluate("() => window.__tell.length")
        esquivos = await p.evaluate("() => window.__esquivo.length")
        marcas   = await p.evaluate("() => window.__esquivo.slice(0, 4)")
        print(f"  avisos dibujados:     {tellsB}")
        print(f"  '¡Esquivó!' completadas: {esquivos}")
        for m in marcas:
            print(f"    texto en ({m['x']},{m['y']})")

        print(f"\n  errores de pagina:     {perrs[:2] if perrs else 'ninguno'}")

        # El tell se dibuja en cada cuadro mientras dura (200ms ~ 12 cuadros) y
        # "¡Esquivó!" es la prueba de que la reaccion termina entera: el viento
        # se agoto sin que ningun tiro la cortara.
        ok = (not perrs) and tellsA >= 10 and esquivos >= 3
        print(f"\n  {'OK — el tell se dibuja y la esquiva completa jugando, sin errores' if ok else 'FALLO'}")
        await b.close()
        raise SystemExit(0 if ok else 1)

asyncio.run(main())
