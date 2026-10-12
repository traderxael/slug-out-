/**
 * Habilidades — VERIFICACION DE LO DIBUJADO.
 *
 * Una habilidad que funciona pero no se ve es mecanica muerta: el jugador no
 * sabria por que no puede tirar. Este test comprueba que cada estado pinta su
 * marca, con el modo de grabacion del harness (no por pixels: el color del
 * elemento se confunde con los proyectiles del mismo elemento).
 *
 * Corre contra el index.html real via harness.cjs.
 */
const { buildGame } = require('./harness.cjs');

let fallos = 0;
const check = (nombre, ok, detalle) => {
  console.log(`  ${ok ? 'OK  ' : 'FALLA'} ${nombre}` + (detalle ? `   [${detalle}]` : ''));
  if (!ok) fallos++;
};

const pintoCon = (log, patron) =>
  log.some(e => String(e.stroke || '').includes(patron) ||
                String(e.fill   || '').includes(patron));

const G = buildGame({ recordDraw: true });
const log = G.__drawLog;
console.log('habilidades: lo que se DIBUJA de cada estado\n');

const pintar = (setup) => {
  G.iniciar('nivel', 6);
  const st = G.state;
  st.enemy.atkInterval = 1e9; st.rocaTimer = 1e9; st.rocas = [];
  setup(st);
  log.length = 0;
  G.draw();
  return log;
};

// ---------- El aviso de la habilidad usa el color del elemento ----------
for (const [tipo, hex] of [['fuego','224,90,48'], ['hielo','111,214,230'], ['sombra','138,95,214']]){
  const l = pintar(st => { st.enemy.tipo = tipo; st.enemy.habTell = 400; });
  check(`aviso de ${tipo.padEnd(7)} -> anillo del color del elemento`,
        pintoCon(l, hex), `${hex}`);
}

// ---------- El charco de fuego se ve en el piso ----------
{
  const l = pintar(st => { st.zonas = [{x:180, r:46, t:3000, max:4000, tick:0}]; });
  check('el charco se dibuja en el piso (naranja fuego)',
        pintoCon(l, '224,90,48'));
}

// ---------- Los estados del jugador se ven ----------
{
  const l = pintar(st => { st.player.freezeT = 600; });
  check('congelado -> marcas de hielo sobre el jugador',
        pintoCon(l, '111,214,230'));
}
{
  const l = pintar(st => { st.player.stunT = 600; });
  check('aturdido -> estrellas sobre el jugador', pintoCon(l, '#ffc371'));
}
{
  const l = pintar(st => { st.player.burnT = 600; });
  check('ardiendo -> llamas sobre el jugador', pintoCon(l, '224,90,48'));
}
{
  const l = pintar(st => { st.player.slowT = 600; });
  check('marea -> marcas de agua sobre el jugador', pintoCon(l, '63,149,207'));
}

// ---------- Control: sin estados, nada de esto se pinta ----------
{
  const l = pintar(() => {});
  const marcas = l.filter(e => {
    const s = String(e.stroke || '') + String(e.fill || '');
    return s.includes('111,214,230') || s.includes('255,195,113') || s.includes('63,149,207');
  }).length;
  check('sin estados: no pinta ninguna marca de jugador afectado',
        marcas === 0, `${marcas} trazos`);
}

console.log('\n' + (fallos === 0
  ? '=== OK: las habilidades y sus estados se ven ==='
  : `=== ${fallos} FALLA(S): algo no se dibuja ===`));
process.exit(fallos ? 1 : 0);
