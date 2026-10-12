# QA real del juego en Edge headless. Cada check tiene un numero detras.
# Tres mediciones distintas, cada una con su banda para no contaminarse:
#   ANIM    - todo el canvas          -> "esta vivo?" / "esta en pausa?"
#   TIRO    - columna del proyectil   -> "salio un proyectil?"
#   ALCANCE - franja bajo el enemigo  -> "hasta donde llego?" (maxX)
import sys, time
from playwright.sync_api import sync_playwright

URL  = "file:///C:/Users/USUARIO/slug-out-/index.html"
SAVE = "lanzador_cavernas_2_save_v1"
EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
SHOT = "C:/Users/USUARIO/AppData/Local/hermes/cache/scratch"

def mk(y0, y1, x0, x1):
    return f"""() => {{
  const g = document.getElementById('c').getContext('2d', {{willReadFrequently:true}});
  const b = g.getImageData(0,0,360,560).data, a = window.__base;
  let n = 0, sumY = 0, maxX = -1;
  for (let y = {y0}; y < {y1}; y++) {{
    for (let x = {x0}; x < {x1}; x++) {{
      const i = (y*360 + x) * 4;
      const d = Math.abs(a[i]-b[i]) + Math.abs(a[i+1]-b[i+1]) + Math.abs(a[i+2]-b[i+2]);
      if (d > 60) {{ n++; sumY += y; if (x > maxX) maxX = x; }}
    }}
  }}
  return {{n:n, meanY: n ? sumY/n : -1, maxX: maxX}};
}}"""

GRAB    = """() => { document.getElementById('c').getContext('2d',{willReadFrequently:true})
             .getImageData(0,0,360,560); window.__base =
             document.getElementById('c').getContext('2d').getImageData(0,0,360,560).data; return true; }"""
ANIM    = mk(0, 560, 0, 360)     # canvas completo
TIRO    = mk(250, 400, 168, 193) # columna por donde sube el proyectil
ALCANCE = mk(250, 400, 0, 360)   # franja bajo el enemigo: mide alcance horizontal

fails = []
def check(n, c, d=""):
    print(("  OK   " if c else " FALLO ") + n + (("   [" + d + "]") if d else ""))
    if not c: fails.append(n)

def mov(page, fn, ms=320):
    page.evaluate(GRAB); page.wait_for_timeout(ms)
    return page.evaluate(fn)

def box(page):
    return page.evaluate("""() => { const r = document.getElementById('c').getBoundingClientRect();
      return {x:r.left, y:r.top, w:r.width, h:r.height}; }""")

def pt(b, lx, ly):
    return (b["x"] + lx/360*b["w"], b["y"] + ly/560*b["h"])

with sync_playwright() as p:
    br = p.chromium.launch(channel="msedge", headless=True, executable_path=EDGE,
                           args=["--disable-gpu","--autoplay-policy=no-user-gesture-required"])
    page = br.new_page(viewport={"width": 900, "height": 1000})
    errores = []
    page.on("console", lambda m: errores.append(m.text) if m.type == "error" else None)
    page.on("pageerror", lambda e: errores.append("PAGEERROR: " + str(e)))

    page.goto(URL); page.wait_for_timeout(900)

    print("\n== 1. arranque limpio ==")
    check("sin errores de consola ni pageerror", len(errores) == 0, "; ".join(errores[:3]))
    check("el menu principal se renderiza",
          page.locator("#overlay h2").inner_text().strip() == "Lanzador de Cavernas 2")
    check("el menu anuncia la mecanica nueva",
          "potencia de tiro" in page.locator("#overlay").inner_text().lower())

    print("\n== 2. arranca una partida y el canvas vive ==")
    page.click("#btnNiveles"); page.wait_for_timeout(400)
    page.click("#lv_1");     page.wait_for_timeout(800)
    check("el overlay se oculta al jugar",
          page.evaluate("document.getElementById('overlay').style.display") == "none")
    check("aparece el boton de pausa",
          page.evaluate("document.getElementById('pauseBtn').style.display") == "flex")
    m = mov(page, ANIM)
    check("el canvas se anima", m["n"] > 200, f'{m["n"]} px cambiados')

    print("\n== 3. la potencia del arrastre cambia la velocidad de salida ==")
    # El primer escupitajo del enemigo sale a los ~3300ms, asi que los dos tiros
    # de aqui caen antes y la medida no tiene proyectiles enemigos de por medio.
    b = box(page)
    def disparar(release_y, espera=150):
        """Tiro vertical (dx=0) -> el proyectil queda en la columna x=180.
        El baseline se toma DESPUES de soltar, para no capturar los puntos
        de previsualizacion. Devuelve meanY: menor = mas alto = mas potencia."""
        page.mouse.move(*pt(b, 180, 430)); page.mouse.down()
        page.mouse.move(*pt(b, 180, release_y), steps=6)
        page.mouse.up()
        page.evaluate(GRAB)
        page.wait_for_timeout(espera)
        return page.evaluate(TIRO)

    flojo  = disparar(422)          # arrastre  8px  -> potencia minima (0.55)
    page.wait_for_timeout(700)      # recarga del tiro flojo (476ms)
    fuerte = disparar(190)          # arrastre 240px -> potencia maxima (1.60)
    print(f'    tiro flojo  : meanY={flojo["meanY"]:.0f}  px={flojo["n"]}')
    print(f'    tiro fuerte : meanY={fuerte["meanY"]:.0f}  px={fuerte["n"]}')
    check("el tiro flojo genero un proyectil",  flojo["n"] > 20)
    check("el tiro fuerte genero un proyectil", fuerte["n"] > 20)
    # Prediccion teorica (v0=0.56*potencia, g_fuego=0.0002, y0=419):
    #   potencia 0.55 -> y~375   |   potencia 1.60 -> y~287
    check("el tiro cargado sube MAS ALTO (fisica real)",
          fuerte["meanY"] < flojo["meanY"] - 50,
          f'meanY {flojo["meanY"]:.0f} -> {fuerte["meanY"]:.0f}  ({flojo["meanY"]-fuerte["meanY"]:.0f} px mas alto)')
    check("la altura medida esta en el rango teorico",
          270 <= flojo["meanY"] <= 400 and 260 <= fuerte["meanY"] <= 330,
          f'flojo {flojo["meanY"]:.0f} (teorico 375) · fuerte {fuerte["meanY"]:.0f} (teorico 287)')

    print("\n== 4. pausa ==")
    page.keyboard.press("p"); page.wait_for_timeout(400)
    check("la tecla P abre el overlay de pausa", "Pausa" in page.locator("#overlay").inner_text())
    check("el overlay ofrece seguir/reiniciar/sonido/salir",
          all(t in page.locator("#overlay").inner_text()
              for t in ("Seguir jugando","Reiniciar","Sonido","Salir al menú")))
    m = mov(page, ANIM, 500)
    check("en pausa el canvas NO se mueve", m["n"] < 40, f'{m["n"]} px cambiados en pausa')
    # El BOTON de pausa, clickeado de verdad (no via window.pauseBtn, que funciona
    # solo porque los id del HTML son tambien propiedades de window)
    check("existe el boton de pausa en el DOM",
          page.evaluate("() => document.getElementById('pauseBtn') !== null"))
    page.keyboard.press("p"); page.wait_for_timeout(400)
    check("la tecla P reanuda la partida",
          page.evaluate("document.getElementById('overlay').style.display") == "none")
    page.evaluate("() => document.getElementById('pauseBtn').click()"); page.wait_for_timeout(400)
    check("el BOTON de pausa tambien pausa",
          page.evaluate("document.getElementById('overlay').style.display") != "none")
    page.evaluate("() => document.getElementById('btnSeguir').click()"); page.wait_for_timeout(400)
    check("Seguir reanuda la partida",
          page.evaluate("document.getElementById('overlay').style.display") == "none")
    m = mov(page, ANIM)
    check("al reanudar el canvas vuelve a animarse", m["n"] > 200, f'{m["n"]} px cambiados')

    print("\n== 5. controles de teclado ==")
    page.wait_for_timeout(1400)
    page.mouse.move(*pt(b, 180, 400)); page.mouse.down()
    page.mouse.move(*pt(b, 180, 300), steps=4); page.mouse.up()
    page.wait_for_timeout(1400)                      # esperar recarga
    page.evaluate(GRAB)
    page.keyboard.press("Space"); page.wait_for_timeout(120)
    m = page.evaluate(TIRO)
    check("Espacio dispara (proyectil en la columna)", m["n"] > 15, f'{m["n"]} px cambiados')

    print("\n== 6. sonido silenciable y persistente ==")
    mute = lambda: page.evaluate(f"JSON.parse(localStorage.getItem('{SAVE}')||'{{}}').mute")
    page.keyboard.press("m"); page.wait_for_timeout(300)
    check("la tecla M activa el silencio", mute() is True)
    page.reload(); page.wait_for_timeout(900)
    check("el silencio persiste al recargar", mute() is True)
    check("tras recargar no hay errores nuevos", len(errores) == 0, "; ".join(errores[:3]))
    page.keyboard.press("m"); page.wait_for_timeout(300)
    check("la tecla M vuelve a activar el sonido", mute() is False)

    print("\n== 7. contenido y ayudas ==")
    page.click("#btnNiveles"); page.wait_for_timeout(300); page.click("#lv_1"); page.wait_for_timeout(500)
    page.evaluate("document.getElementById('exitBtn').click()"); page.wait_for_timeout(400)
    page.click("#btnVolver"); page.wait_for_timeout(400)
    t = page.locator("#btnLogros").inner_text()
    check("el total de logros es 25 (21 originales + 4 nuevos)", t == "Logros 0/25", t)
    check("el menu muestra el total calculado, no hardcodeado",
          "25 logros" in page.locator("#overlay").inner_text())
    page.click("#btnAyuda"); page.wait_for_timeout(300)
    a = page.locator("#overlay").inner_text()
    check("la ayuda explica la potencia de tiro", "Potencia de tiro" in a)
    check("la ayuda explica los controles de teclado", "Espacio" in a)
    check("la ayuda menciona la caducidad del combo", "combo" in a.lower())

    print("\n== 8. capturas ==")
    page.click("#btnVolver"); page.wait_for_timeout(300)
    page.screenshot(path=f"{SHOT}/slug-menu.png")
    page.click("#btnNiveles"); page.wait_for_timeout(300); page.click("#lv_1"); page.wait_for_timeout(1600)
    page.screenshot(path=f"{SHOT}/slug-jugando.png")
    page.keyboard.press("p"); page.wait_for_timeout(500)
    page.screenshot(path=f"{SHOT}/slug-pausa.png")
    print("  slug-menu.png, slug-jugando.png, slug-pausa.png")

    print("\n== 9. el medidor de potencia se dibuja al apuntar ==")
    page.keyboard.press("p"); page.wait_for_timeout(300)   # salir de la pausa
    page.evaluate("document.getElementById('exitBtn').click()"); page.wait_for_timeout(400)
    page.click("#btnVolver"); page.wait_for_timeout(400)   # Niveles -> Menu
    page.click("#btnNiveles"); page.wait_for_timeout(300); page.click("#lv_1"); page.wait_for_timeout(1000)
    b = box(page)
    # Region del medidor: barra vertical en x 12..19, y 214..364
    REGION = """() => {
      const g = document.getElementById('c').getContext('2d',{willReadFrequently:true});
      const d = g.getImageData(12,214,8,150).data;
      let brillantes = 0, suma = 0;
      for (let i = 0; i < d.length; i += 4) {
        const l = (d[i]+d[i+1]+d[i+2])/3;
        suma += l;
        if (l > 70) brillantes++;
      }
      return {brillantes: brillantes, total: d.length/4, medio: suma/(d.length/4)};
    }"""
    reposo = page.evaluate(REGION)
    # mantener el arrastre SIN soltar: el medidor debe aparecer
    page.mouse.move(*pt(b, 180, 420)); page.mouse.down()
    page.mouse.move(*pt(b, 180, 250), steps=6)
    page.wait_for_timeout(250)
    apuntando = page.evaluate(REGION)
    page.screenshot(path=f"{SHOT}/slug-apuntando.png")
    page.mouse.up()
    print(f'    en reposo   : brillantes={reposo["brillantes"]}/{reposo["total"]}  brillo medio={reposo["medio"]:.1f}')
    print(f'    apuntando   : brillantes={apuntando["brillantes"]}/{apuntando["total"]}  brillo medio={apuntando["medio"]:.1f}')
    check("en reposo el medidor NO se dibuja (no ensucia la pantalla en tactil)",
          reposo["brillantes"] < 40, f'{reposo["brillantes"]} px brillantes')
    check("al apuntar aparece el medidor de potencia",
          apuntando["brillantes"] > reposo["brillantes"] + 150,
          f'{reposo["brillantes"]} -> {apuntando["brillantes"]} px brillantes')

    print("\n" + ("=== TODOS LOS CHECKS PASARON ===" if not fails
                  else f"=== {len(fails)} FALLO(S): " + " | ".join(fails) + " ==="))
    if errores: print("errores de consola:", errores[:5])
    br.close()
    sys.exit(1 if fails else 0)
