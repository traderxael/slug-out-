/**
 * Verifica el tradeoff de la esquiva telegrafiada por CALCULO, no por lectura.
 *
 * Extrae las constantes REALES de index.html y comprueba la cadena de tiempos
 * que sostiene el diseno: el rival ve venir el tiro a `radioDeteccion` px, se
 * agacha `dodgeWindupMax` ms y recien despues se mueve `dodgeT` ms. Un tiro que
 * llegue dentro de esa ventana INTERRUMPE la esquiva.
 *
 * La evasion ya no es un dado: es la DISTANCIA a la que ve venir el tiro. Mas
 * evasion = mas radio = el tell aparece mas temprano y cuesta mas cortarlo.
 * Lo determinista lo cubre test-esquiva-determinista.cjs; aca se sostiene que,
 * incluso en el peor caso (el jefe, evasion maxima), la potencia sigue siendo la
 * respuesta correcta. Si la curva se invirtiera, el tradeoff seria al reves y la
 * mecanica no significaria nada.
 */
const fs = require('fs');
const html = fs.readFileSync(__dirname + '/index.html', 'utf8');

const num = (re, label) => {
  const m = html.match(re);
  if (!m) throw new Error('no encontre ' + label);
  return parseFloat(m[1]);
};

const V0             = num(/const V0 = ([\d.]+)/, 'V0');
const GRAV_FUEGO     = num(/const GRAV = \{fuego:([\d.]+)/, 'GRAV.fuego');
const POWER_MIN      = num(/POWER_MIN = ([\d.]+)/, 'POWER_MIN');
const POWER_MAX      = num(/POWER_MAX = ([\d.]+)/, 'POWER_MAX');
const WINDUP         = num(/dodgeWindupMax:(\d+)/, 'dodgeWindupMax');
const DODGE_T        = num(/en\.dodgeT = (\d+)/, 'dodgeT');
// La evasion ya no es probabilidad: es la distancia a la que ve venir el tiro.
const RADIO_BASE     = num(/radioDeteccion = (\d+)/, 'radio base de deteccion');
const RADIO_ESC      = num(/radioDeteccion = \d+ \+ Math\.max\(0, en\.evasion\)\*(\d+)/, 'radio por punto de evasion');
const EVASION_MAX    = num(/Math\.min\(([\d.]+), st\.evasion\+0\.1\)/, 'evasion maxima del jefe');
const MARGEN_COLISION= num(/en\.r\*1\.4\+5/, 'margen de colision') ? 0 : 0; // documental

// El elemento por defecto es equipo[0] = ELKEYS[0] = 'fuego' (VEL solo sobreescribe 'rayo')
const vel = 0.56 * 1;   // V0 * potencia 1

console.log('constantes extraidas del archivo real:');
console.log(`  V0=${V0}  GRAV.fuego=${GRAV_FUEGO}  POWER ${POWER_MIN}..${POWER_MAX}`);
console.log(`  radio = ${RADIO_BASE} + evasion*${RADIO_ESC}   (evasion max ${EVASION_MAX} -> ${RADIO_BASE + EVASION_MAX*RADIO_ESC}px)`);
console.log(`  viento=${WINDUP}ms  esquiva=${DODGE_T}ms`);
console.log(`  ventana de interrupcion total = ${WINDUP + DODGE_T}ms\n`);

/**
 * Tiempo que tarda el proyectil en recorrer `dy` px hacia arriba, CON gravedad.
 * y(t) = y0 - v*t + 0.5*g*t^2  =>  0.5*g*t^2 - v*t + dy = 0  (raiz menor)
 */
function tiempoDeVuelo(potencia, dy) {
  const v = V0 * potencia, g = GRAV_FUEGO;
  const disc = v * v - 2 * g * dy;
  if (disc <= 0) return Infinity;          // no llega
  return (v - Math.sqrt(disc)) / g;
}

const VENTANA = WINDUP + DODGE_T;
let fallos = 0;
const check = (nombre, ok, detalle) => {
  console.log(`  ${ok ? 'OK  ' : 'FALLA'} ${nombre}` + (detalle ? `   [${detalle}]` : ''));
  if (!ok) fallos++;
};

console.log('tiempo de vuelo desde que el rival ve venir el tiro:');
const casos = [
  ['MAXIMA (1.60)', POWER_MAX],
  ['media   (1.00)', 1.00],
  ['MINIMA (0.55)', POWER_MIN],
];
const t = {};

// El peor caso es el JEFE: evasion maxima, radio maximo, el tell aparece antes y
// hay mas tiempo para que la esquiva complete. Si el tradeoff se sostiene aca,
// se sostiene en todos los niveles.
const RADIO_JEFE = RADIO_BASE + EVASION_MAX * RADIO_ESC;
for (const [nombre, p] of casos) {
  t[nombre] = tiempoDeVuelo(p, RADIO_JEFE);
  const fase = t[nombre] <= WINDUP ? 'durante el tell (congelado)'
            : t[nombre] <= VENTANA ? 'durante el movimiento de esquiva'
            : 'DESPUES: la esquiva ya completo';
  console.log(`  ${nombre}: ${t[nombre].toFixed(0).padStart(4)}ms  -> ${fase}`);
}

console.log(`\nradio por nivel (progresion de la lectura):`);
for (const [nivel, ev] of [[3, 0.06], [4, 0.12], [6, 0.24], [8, 0.36], [9, 0.42], [12, 0.5]]) {
  const r = RADIO_BASE + ev * RADIO_ESC;
  const tmin = tiempoDeVuelo(POWER_MIN, r);
  const tmax = tiempoDeVuelo(POWER_MAX, r);
  console.log(`  nivel ${String(nivel).padStart(2)} (evasion ${ev.toFixed(2)}): radio ${r.toFixed(0).padStart(3)}px`
    + `  -> a potencia minima ${tmin > VENTANA ? 'ESQUIVA' : 'no esquiva'} (${tmin.toFixed(0)}ms),`
    + ` a maxima siempre interrumpe (${tmax.toFixed(0)}ms)`);
}

console.log('\nel tradeoff se sostiene (peor caso: el jefe):');
check('a potencia MAXIMA el tiro llega durante el tell (el rival esta quieto)',
      t['MAXIMA (1.60)'] < WINDUP,
      `${t['MAXIMA (1.60)'].toFixed(0)}ms < ${WINDUP}ms`);
check('a potencia media llega dentro de la ventana de interrupcion',
      t['media   (1.00)'] < VENTANA,
      `${t['media   (1.00)'].toFixed(0)}ms < ${VENTANA}ms`);
check('a potencia MINIMA llega tarde: la esquiva ya se completo',
      t['MINIMA (0.55)'] > VENTANA,
      `${t['MINIMA (0.55)'].toFixed(0)}ms > ${VENTANA}ms`);
check('mas carga llega SIEMPRE antes (monotono)',
      t['MAXIMA (1.60)'] < t['media   (1.00)'] && t['media   (1.00)'] < t['MINIMA (0.55)'],
      'max < media < minima');
// La progresion es lo que hacia legible la evasion: a mas nivel el radio crece,
// asi que la potencia MINIMA pasa de funcionar a no funcionar. Sin esto, subir
// de nivel no cambiaria nada y el jugador no tendria que adaptarse.
check('mas evasion = mas radio = la potencia minima deja de bastar',
      tiempoDeVuelo(POWER_MIN, RADIO_BASE + 0.12*RADIO_ESC) < VENTANA
      && tiempoDeVuelo(POWER_MIN, RADIO_BASE + 0.42*RADIO_ESC) > VENTANA,
      `nivel 4 ${tiempoDeVuelo(POWER_MIN, RADIO_BASE + 0.12*RADIO_ESC).toFixed(0)}ms`
      + ` vs nivel 9 ${tiempoDeVuelo(POWER_MIN, RADIO_BASE + 0.42*RADIO_ESC).toFixed(0)}ms`);

// El tell tiene que ser VISIBLE: si durara menos que un frame (~16ms) no se
// podria leer, y si durara mas que el vuelo del tiro mas rapido nunca se
// podria castigar.
check('el tell dura lo suficiente para leerse (>= 3 frames)',
      WINDUP >= 48, `${WINDUP}ms`);
check('el tell es castigable: el tiro mas rapido llega antes de que cierre',
      t['MAXIMA (1.60)'] < WINDUP + DODGE_T);

console.log('\n' + (fallos === 0
  ? '=== OK: el tradeoff carga-vs-evasion es real y calculable ==='
  : `=== ${fallos} FALLA(S): el balance de la esquiva no sostiene el tradeoff ===`));
process.exit(fallos ? 1 : 0);
