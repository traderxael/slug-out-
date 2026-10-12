# 🐌 Lanzador de Cavernas 2

Juego de disparos parabólicos con babosas elementales. 8 elementos, 40 babosas coleccionables, 5 rarezas, cofres, habilidades definitivas, **potencia de tiro por arrastre** y enemigos que esquivan.

## 🎮 Cómo jugar

Abrí `index.html` en tu navegador (doble click) o jugá online con GitHub Pages.

- **Arrastrá** para apuntar y **soltá** para disparar
- **Cuanto más lejos arrastres, más potente es el disparo**: más daño y el efecto
  elemental **garantizado** a carga máxima (marca `MÁX`) — pero la recarga se alarga
- **Tocá** una babosa abajo para cambiar de elemento
- **Tocá la barra de ULTI** cuando esté cargada para usar la habilidad definitiva
- **Interceptá** los escupitajos del rival con tus disparos

### Teclado (PC)

| Tecla | Acción |
|---|---|
| `←` `→` | Apuntar |
| `↑` `↓` | Subir / bajar la potencia |
| `Espacio` | Disparar |
| `1` – `8` | Cambiar de elemento |
| `U` | ULTI |
| `P` / `Esc` | Pausa |
| `M` | Silenciar |
| `R` | Reiniciar partida |

## ⚡ Potencia de tiro: la decisión central

El arrastre controla la potencia, y la potencia tiene dos caras:

| Potencia | Daño | Recarga | DPS | Efecto elemental |
|---|---|---|---|---|
| 0.55 (mínima) | 13 | 476 ms | 27.3 | 35-40% de probabilidad |
| 1.00 (normal) | 16 | 620 ms | 25.8 | 35-40% de probabilidad |
| 1.60 (máxima) | 25 | 812 ms | 30.8 | **garantizado** y +50% más fuerte |

Espamear tiros flojos te da más cadencia y más chances de interceptar; comprometerte
a un tiro pesado rinde más daño por impacto y asegura aturdir / congelar / encadenar,
pero si el rival **esquiva** perdés 812 ms. El combo caduca a los 5 segundos sin acertar,
así que mantenerlo también empuja a no quedarse cargando.

## ⚔️ Elementos

| Elemento | Habilidad | ULTI |
|---|---|---|
| 🔥 Fuego | Quema (daño por tiempo) | Lluvia de meteoritos |
| 💧 Agua | Ralentiza al rival | Ola sanadora |
| 🪨 Tierra | Puede aturdir | Terremoto brutal |
| 💨 Aire | Perfora resistencias | Triple disparo |
| ❄️ Hielo | Puede congelar | Congelación total |
| ⚡ Rayo | Rápido, golpea 2 veces | Tormenta eléctrica |
| 🌿 Planta | Te cura al impactar | Enredo 5s |
| 🌑 Sombra | Roba vida | Eclipse (daño x3) |

## 🏆 Sistemas

- **12 niveles** con Guardianes jefes
- **Modo infinito** con dificultad progresiva
- **5 cofres**: madera, hierro, dorado, cristal y mítico
- **25 logros** desbloqueables (incluye *Francotirador* por críticos y *A toda carga*
  por disparos a carga máxima)
- **3 desafíos diarios** con recompensa en gemas
- **Racha diaria** de días consecutivos jugando
- **Pausa** con reanudar / reiniciar / silenciar, y pausa automática al cambiar de pestaña

## 🛠️ Tech

Un solo archivo HTML + CSS + JS vanilla. Sin dependencias, sin build. Todo el arte es
procedural (Canvas 2D) y el audio es sintetizado (Web Audio API).

## ✅ Verificación

Dos suites que corren contra el `index.html` real, sin servidor:

```bash
# 1. Balance y tradeoffs: extrae las funciones de potencia DEL archivo y las ejecuta
node test-potencia.cjs

# 2. Juego real en Edge headless: mide la fisica por pixels y ejercita los controles
"C:/Users/USUARIO/AppData/Local/Programs/Python/Python312/python.exe" qa-juego.py

# 3. El juego YA PUBLICADO, cargado desde la URL de produccion
"C:/Users/USUARIO/AppData/Local/Programs/Python/Python312/python.exe" qa-live.py
```

La suite 2 mide, entre otras cosas, que un tiro a potencia baja se detiene en `y≈368`
y uno a potencia máxima en `y≈280` del canvas (predicción teórica: 375 y 287), que el
canvas se congela en pausa (0 px cambiados) y que el medidor de potencia aparece al
apuntar (0 → 1082 px brillantes) sin ensuciar la pantalla en reposo.

La suite 3 corre lo mismo contra `https://traderxael.github.io/slug-out-/`: arranca el
nivel, dispara 3 pares de tiros con nivel reiniciado entre cada uno (así ningún
escupitajo del rival baja por la banda de medición) y comprueba que el cargado sube
más alto en las 3 repeticiones (21 / 28 / 27 px).
