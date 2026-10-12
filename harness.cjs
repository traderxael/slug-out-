/**
 * Harness: ejecuta el index.html REAL en Node con el DOM y el audio stubeados.
 *
 * El juego vive dentro de un IIFE que no expone nada al contexto de pagina, asi
 * que este harness extrae el <script>, le inyecta un hook de exportacion justo
 * antes del cierre del IIFE, y lo evalua en un contexto con stubs. El resultado
 * es el estado y las funciones REALES del juego: se prueba comportamiento, no
 * una reimplementacion.
 *
 * Los stubs son no-ops a proposito: el canvas dibuja a la nada, el audio no
 * existe (window.AudioContext es undefined), y requestAnimationFrame no agenda
 * nada. El loop se maneja a mano con update(dt).
 */
const fs = require('fs');
const vm = require('vm');
const path = require('path');

const HTML_PATH = path.join(__dirname, 'index.html');

function makeCtx(canvasEl, rec){
  const store = {};
  const ctxProxy = new Proxy(store, {
    get(t, k){
      if (k === 'canvas') return canvasEl;
      if (k === 'getImageData' || k === 'createImageData')
        return (x, y, w, h) => ({
          data: new Uint8ClampedArray(Math.max(1, (w|0) * (h|0) * 4)),
          width: w|0, height: h|0
        });
      if (k === 'measureText') return () => ({ width: 10 });
      // Modo grabacion: cada trazo o texto anota el estilo vigente. Sirve para
      // verificar QUE se dibuja y de que color sin adivinar por pixels, algo
      // imposible cuando dos entidades comparten tono: el escudo cian
      // (127,212,255) y el proyectil de hielo (#6fd6e6) son casi el mismo color,
      // y contarlos juntos daria un falso positivo permanente.
      // Incluye fillText/strokeText: varios estados se dibujan con glifos, no
      // con trazos, y sin grabarlos parecerian ausentes.
      if (rec && (k === 'stroke' || k === 'fill' || k === 'fillText' || k === 'strokeText')){
        return (...args) => {
          // Solo los estilos cadena son comparables: un gradiente llega como
          // objeto (el createLinearGradient no-op devuelve el proxy) y romperia
          // cualquier String() del consumidor. Se anota null y listo.
          rec.push({ op: k, text: args[0],
                     stroke: typeof t.strokeStyle === 'string' ? t.strokeStyle : null,
                     fill:   typeof t.fillStyle   === 'string' ? t.fillStyle   : null,
                     lineWidth: t.lineWidth });
          return ctxProxy;
        };
      }
      if (k in t) return t[k];
      // cualquier otro metodo de canvas es un no-op encadenable
      return () => ctxProxy;
    },
    set(t, k, v){ t[k] = v; return true; }
  });
  return ctxProxy;
}

function makeEl(tag, rec){
  const ctx = makeCtx(null, rec);
  const el = {
    tagName: String(tag || 'div').toUpperCase(),
    style: {}, dataset: {},
    width: 360, height: 560,
    innerHTML: '', textContent: '', value: '',
    classList: { add(){}, remove(){}, toggle(){}, contains(){ return false } },
    // Los clics se registran y se pueden disparar a mano: asi un test de Node
    // puede recorrer el flujo completo (victoria -> historia -> siguiente nivel)
    // sin abrir un navegador. Sin esto, addEventListener era un no-op y ningun
    // boton del overlay era accionable desde los tests.
    addEventListener(t, fn){ this.__ls = this.__ls || {}; this.__ls[t] = fn; },
    removeEventListener(t){ if(this.__ls) delete this.__ls[t]; },
    dispatchEvent(){ return true },
    click(){ const f = this.__ls && this.__ls.click; if(f) f({ target:this }); },
    appendChild(c){ return c }, removeChild(c){ return c }, remove(){},
    insertBefore(c){ return c },
    getBoundingClientRect(){ return { left:0, top:0, width:360, height:560, right:360, bottom:560 }; },
    getContext(){ return ctx; },
    setAttribute(){}, removeAttribute(){}, getAttribute(){ return null },
    querySelector(){ return null }, querySelectorAll(){ return [] },
    scrollTop: 0, scrollHeight: 0, offsetWidth: 360, offsetHeight: 560,
    focus(){}, blur(){},
    setPointerCapture(){}, releasePointerCapture(){}, hasPointerCapture(){ return false }
  };
  return el;
}

/**
 * Construye el juego. Devuelve el hook con el estado y las funciones reales.
 */
function buildGame(opts){
  const rec = (opts && opts.recordDraw) ? [] : null;
  const html = fs.readFileSync(HTML_PATH, 'utf8');
  const m = html.match(/<script>([\s\S]*?)<\/script>/);
  if (!m) throw new Error('no encontre el <script> en index.html');
  let src = m[1];

  // Hook de exportacion, inyectado dentro del IIFE justo antes de su cierre.
  const hook = `
;globalThis.__G = {
  get state(){ return state; },
  get save(){ return save; },
  update, draw, fire, iniciar, detenerJuego, nuevoEstado,
  W, H, LX, LY, SELECTOR_Y, LEDGE_Y, FLOOR_Y, GRAV, V0, VEL,
  POWER_MIN, POWER_MAX, ELEMENT_INFO, ELKEYS, NIVELES,
  applyHit, addParticles, muzzlePos, vel, cooldownFor,
  cargaMaxima, powerMult,
    // El hook se inyecta DENTRO del IIFE, asi que solo ve lo que esta en su
    // ambito. El stub de documento vive afuera: se le cuelga despues para que los
    // tests puedan disparar clics sobre el overlay.
    HISTORIA, mostrarHistoria, ganarNivel,
    arquetipoDe, arquetipoInfinito, enemigoNivel, enemigoInfinito, crearEnemigo,
    equipoActual, baseSave, normalizeSave, mejorRareza, renderMenu,
    toggleEquipo, renderEquipo, EQUIPO_MAX,
    get overlayHTML(){ return overlayEl.innerHTML; }
};`;
  const close = src.lastIndexOf('})();');
  if (close < 0) throw new Error('no encontre el cierre del IIFE');
  src = src.slice(0, close) + hook + src.slice(close);

  const canvasEl = makeEl('canvas', rec);
  const els = {
    c: canvasEl,
    overlay: makeEl('div'),
    exitBtn: makeEl('button'),
    pauseBtn: makeEl('button')
  };

  const documentStub = {
    getElementById: (id) => els[id] || (els[id] = makeEl('div')),
    createElement: (tag) => makeEl(tag),
    addEventListener(){}, removeEventListener(){},
    body: makeEl('body'), documentElement: makeEl('html'),
    hidden: false, visibilityState: 'visible'
  };

  const windowStub = {
    devicePixelRatio: 1,
    addEventListener(){}, removeEventListener(){},
    AudioContext: undefined, webkitAudioContext: undefined,
    requestAnimationFrame: () => 1,
    cancelAnimationFrame(){},
    innerWidth: 400, innerHeight: 700,
    location: { href: 'file:///index.html', reload(){} }
  };

  const sandbox = {
    window: windowStub,
    document: documentStub,
    localStorage: { getItem: () => null, setItem(){}, removeItem(){} },
    requestAnimationFrame: windowStub.requestAnimationFrame,
    cancelAnimationFrame(){},
    performance: { now: () => Date.now() },
    navigator: { userAgent: 'node' },
    Math, JSON, Date, console, setTimeout, clearTimeout,
    Uint8ClampedArray, Array, Object, Number, String, Boolean, Error,
    isNaN, parseFloat, parseInt
  };
  sandbox.globalThis = sandbox;
  sandbox.window.document = documentStub;

  vm.createContext(sandbox);
  vm.runInContext(src, sandbox, { filename: 'index.html' });
  if (!sandbox.__G) throw new Error('el hook de exportacion no se inyecto');
  if (rec) sandbox.__G.__drawLog = rec;   // log de lo realmente dibujado
  sandbox.__G.__doc = documentStub;       // para simular clics desde los tests
  return sandbox.__G;
}

module.exports = { buildGame };
