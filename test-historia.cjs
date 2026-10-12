/**
 * Mini historia de campana — RED primero.
 *
 * La campana sin historia es una lista de niveles. Un fragmento corto tras cada
 * combate la vuelve un viaje: cuenta el mundo y al protagonista, no la trama de
 * un tiron, y el arco se lee igual que el elemento de cada nivel.
 *
 * Corre contra el index.html real via harness.cjs. La historia es HTML (no
 * canvas), asi que se verifica lo que se escribe en el overlay y los clics se
 * simulan sobre el stub de documento.
 */
const { buildGame } = require('./harness.cjs');

let fallos = 0;
const check = (n, ok, d) => {
  console.log(`  ${ok ? 'OK  ' : 'FALLA'} ${n}` + (d ? `   [${d}]` : ''));
  if (!ok) fallos++;
};
const esperar = ms => new Promise(r => setTimeout(r, ms));

(async () => {
  const G  = buildGame({});   // el hook YA es el retorno: se usa directo
  const H  = G;
  const ov = () => H.overlayHTML;

  console.log('mini historia: un fragmento por combate de campana\n');

  // ---------- 1. Hay un fragmento por cada nivel ----------
  const n = H.HISTORIA ? Object.keys(H.HISTORIA).length : 0;
  check('hay un fragmento por cada nivel de campana',
    n === H.NIVELES.length, `esperado ${H.NIVELES.length}, hay ${n}`);

  // ---------- 2. Cada uno tiene titulo y texto reales ----------
  if (H.HISTORIA) {
    const malos = Object.entries(H.HISTORIA)
      .filter(([, h]) => !h || typeof h.titulo !== 'string' || typeof h.texto !== 'string'
                        || !h.titulo.trim() || !h.texto.trim())
      .map(([k]) => k);
    check('cada fragmento tiene titulo y texto', malos.length === 0,
      malos.length ? 'incompletos: ' + malos.join(',') : `${n} fragmentos`);

    // Un fragmento de una linea no cuenta nada: es relleno con forma de historia.
    const cortos = Object.entries(H.HISTORIA)
      .filter(([, h]) => h && h.texto && h.texto.trim().length < 60)
      .map(([k]) => k);
    check('ningun fragmento es demasiado corto para contar algo', cortos.length === 0,
      cortos.length ? 'cortos: ' + cortos.join(',') : '');

    // El arco tiene que cerrar: el ultimo fragmento no puede ser otro "siguiente".
    const ult = H.HISTORIA[H.NIVELES.length];
    check('el ultimo fragmento cierra el arco (no deja la historia colgada)',
      !!ult && ult.texto.length > 120,
      ult ? `${ult.texto.length} caracteres` : 'no existe');
  }

  // ---------- 3. mostrarHistoria escribe en el overlay ----------
  if (H.mostrarHistoria) {
    G.iniciar('nivel', 3);
    H.mostrarHistoria(3, () => {});
    await esperar(20);
    const html = ov();
    check('mostrarHistoria escribe el titulo del fragmento',
      html.includes(H.HISTORIA[3].titulo), `buscado "${H.HISTORIA[3].titulo}"`);
    const palabras = H.HISTORIA[3].texto.split(/\s+/).filter(w => w.length > 5).slice(0, 4);
    check('mostrarHistoria escribe la prosa del fragmento',
      palabras.every(w => html.includes(w)), `buscado "${palabras.join(' ')}"`);
    check('el fragmento se marca con el nivel al que pertenece',
      html.includes('>3<') || html.includes('Nivel 3'), '');
  } else {
    check('mostrarHistoria existe', false);
  }

  // ---------- 4. Sin fragmento, la campana sigue derecho ----------
  if (H.mostrarHistoria) {
    let paso = false;
    H.mostrarHistoria(99, () => { paso = true; });
    check('sin fragmento para ese nivel, la continuacion corre igual', paso);
  }

  // ---------- 5. El flujo: victoria -> historia -> siguiente nivel ----------
  if (H.ganarNivel && H.mostrarHistoria) {
    const clic = id => G.__doc.getElementById(id).click();

    G.iniciar('nivel', 1);
    H.ganarNivel();
    await esperar(750);
    check('la pantalla de victoria sigue existiendo',
      ov().includes('Siguiente nivel'), '');

    // Repetir NO debe pasar por la historia: no estas avanzando la campana.
    clic('btnRepetir');
    await esperar(20);
    check('repetir arranca directo, sin intersticio de historia',
      G.__doc.getElementById('overlay').style.display === 'none' && !!G.state && G.state.nivel === 1,
      `nivel=${G.state ? G.state.nivel : '?'}`);

    // Avanzar SI: la historia es el puente entre combates.
    G.iniciar('nivel', 1);
    H.ganarNivel();
    await esperar(750);
    clic('btnSiguiente');
    await esperar(20);
    check('"Siguiente nivel" lleva a la mini historia',
      ov().includes(H.HISTORIA[1].titulo), '');
    check('la historia se puede saltar',
      !!G.__doc.getElementById('btnSaltarHist'), '');

    clic('btnSeguirHist');
    await esperar(20);
    check('la historia avanza al nivel siguiente',
      !!G.state && G.state.nivel === 2, `nivel=${G.state ? G.state.nivel : '?'}`);
  }

  console.log('\n' + (fallos === 0
    ? '=== OK: cada combate de campana termina en un pedazo de historia ==='
    : `=== ${fallos} FALLA(S) ===`));
  process.exit(fallos ? 1 : 0);
})();
