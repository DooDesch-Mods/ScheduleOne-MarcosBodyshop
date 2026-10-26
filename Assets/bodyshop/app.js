// Marco's Bodyshop - the garage menu.
//
// One list at a time: the categories first, Enter opens one, Enter on an entry buys it, Escape goes back up. An entry
// with a group sits in a sub-list under the group's name (Lights & Looks > Xenon Headlights > the colours). The mod
// sends the arrow keys as 'key' events (the page only sees keys inside a focused field); in a browser the document's
// own keydown stands in. The mouse picks a row by hovering and buys it by clicking.
//
// The host owns the data: shop.start gives the categories, shop.list a category's entries, shop.pick previews an
// entry on the vehicle ("cat/" ends the preview), shop.buy asks for a purchase and returns "" or the reason it was not
// sent. The host answers with 'flash' (the line under the menu) and 'refresh' (the data changed).

var inGame = typeof s1 !== 'undefined';
var VISIBLE = 9;

var root = null;          // { garage, vehicle, cash, items: [{ id, label, icon, note | locked }] }
var stack = [];           // [{ kind: 'root' | 'cat' | 'group', title, rows, pick, top, cat, group }]
var cash = 0;

// ---- host calls -------------------------------------------------------------------------------------------------

var call = inGame ? function (name, arg) { return s1.call(name, arg || ''); } : browserCall;

function json(name, arg) {
  var s = call(name, arg);
  if (!s) return null;
  try { return JSON.parse(s); }
  catch (e) { flash('The garage sent bad data.'); return null; }
}

// ---- building the levels ----------------------------------------------------------------------------------------

function rootLevel() {
  var rows = root.items.map(function (c) { return { kind: 'cat', id: c.id, label: c.label, icon: c.icon, note: c.note, locked: c.locked }; });
  return { kind: 'root', title: root.vehicle || root.garage, rows: rows, pick: 0, top: 0 };
}

function categoryLevel(cat) {
  var rows = [], groups = {};
  cat.items.forEach(function (e) {
    if (!e.group) { rows.push({ kind: 'entry', id: e.id, entry: e }); return; }
    if (groups[e.group]) return;
    groups[e.group] = true;
    rows.push({ kind: 'group', id: 'g:' + e.group, label: e.group, group: e.group, note: groupNote(cat, e.group) });
  });
  return { kind: 'cat', title: cat.title, cat: cat, rows: rows, pick: firstInstalled(rows), top: 0 };
}

function groupLevel(cat, group) {
  var rows = cat.items.filter(function (e) { return e.group === group; })
                      .map(function (e) { return { kind: 'entry', id: e.id, entry: e }; });
  return { kind: 'group', title: group, cat: cat, group: group, rows: rows, pick: firstInstalled(rows), top: 0 };
}

function groupNote(cat, group) {
  for (var i = 0; i < cat.items.length; i++) {
    var e = cat.items[i];
    if (e.group === group && e.installed && !e.toggle) return e.label;
  }
  return '';
}

function firstInstalled(rows) {
  for (var i = 0; i < rows.length; i++) if (rows[i].entry && rows[i].entry.installed && !rows[i].entry.toggle) return i;
  return 0;
}

function level() { return stack[stack.length - 1]; }
function picked() { var lv = level(); return lv.rows[lv.pick]; }

// ---- drawing ------------------------------------------------------------------------------------------------------

function el(tag, cls, text) {
  var e = document.createElement(tag);
  if (cls) e.className = cls;
  if (text !== undefined) e.textContent = text;
  return e;
}

function img(cls, src) { var i = el('img', cls); i.setAttribute('src', src + '.png'); return i; }

function money(n) {
  var s = String(Math.abs(Math.round(n))), out = '';
  while (s.length > 3) { out = ',' + s.slice(-3) + out; s = s.slice(0, -3); }
  return (n < 0 ? '-$' : '$') + s + out;
}

function render() {
  var lv = level();
  if (lv.rows.length === 0) lv.pick = 0;
  else lv.pick = Math.max(0, Math.min(lv.pick, lv.rows.length - 1));
  if (lv.pick < lv.top) lv.top = lv.pick;
  if (lv.pick >= lv.top + VISIBLE) lv.top = lv.pick - VISIBLE + 1;
  lv.top = Math.max(0, Math.min(lv.top, Math.max(0, lv.rows.length - VISIBLE)));

  document.getElementById('crumb').textContent = lv.title || '';
  document.getElementById('count').textContent = lv.rows.length ? (lv.pick + 1) + ' / ' + lv.rows.length : '';
  document.getElementById('cash').textContent = money(cash);
  document.getElementById('vehicle').textContent = root ? root.vehicle : '';

  var list = document.getElementById('list');
  list.replaceChildren();
  if (lv.rows.length === 0) list.appendChild(el('div', 'more', 'Nothing here'));
  if (lv.top > 0) list.appendChild(el('div', 'more', 'more above'));
  var end = Math.min(lv.rows.length, lv.top + VISIBLE);
  for (var i = lv.top; i < end; i++) list.appendChild(rowOf(lv.rows[i], i, i === lv.pick));
  if (end < lv.rows.length) list.appendChild(el('div', 'more', 'more below'));

  detail(lv.rows[lv.pick]);
}

function rowOf(r, index, isPick) {
  var e = r.entry;
  var d = el('div', 'row' + (isPick ? ' pick' : '') + (r.locked ? ' locked' : ''));
  if (r.icon) {
    var ic = img('ico', r.icon);
    // The game tints a glyph through `color`; a browser does not, so the white glyph is inverted on the light row.
    if (!inGame && isPick) ic.style.filter = 'invert(1)';
    d.appendChild(ic);
  }
  if (e && e.swatch) { var sw = el('div', 'swatch'); sw.style.background = e.swatch; d.appendChild(sw); }
  d.appendChild(el('span', 'label', e ? e.label : r.label));
  rightSide(d, r);
  d.addEventListener('mouseenter', function () { if (level().pick !== index) { level().pick = index; changed(); } });
  d.addEventListener('click', function () { level().pick = index; enter(); });
  d.addEventListener('wheel', function (ev) {
    var dir = inGame ? ev.wheelDelta : (ev.deltaY > 0 ? 1 : -1);
    move(dir > 0 ? 1 : -1);
  });
  return d;
}

function rightSide(d, r) {
  var e = r.entry;
  if (!e) {
    if (r.locked) d.appendChild(img('ico', 'i-lock'));
    else if (r.note) d.appendChild(el('span', 'note', r.note));
    return;
  }
  if (e.toggle) {
    d.appendChild(el('span', 'note', e.installed ? 'Fitted' : 'Removed'));
    d.appendChild(el('span', 'price' + (e.price > cash ? ' short' : ''), e.price === 0 ? 'Free' : money(e.price)));
    return;
  }
  if (e.installed) {
    var o = el('div', 'owned');
    o.appendChild(img('', 'i-check'));
    o.appendChild(el('span', '', 'Installed'));
    d.appendChild(o);
    return;
  }
  d.appendChild(el('span', 'price' + (e.price > cash ? ' short' : ''), e.price === 0 ? 'Free' : money(e.price)));
}

function detail(r) {
  var d = document.getElementById('detail');
  d.replaceChildren();
  if (!r || r.kind === 'group' || (r.kind === 'cat' && !r.locked)) { d.style.display = 'none'; return; }
  d.style.display = 'flex';
  if (r.kind === 'cat') { d.appendChild(el('div', 'warn', r.locked)); return; }

  var e = r.entry;
  if (e.blocked) d.appendChild(el('div', 'warn', e.blocked));
  else if (e.text) d.appendChild(el('div', 'text', e.text));
  if (e.unlocked !== undefined) {
    d.appendChild(el('div', 'text', e.unlocked
      ? 'Unlocked. Removing and refitting is free.'
      : 'Pay once to unlock. Removing and refitting is free after that.'));
  }
  if (!e.now || !e.then) {
    if (!d.firstChild) d.style.display = 'none';
    return;
  }
  var names = level().cat.stats || [];
  for (var i = 0; i < names.length; i++) {
    var now = e.now[i] || 0, then = e.then[i] || 0;
    var s = el('div', 'stat');
    s.appendChild(el('span', 'name', names[i]));
    var t = el('div', 'track');
    var f = el('div', 'fill'); f.style.width = pct(Math.min(now, then)); t.appendChild(f);
    if (then > now + 0.001) { var g = el('div', 'gain'); g.style.width = pct(then - now); t.appendChild(g); }
    if (now > then + 0.001) { var l = el('div', 'loss'); l.style.width = pct(now - then); t.appendChild(l); }
    s.appendChild(t);
    d.appendChild(s);
  }
}

function pct(f) { return Math.round(Math.max(0, Math.min(1, f)) * 100) + '%'; }

// ---- moving and buying ----------------------------------------------------------------------------------------

// The highlighted row changed: preview it on the vehicle (or end the preview), then draw.
function changed() {
  var lv = level(), r = picked();
  if (lv.cat) call('shop.pick', lv.cat.id + '/' + (r && r.entry ? r.entry.id : ''));
  render();
}

function move(step) {
  var lv = level(), n = lv.rows.length;
  if (n === 0) return;
  lv.pick = (lv.pick + step + n) % n;
  changed();
}

function enter() {
  var lv = level(), r = picked();
  if (!r) return;
  if (r.kind === 'cat') {
    if (r.locked) { flash(r.locked); return; }
    var cat = json('shop.list', r.id);
    if (!cat) { flash('Not available.'); return; }
    stack.push(categoryLevel(cat));
    changed();
    return;
  }
  if (r.kind === 'group') { stack.push(groupLevel(lv.cat, r.group)); changed(); return; }

  var e = r.entry;
  if (e.installed && !e.toggle) { flash('Already installed.'); return; }
  if (e.blocked) { flash(e.blocked); return; }
  if (e.price > cash) { flash('Not enough cash.'); return; }
  var why = call('shop.buy', lv.cat.id + '/' + e.id);
  if (why) flash(why);
}

function back() {
  var lv = level();
  if (lv.kind === 'group') { stack.pop(); changed(); return; }
  if (lv.kind === 'cat') { stack.pop(); call('shop.leave', ''); refreshRoot(); render(); return; }
  call('shop.close', '');
}

// The data changed on the host: reload every open level and keep the highlighted rows where they were.
function refresh() {
  refreshRoot();
  for (var i = 1; i < stack.length; i++) {
    var old = stack[i];
    var cat = json('shop.list', old.cat.id);
    if (!cat) { stack.length = i; break; }
    var fresh = old.kind === 'group' ? groupLevel(cat, old.group) : categoryLevel(cat);
    keepPick(old, fresh);
    stack[i] = fresh;
  }
  render();
}

function refreshRoot() {
  var fresh = json('shop.start', '');
  if (!fresh) return;
  root = fresh;
  cash = root.cash;
  var lv = rootLevel();
  if (stack.length) keepPick(stack[0], lv);
  stack[0] = lv;
}

function keepPick(old, fresh) {
  var id = old.rows[old.pick] ? old.rows[old.pick].id : null;
  for (var i = 0; i < fresh.rows.length; i++) if (fresh.rows[i].id === id) { fresh.pick = i; break; }
  fresh.top = old.top;
}

var flashTimer = null;
function flash(text) {
  var f = document.getElementById('flash');
  f.textContent = text;
  f.classList.add('on');
  if (flashTimer) clearTimeout(flashTimer);
  flashTimer = setTimeout(function () { f.classList.remove('on'); }, 2600);
}

function key(k) {
  if (!stack.length) return;
  if (k === 'up') move(-1);
  else if (k === 'down') move(1);
  else if (k === 'enter') enter();
  else if (k === 'back') back();
}

function start() {
  stack = [];
  refreshRoot();
  if (!stack.length) { flash('The garage did not answer.'); return; }
  render();
}

// ---- wiring -------------------------------------------------------------------------------------------------------

if (inGame) {
  s1.on('key', key);
  s1.on('refresh', function () { if (stack.length) refresh(); });
  s1.on('flash', flash);
  s1.on('cash', function (c) { cash = parseInt(c, 10) || 0; document.getElementById('cash').textContent = money(cash); render(); });
  // An emit right after the mount arrives before this script runs, so the page asks for its start state itself.
  start();
} else {
  document.addEventListener('keydown', function (ev) {
    var map = { ArrowUp: 'up', ArrowDown: 'down', Enter: 'enter', Escape: 'back', Backspace: 'back' };
    if (map[ev.key]) { ev.preventDefault(); key(map[ev.key]); }
  });
  document.body.style.background = '#3a4048 url(backdrop.png) center / cover no-repeat';
  start();
}

// ---- browser stand-in for the host (layout work outside the game) ------------------------------------------------

function browserCall(name, arg) {
  var D = window.BODYSHOP_SAMPLE || (window.BODYSHOP_SAMPLE = sample());
  if (name === 'shop.start') return JSON.stringify(D.root);
  if (name === 'shop.list') return D.cats[arg] ? JSON.stringify(D.cats[arg]) : '';
  if (name === 'shop.buy') {
    var p = arg.split('/'), cat = D.cats[p[0]];
    cat.items.forEach(function (e) {
      if (e.id !== p[1]) { if (!e.toggle && (e.group || null) === (find(cat, p[1]).group || null)) e.installed = false; return; }
      D.root.cash -= e.price;
      e.installed = e.toggle ? !e.installed : true;
      if (e.unlocked === false) { e.unlocked = true; e.price = 0; }
      setTimeout(function () { flash(e.label + ' installed.'); refresh(); }, 300);
    });
    return '';
  }
  return 'ok';
}

function find(cat, id) { for (var i = 0; i < cat.items.length; i++) if (cat.items[i].id === id) return cat.items[i]; return {}; }

function sample() {
  var stats = ['Speed', 'Acceleration', 'Braking', 'Handling'];
  function stage(id, label, price, installed, then) {
    return { id: id, label: label, price: price, installed: installed, toggle: false, text: 'Engine upgrade.', now: [0.5, 0.52, 0.45, 0.45], then: then };
  }
  return {
    root: { garage: "Marco's Bodyshop", vehicle: 'Shitbox', cash: 24850, items: [
      { id: 'paint', label: 'Paint', icon: 'i-paint', note: 'Red' },
      { id: 'looks', label: 'Lights & Looks', icon: 'i-looks', note: '' },
      { id: 'engine', label: 'Engine', icon: 'i-engine', note: 'Engine Tune 1' },
      { id: 'interior', label: 'Interior', icon: 'i-interior', locked: 'Ask Marco to make the van drivable first.' }
    ] },
    cats: {
      paint: { id: 'paint', title: 'Paint', stats: stats, items: [
        { id: 'f_red', label: 'Red', group: 'Factory', price: 100, installed: true, toggle: false, swatch: '#C8312B' },
        { id: 'f_black', label: 'Black', group: 'Factory', price: 100, installed: false, toggle: false, swatch: '#16181B' },
        { id: 'm_stock', label: 'Factory Paint', group: 'Metallic', price: 0, installed: true, toggle: false },
        { id: 'm_gold', label: 'Gold', group: 'Metallic', price: 250, installed: false, toggle: false, swatch: '#C9A13B' }
      ] },
      looks: { id: 'looks', title: 'Lights & Looks', stats: stats, items: [
        { id: 'x_stock', label: 'Stock', group: 'Xenon Headlights', price: 0, installed: true, toggle: false },
        { id: 'x_blue', label: 'Blue', group: 'Xenon Headlights', price: 375, installed: false, toggle: false, swatch: '#5AA0FF' }
      ] },
      engine: { id: 'engine', title: 'Engine', stats: stats, items: [
        stage('stock', 'Stock', 0, false, [0.45, 0.45, 0.45, 0.45]),
        stage('engine_1', 'Engine Tune 1', 500, true, [0.5, 0.52, 0.45, 0.45]),
        stage('engine_2', 'Engine Tune 2', 1250, false, [0.55, 0.6, 0.45, 0.45]),
        stage('engine_3', 'Engine Tune 3', 2500, false, [0.6, 0.69, 0.45, 0.45])
      ] }
    }
  };
}
