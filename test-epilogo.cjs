/**
 * El epilogo tras el nivel 12 (issue #3).
 *
 * El arco de la campana cierra en el 12 ("La verdad"), pero el juego sigue:
 * del 13 al 28 van las consecuencias y el descenso. Lo que faltaba era el
 * cierre: un fragmento que dice que paso DESPUES de la verdad, para que la
 * campana se cierre de verdad y los niveles que sigan no sean un "siguiente"
 * mas.
 *
 * El epilogo vive en su PROPIA pantalla, no en HISTORIA: HISTORIA es 1 a 1 con
 * los niveles (un fragmento por combate) y el epilogo no es un nivel, es el
 * puente entre dos actos.
 *
 * Corre contra el index.html real via harness.cjs.
 */
const { buildGame } = require('./harness.cjs');

let fallos = 0;
const check = (nombre, ok, detalle) => {
  console.log(`  ${ok ? 'OK  ' : 'FALLA'} ${nombre}` + (detalle ? `   [${detalle}]` : ''));
  if (!ok) fallos++;
};
const esperar = ms => new Promise(r => setTimeout(r, ms));

(async () => {
  const G = buildGame({});
  const ov = () => G.overlayHTML;
  const clic = id => G.__doc.getElementById(id).click();

  console.log('epilogo: el cierre que faltaba tras la verdad\n');

  // ---------- 1. el epilogo existe y es un fragmento de verdad ----------
  const ep = G.EPILOGO;
  check('el epilogo existe', !!ep, ep ? '' : 'no esta definido');
  if (ep) {
    check('el epilogo tiene titulo y texto',
      typeof ep.titulo === 'string' && ep.titulo.trim().length > 0
      && typeof ep.texto === 'string' && ep.texto.trim().length > 0, '');
    check('el texto del epilogo no es un relleno de una linea',
      ep.texto.trim().length >= 120, `${ep.texto.trim().length} caracteres`);
    check('el epilogo es su propia pieza: no es un fragmento de nivel',
      !Object.values(G.HISTORIA || {}).some(h => h && h.titulo === ep.titulo), '');
  }

  // ---------- 2. mostrarEpilogo escribe en el overlay ----------
  if (G.mostrarEpilogo) {
    let corrado = false;
    G.mostrarEpilogo(() => { corrado = true; });
    await esperar(20);
    check('el epilogo se escribe en el overlay',
      ep && ov().includes(ep.titulo), '');
    check('el epilogo se puede saltar',
      !!G.__doc.getElementById('btnSaltarEpilogo'), '');
    clic('btnSeguirEpilogo');
    await esperar(20);
    check('continuar el epilogo sigue con el juego', corrado, '');
  } else {
    check('mostrarEpilogo existe', false);
  }

  // ---------- 3. el epilogo va DESPUES de la verdad y ANTES del 13 ----------
  if (G.ganarNivel && G.mostrarEpilogo) {
    G.iniciar('nivel', 12);
    G.ganarNivel();
    await esperar(750);
    check('la pantalla de victoria del 12 existe',
      ov().includes('Siguiente nivel'), '');

    clic('btnSiguiente');
    await esperar(20);
    check('primero va el fragmento del 12 ("La verdad")',
      ov().includes(G.HISTORIA[12].titulo), '');

    clic('btnSeguirHist');
    await esperar(20);
    check('despues del fragmento viene el epilogo',
      ov().includes(ep.titulo), '');

    clic('btnSeguirEpilogo');
    await esperar(20);
    check('el epilogo abre el nivel 13',
      !!G.state && G.state.nivel === 13, `nivel=${G.state ? G.state.nivel : '?'}`);
  } else {
    check('el flujo de victoria esta disponible', false);
  }

  // ---------- 4. los demás niveles NO muestran el epilogo ----------
  if (G.ganarNivel && G.mostrarEpilogo) {
    G.iniciar('nivel', 11);
    G.ganarNivel();
    await esperar(750);
    clic('btnSiguiente');
    await esperar(20);
    clic('btnSeguirHist');
    await esperar(20);
    check('del 11 al 12 no hay epilogo: se entra derecho al nivel 12',
      !!G.state && G.state.nivel === 12 && !(ep && ov().includes(ep.titulo)),
      `nivel=${G.state ? G.state.nivel : '?'}`);
  }

  // ---------- 5. el epilogo queda en la ruta de la cueva ----------
  if (G.renderHistoria && G.epilogoVisto) {
    G.save.estrellas = {12: 3};
    check('el epilogo se considera visto cuando viviste la verdad',
      G.epilogoVisto() === true, '');
    G.renderHistoria();
    await esperar(30);
    check('la ruta de la cueva muestra el epilogo',
      ep && ov().includes(ep.titulo), '');

    G.save.estrellas = {1: 3};
    check('sin la verdad vivida, el epilogo todavia no aparece',
      G.epilogoVisto() === false, '');
    G.renderHistoria();
    await esperar(30);
    check('y la ruta no lo muestra',
      !(ep && ov().includes(ep.titulo)), '');
  } else {
    check('la ruta de la cueva existe', false);
  }

  console.log('\n' + (fallos === 0
    ? '=== OK: la campana cierra con un epilogo ==='
    : `=== ${fallos} FALLA(S) ===`));
  process.exit(fallos ? 1 : 0);
})();
