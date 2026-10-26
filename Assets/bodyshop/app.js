// Marco's Bodyshop - garage menu mockup.
//
// One list at a time, Benny's style: categories first, Enter opens one, Enter on an entry buys it, Escape goes back
// up. The arrow keys come from the mod as 'key' events (the page only sees keys inside a focused field); in a
// browser the document's own keydown stands in. The mouse picks a row by hovering and buys it by clicking.

var inGame = typeof s1 !== 'undefined';
var VISIBLE = 9;

var COLOURS = [
  ['Black', '#16181B'], ['Dark Grey', '#3C4046'], ['Light Grey', '#9A9FA6'], ['White', '#ECEDEF'],
  ['Yellow', '#E8C23A'], ['Orange', '#E57D2A'], ['Red', '#C8312B'], ['Dull Red', '#7E2E2A'],
  ['Pink', '#E07FA8'], ['Purple', '#6B3FA0'], ['Navy', '#1F2A4A'], ['Dark Blue', '#22408A'],
  ['Light Blue', '#7FB4E3'], ['Cyan', '#3FC4C9'], ['Light Green', '#8ED36B'], ['Dark Green', '#2E5A34']
];

function paint(current) {
  return COLOURS.map(function (c) {
    return { label: c[0], swatch: c[1], price: 100, owned: c[0] === current, text: 'Vanilla paint.' };
  });
}

function stages(slot, names, prices, gains, owned) {
  var items = [{ label: 'Stock', price: 0, owned: owned === 0, stats: gains[0], text: 'Back to stock. Free, no refund.' }];
  for (var i = 0; i < names.length; i++)
    items.push({ label: names[i], price: prices[i], owned: owned === i + 1, stats: gains[i + 1], text: slot + ' upgrade.' });
  return items;
}

var STAT_BASE = { Speed: 0.46, Acceleration: 0.38, Braking: 0.42, Handling: 0.5 };
function gain(sp, ac, br, ha) { return { Speed: sp, Acceleration: ac, Braking: br, Handling: ha }; }

var CAR = {
  name: 'Shitbox', cash: 24850,
  menu: { title: 'Shitbox', items: [
    { label: 'Paint', icon: 'i-paint', note: 'Red', menu: { title: 'Paint', items: paint('Red') } },
    { label: 'Lights & Looks', icon: 'i-looks', note: 'Stock', menu: { title: 'Lights & Looks', items: [
      { label: 'Xenon Headlights', price: 375, text: 'Coloured headlight glow.' },
      { label: 'Underglow', price: 375, text: 'Neon under the body, all four sides.' },
      { label: 'Window Tint', price: 250, owned: true, text: 'Darker glass.' },
      { label: 'Rim Colour', price: 250, text: 'Painted rims.' },
      { label: 'Tyre Smoke', price: 125, text: 'Coloured drift smoke.' }
    ] } },
    { label: 'Engine', icon: 'i-engine', note: 'Tune 1', menu: { title: 'Engine', items: stages('Engine',
      ['Engine Tune 1', 'Engine Tune 2', 'Engine Tune 3'], [500, 1250, 2500],
      [gain(0, 0), gain(0.03, 0.06), gain(0.06, 0.12), gain(0.1, 0.2)], 1) } },
    { label: 'Brakes', icon: 'i-brakes', note: 'Stock', menu: { title: 'Brakes', items: stages('Brakes',
      ['Street Brakes', 'Sport Brakes', 'Race Brakes'], [500, 1000, 2000],
      [gain(0, 0, 0), gain(0, 0, 0.1), gain(0, 0, 0.2), gain(0, 0, 0.3)], 0) } },
    { label: 'Transmission', icon: 'i-gears', note: 'Stock', menu: { title: 'Transmission', items: stages('Transmission',
      ['Street Transmission', 'Sport Transmission', 'Race Transmission'], [500, 1250, 2500],
      [gain(0, 0), gain(0.04, 0.03), gain(0.08, 0.06), gain(0.12, 0.09)], 0) } },
    { label: 'Suspension', icon: 'i-susp', note: 'Stock', menu: { title: 'Suspension', items: stages('Suspension',
      ['Lowered Suspension', 'Street Suspension', 'Sport Suspension', 'Competition Suspension'], [500, 1000, 1750, 2500],
      [gain(0, 0, 0, 0), gain(0, 0, 0, 0.05), gain(0, 0, 0, 0.1), gain(0, 0, 0, 0.15), gain(0, 0, 0, 0.2)], 0) } },
    { label: 'Turbo', icon: 'i-turbo', note: 'None', menu: { title: 'Turbo', items: [
      { label: 'None', price: 0, owned: true, stats: gain(0, 0), text: 'Back to stock. Free, no refund.' },
      { label: 'Turbo Tuning', price: 3750, stats: gain(0.05, 0.14), text: 'Turbo upgrade.' }
    ] } },
    { label: 'Tyres', icon: 'i-tyres', note: 'Stock', menu: { title: 'Tyres', items: [
      { label: 'Stock', price: 0, owned: true, stats: gain(0, 0, 0, 0), text: 'Back to stock. Free, no refund.' },
      { label: 'Drift Tyres', price: 750, stats: gain(0, 0, 0, -0.15), text: 'Less grip, longer slides.' }
    ] } }
  ] }
};

function part(label, price, unlocked, installed, extra) {
  var p = { label: label, price: price, unlocked: unlocked, installed: installed, interior: true };
  for (var k in extra || {}) p[k] = extra[k];
  return p;
}

var RV = {
  name: 'RV', cash: 24850,
  menu: { title: 'RV', items: [
    { label: 'Paint', icon: 'i-paint', note: 'White', menu: { title: 'Paint', items: paint('White') } },
    { label: 'Lights & Looks', icon: 'i-looks', note: 'Stock', menu: { title: 'Lights & Looks', items: [
      { label: 'Xenon Headlights', price: 375, text: 'Coloured headlight glow.' },
      { label: 'Underglow', price: 375, text: 'Neon under the body, all four sides.' }
    ] } },
    { label: 'Interior', icon: 'i-interior', note: '6 of 8 fitted', menu: { title: 'Interior', items: [
      part('Bed', 450, false, true),
      part('Kitchen Bench', 400, false, true),
      part('Upper Cabinets', 300, true, false),
      part('Lower Cabinets', 350, false, true),
      part('Bedroom Wall', 250, false, true),
      part('Wall Behind Driver', 250, false, true),
      part('Bedside Table', 150, true, false, { blocked: true }),
      part('Decor', 350, false, true)
    ] } },
    { label: 'Workshop', icon: 'i-workshop', note: 'Not built', menu: { title: 'Workshop', items: [
      { label: 'Workshop Floor', price: 7500, text: 'Lets you build stations on every cleared floor space.' }
    ] } },
    { label: 'Engine', icon: 'i-engine', note: 'Locked', locked: true, text: 'Ask Marco to make the van drivable first.' },
    { label: 'Brakes', icon: 'i-brakes', note: 'Locked', locked: true, text: 'Ask Marco to make the van drivable first.' }
  ] }
};

var VEHICLES = { car: CAR, rv: RV };
var vehicle = CAR;
var stack = [];            // [{ menu, pick, top }]
var cash = 0;

function el(tag, cls, text) {
  var e = document.createElement(tag);
  if (cls) e.className = cls;
  if (text !== undefined) e.textContent = text;
  return e;
}

function money(n) {
  var s = String(Math.abs(n)), out = '';
  while (s.length > 3) { out = ',' + s.slice(-3) + out; s = s.slice(0, -3); }
  return (n < 0 ? '-$' : '$') + s + out;
}

function level() { return stack[stack.length - 1]; }

function priceOf(item) {
  if (item.interior) return item.unlocked ? 0 : item.price;
  return item.price || 0;
}

function rightSide(row, item) {
  if (item.menu || item.locked) {
    if (item.locked) { var lk = el('img', 'ico'); lk.setAttribute('src', 'i-lock.png'); row.appendChild(lk); }
    else if (item.note) row.appendChild(el('span', 'note', item.note));
    return;
  }
  if (item.interior) {
    row.appendChild(el('span', 'note', item.installed ? 'Fitted' : 'Removed'));
    var p = priceOf(item);
    row.appendChild(el('span', 'price', p === 0 ? 'Free' : money(p)));
    return;
  }
  if (item.owned) {
    var o = el('div', 'owned');
    var ck = el('img'); ck.setAttribute('src', 'i-check.png');
    o.appendChild(ck); o.appendChild(el('span', '', 'Installed'));
    row.appendChild(o);
    return;
  }
  var price = priceOf(item);
  var tag = el('span', 'price' + (price > cash ? ' short' : ''), price === 0 ? 'Free' : money(price));
  row.appendChild(tag);
}

function render() {
  var lv = level();
  var items = lv.menu.items;
  if (lv.pick < lv.top) lv.top = lv.pick;
  if (lv.pick >= lv.top + VISIBLE) lv.top = lv.pick - VISIBLE + 1;

  document.getElementById('crumb').textContent = lv.menu.title;
  document.getElementById('count').textContent = (lv.pick + 1) + ' / ' + items.length;
  document.getElementById('cash').textContent = money(cash);
  document.getElementById('vehicle').textContent = vehicle.name;

  var list = document.getElementById('list');
  list.replaceChildren();
  if (lv.top > 0) list.appendChild(el('div', 'more', 'more above'));
  var end = Math.min(items.length, lv.top + VISIBLE);
  for (var i = lv.top; i < end; i++) list.appendChild(row(items[i], i, i === lv.pick));
  if (end < items.length) list.appendChild(el('div', 'more', 'more below'));

  detail(items[lv.pick]);
}

function row(item, index, picked) {
  var r = el('div', 'row' + (picked ? ' pick' : '') + (item.locked ? ' locked' : ''));
  if (item.icon) {
    var ic = el('img', 'ico'); ic.setAttribute('src', item.icon + '.png');
    // The game tints a glyph through `color`; a browser does not, so the white glyph is inverted on the light row.
    if (!inGame && picked) ic.style.filter = 'invert(1)';
    r.appendChild(ic);
  }
  if (item.swatch) { var sw = el('div', 'swatch'); sw.style.background = item.swatch; r.appendChild(sw); }
  r.appendChild(el('span', 'label', item.label));
  rightSide(r, item);
  r.addEventListener('mouseenter', function () {
    if (inGame) s1.call('mock.hover', item.label);
    if (level().pick !== index) { level().pick = index; render(); }
  });
  r.addEventListener('click', function () {
    if (inGame) s1.call('mock.click', item.label);
    level().pick = index;
    enter();
  });
  r.addEventListener('wheel', function (e) {
    var d = inGame ? e.wheelDelta : (e.deltaY > 0 ? 1 : -1);
    move(d > 0 ? 1 : -1);
  });
  return r;
}

function detail(item) {
  var d = document.getElementById('detail');
  d.replaceChildren();
  if (item.menu) { d.style.display = 'none'; return; }
  d.style.display = 'flex';

  if (item.interior) {
    if (item.installed) d.appendChild(el('div', 'text', item.unlocked
      ? 'Unlocked. Removing and refitting is free.'
      : 'Pay once to unlock. Removing and refitting is free after that.'));
    else if (item.blocked) d.appendChild(el('div', 'warn', 'Clear the floor here to refit it.'));
    else d.appendChild(el('div', 'text', 'Unlocked. Refitting is free.'));
    return;
  }

  d.appendChild(el('div', item.locked ? 'warn' : 'text', item.text || ''));
  if (!item.stats) return;
  var current = currentStats();
  for (var name in STAT_BASE) {
    var now = STAT_BASE[name] + (current[name] || 0);
    var then = STAT_BASE[name] + (item.stats[name] || 0);
    var s = el('div', 'stat');
    s.appendChild(el('span', 'name', name));
    var t = el('div', 'track');
    var f = el('div', 'fill'); f.style.width = Math.round(Math.min(now, then) * 100) + '%';
    t.appendChild(f);
    if (then > now) { var g = el('div', 'gain'); g.style.width = Math.round((then - now) * 100) + '%'; t.appendChild(g); }
    s.appendChild(t);
    d.appendChild(s);
  }
}

function currentStats() {
  var items = level().menu.items;
  for (var i = 0; i < items.length; i++) if (items[i].owned && items[i].stats) return items[i].stats;
  return {};
}

function move(step) {
  var lv = level();
  var n = lv.menu.items.length;
  lv.pick = (lv.pick + step + n) % n;
  render();
}

function enter() {
  var item = level().menu.items[level().pick];
  if (item.locked) return;
  if (item.menu) { stack.push({ menu: item.menu, pick: ownedIndex(item.menu), top: 0 }); render(); return; }

  var price = priceOf(item);
  if (price > cash) { flash('Not enough money.'); return; }
  cash -= price;
  if (item.interior) {
    if (!item.installed && item.blocked) { cash += price; flash('Clear the floor here to refit it.'); return; }
    item.unlocked = true;
    item.installed = !item.installed;
    flash(item.label + (item.installed ? ' fitted.' : ' removed.') + (price ? ' ' + money(price) : ''));
  } else {
    var items = level().menu.items;
    for (var i = 0; i < items.length; i++) items[i].owned = false;
    item.owned = true;
    flash(item.label + ' installed.' + (price ? ' ' + money(price) : ''));
  }
  if (inGame) s1.call('mock.bought', item.label);
  render();
}

function ownedIndex(menu) {
  for (var i = 0; i < menu.items.length; i++) if (menu.items[i].owned) return i;
  return 0;
}

function back() {
  if (stack.length > 1) { stack.pop(); render(); return true; }
  if (inGame) s1.call('mock.close', '');
  return false;
}

var flashTimer = null;
function flash(text) {
  var f = document.getElementById('flash');
  f.textContent = text;
  f.classList.add('on');
  if (flashTimer) clearTimeout(flashTimer);
  flashTimer = setTimeout(function () { f.classList.remove('on'); }, 2200);
}

function key(k) {
  if (k === 'up') move(-1);
  else if (k === 'down') move(1);
  else if (k === 'enter') enter();
  else if (k === 'back') back();
}

// Open a vehicle at a path of picks: show('rv', [2, 1]) opens the RV, enters Interior and picks the second part.
function show(which, path) {
  vehicle = VEHICLES[which] || CAR;
  cash = vehicle.cash;
  stack = [{ menu: vehicle.menu, pick: 0, top: 0 }];
  path = path || [];
  for (var i = 0; i < path.length; i++) {
    var lv = level();
    lv.pick = path[i];
    if (i < path.length - 1) { var it = lv.menu.items[path[i]]; stack.push({ menu: it.menu, pick: 0, top: 0 }); }
  }
  render();
}

if (inGame) {
  s1.on('key', function (k) { key(k); s1.call('mock.seen', k); });
  var showArg = function (arg) {
    var parts = String(arg).split(' ');
    show(parts[0], parts.slice(1).map(function (n) { return parseInt(n, 10); }));
  };
  s1.on('show', showArg);
  // The host's first 'show' can fire before this script runs, so the page asks once.
  showArg(s1.call('mock.start', '') || 'car');
} else {
  document.addEventListener('keydown', function (e) {
    var map = { ArrowUp: 'up', ArrowDown: 'down', Enter: 'enter', Escape: 'back', Backspace: 'back' };
    if (map[e.key]) { e.preventDefault(); key(map[e.key]); }
  });
  var q = (location.hash || '#car').slice(1).split(',');
  document.body.style.background = '#3a4048 url(backdrop.png) center / cover no-repeat';
  show(q[0], q.slice(1).map(function (n) { return parseInt(n, 10); }));
}

