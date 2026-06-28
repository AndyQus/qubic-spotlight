/*
 * Qubic Spotlight – Embed-Widget
 * Eine Zeile genügt:
 *   <script src="https://DEIN-HOST/spotlight.js" data-mode="slide-panel" async></script>
 *
 * Das Widget legt sich als eigene Ebene (Shadow DOM, position:fixed) über die
 * Seite und greift nicht ins Layout ein. Es lädt die aktiven Anzeigen von der
 * öffentlichen API und zeigt sie je nach Konfiguration an.
 */
(function () {
  "use strict";

  var script = document.currentScript;
  if (!script) return;

  // Basis-URL = Herkunft des Scripts (funktioniert damit auf jeder Fremdseite).
  var base = new URL(script.src).origin;

  // Auf der eigenen Spotlight-Seite kann der Besucher das Panel über einen
  // Schalter in der Navigation an-/ausschalten. Der Zustand liegt im
  // localStorage (Standard: aktiv); ein CustomEvent hält Schalter und Widget
  // live synchron. Auf Fremdseiten ist der Schlüssel nie gesetzt → immer aktiv.
  var PANEL_KEY = "qspot_panel";
  var PANEL_EVENT = "qspot-panel-toggle";
  function panelEnabled() {
    try { return localStorage.getItem(PANEL_KEY) !== "false"; } catch (e) { return true; }
  }

  var d = script.dataset;
  var cfg = {
    mode: d.mode || "slide-panel",          // slide-panel | edge-marquee | corner-card
    position: d.position || "right",         // right | left | bottom | top
    interval: parseInt(d.interval || "5000", 10),
    speed: parseInt(d.speed || "40", 10),    // px/s bei edge-marquee
    theme: d.theme || "auto",                // auto | dark | light
    bg: d.bg || "",                          // optionaler Hintergrund (Farbe/Gradient), überschreibt Theme-Hintergrund
    max: parseInt(d.max || "10", 10),
    closable: (d.closable || "true") !== "false"
  };

  function isDark() {
    if (cfg.theme === "dark") return true;
    if (cfg.theme === "light") return false;
    return window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches;
  }

  // True nur für einen HELLEN, einfarbigen Hintergrund (#fff, helle Hex/rgb).
  // Gradienten oder dunkle Farben → false (→ weiße Schrift).
  function isLightSolid(bg) {
    if (!bg || /gradient/i.test(bg)) return false;
    var m = bg.replace(/\s/g, "").match(/^#([0-9a-f]{3}|[0-9a-f]{6})$/i);
    var r, g, b;
    if (m) {
      var h = m[1];
      if (h.length === 3) h = h[0] + h[0] + h[1] + h[1] + h[2] + h[2];
      r = parseInt(h.substr(0, 2), 16); g = parseInt(h.substr(2, 2), 16); b = parseInt(h.substr(4, 2), 16);
    } else {
      var rgb = bg.match(/rgba?\((\d+),\s*(\d+),\s*(\d+)/i);
      if (!rgb) return false;
      r = +rgb[1]; g = +rgb[2]; b = +rgb[3];
    }
    return (0.2126 * r + 0.7152 * g + 0.0722 * b) > 180;
  }

  function clickUrl(ad) { return base + "/api/ads/" + ad.id + "/click"; }

  function trackImpression(ad) {
    try {
      navigator.sendBeacon
        ? navigator.sendBeacon(base + "/api/ads/" + ad.id + "/impression")
        : fetch(base + "/api/ads/" + ad.id + "/impression", { method: "POST", keepalive: true });
    } catch (e) { /* still ok */ }
  }

  function start(ads) {
    if (!ads || !ads.length) return;
    ads = ads.slice(0, cfg.max);

    var host = document.createElement("div");
    host.id = "qubic-spotlight";
    document.body.appendChild(host);
    var root = host.attachShadow({ mode: "open" });

    var dark = isDark();
    var bg = dark ? "#1a2332" : "#ffffff";
    if (cfg.bg) bg = cfg.bg;   // wählbarer Hintergrund (Farbe oder Gradient) aus data-bg
    // Dunkle Schrift nur auf einem klar HELLEN, einfarbigen Hintergrund
    // (z. B. "Hell" #ffffff). Standard-/Qubic-Verlauf, Dark-Karte und jede
    // andere Farbe → weiße Überschrift + fast-weiße Beschreibung.
    var lightBg = isLightSolid(bg);
    var border = lightBg ? "#e2e6ee" : "rgba(255,255,255,.18)";
    var title = lightBg ? "#0d1117" : "#ffffff";
    var text = lightBg ? "#42505f" : "#f0f3f8";
    var accent = "#4EE0FC";

    var css = document.createElement("style");
    css.textContent = [
      ":host{all:initial;}",
      "*{box-sizing:border-box;font-family:Roboto,Segoe UI,system-ui,sans-serif;}",
      ".wrap{position:fixed;z-index:2147483600;}",
      ".card{background:" + bg + ";border:1px solid " + border + ";border-radius:12px;",
      "  box-shadow:0 6px 24px rgba(0,0,0,.28);overflow:hidden;}",
      ".row{display:flex;align-items:center;gap:12px;text-decoration:none;}",
      ".thumb{width:64px;height:64px;object-fit:cover;border-radius:8px;flex:0 0 auto;background:#0d1117;}",
      ".t{font-size:14px;font-weight:500;color:" + title + ";margin:0 0 3px;}",
      ".p{font-size:12px;color:" + text + ";margin:0;line-height:1.35;}",
      ".eco{display:inline-block;margin-top:5px;font-size:10px;color:#0d1117;border:1px solid rgba(13,17,23,.35);border-radius:999px;padding:1px 8px;}",
      ".close{position:absolute;top:6px;right:8px;cursor:pointer;border:none;background:none;color:" + text + ";font-size:16px;line-height:1;}",
      ".brand{font-size:10px;color:" + text + ";opacity:.7;text-align:right;padding:2px 8px;}",
      ".brand b{color:#0d1117;font-weight:600;}",
      // slide-panel
      ".panel{width:320px;padding:16px;transition:transform .5s ease;}",
      // edge-marquee
      ".bar{overflow:hidden;white-space:nowrap;padding:8px 0;}",
      ".track{display:inline-block;white-space:nowrap;will-change:transform;}",
      ".track a{display:inline-flex;align-items:center;gap:8px;margin:0 26px;text-decoration:none;}",
      ".track .t{display:inline;font-size:13px;} .track .p{display:inline;margin-left:6px;}"
    ].join("\n");
    root.appendChild(css);

    if (cfg.mode === "edge-marquee") renderMarquee(root, ads, { bg: bg, border: border });
    else if (cfg.mode === "corner-card") renderCycler(root, ads, "corner");
    else renderCycler(root, ads, "panel");
  }

  // ── slide-panel & corner-card: eine Anzeige, die alle interval ms wechselt ──
  function renderCycler(root, ads, kind) {
    var wrap = document.createElement("div");
    wrap.className = "wrap";
    place(wrap, kind === "corner" ? "corner" : cfg.position);

    var card = document.createElement("div");
    card.className = "card panel";
    if (kind === "corner") card.style.width = "280px";
    wrap.appendChild(card);
    root.appendChild(wrap);

    var i = -1;
    var seen = {};

    function show(idx) {
      var ad = ads[idx];
      card.innerHTML = "";

      if (cfg.closable) {
        var x = document.createElement("button");
        x.className = "close"; x.textContent = "×";
        x.onclick = function () {
          wrap.remove();
          // Schließen merkt sich die Entscheidung und meldet sie dem Schalter
          // in der Navigation (auf der eigenen Seite; auf Fremdseiten ohne Wirkung).
          try { localStorage.setItem(PANEL_KEY, "false"); } catch (e) { /* egal */ }
          window.dispatchEvent(new CustomEvent(PANEL_EVENT, { detail: { enabled: false, source: "widget" } }));
        };
        card.appendChild(x);
      }

      var a = document.createElement("a");
      a.className = "row"; a.href = clickUrl(ad); a.target = "_blank"; a.rel = "noopener";
      if (ad.imageUrl) {
        var img = document.createElement("img");
        img.className = "thumb"; img.src = ad.imageUrl; img.alt = "";
        // Bei nicht mehr erreichbarer/kaputter Bild-URL das Bild entfernen,
        // damit kein leerer Platzhalter (oder ein falsches Bild) erscheint.
        img.onerror = function () { img.remove(); };
        a.appendChild(img);
      }
      var box = document.createElement("div");
      box.innerHTML = "<p class='t'></p><p class='p'></p>";
      box.querySelector(".t").textContent = ad.title;
      box.querySelector(".p").textContent = ad.description;
      if (ad.ecosystem) {
        var e = document.createElement("span");
        e.className = "eco"; e.textContent = ad.ecosystem; box.appendChild(e);
      }
      a.appendChild(box);
      card.appendChild(a);

      var brand = document.createElement("div");
      brand.className = "brand"; brand.innerHTML = "powered by <b>Qubic Spotlight</b>";
      card.appendChild(brand);

      if (!seen[ad.id]) { seen[ad.id] = 1; trackImpression(ad); }
    }

    function next() { i = (i + 1) % ads.length; show(i); }
    next();
    if (ads.length > 1) setInterval(next, Math.max(2000, cfg.interval));
  }

  // ── edge-marquee: Laufband von rechts nach links ───────────────────────────
  function renderMarquee(root, ads, c) {
    var wrap = document.createElement("div");
    wrap.className = "wrap";
    wrap.style.left = "0"; wrap.style.right = "0";
    wrap.style[cfg.position === "top" ? "top" : "bottom"] = "0";

    var bar = document.createElement("div");
    bar.className = "card bar";
    bar.style.borderRadius = "0";
    bar.style.background = c.bg;
    wrap.appendChild(bar);

    var track = document.createElement("div");
    track.className = "track";
    bar.appendChild(track);

    // Inhalte doppelt anhängen für einen nahtlosen Loop.
    [0, 1].forEach(function () {
      ads.forEach(function (ad) {
        var a = document.createElement("a");
        a.href = clickUrl(ad); a.target = "_blank"; a.rel = "noopener";
        a.innerHTML = "<span class='t'></span><span class='p'></span>";
        a.querySelector(".t").textContent = ad.title;
        a.querySelector(".p").textContent = ad.description;
        track.appendChild(a);
      });
    });
    root.appendChild(wrap);
    ads.forEach(trackImpression);

    // Animation per requestAnimationFrame (gleichmäßig, performant).
    var x = 0, half = 0, last = 0;
    requestAnimationFrame(function init() { half = track.scrollWidth / 2; });
    function step(ts) {
      if (!last) last = ts;
      var dt = (ts - last) / 1000; last = ts;
      if (!half) half = track.scrollWidth / 2;
      x -= cfg.speed * dt;
      if (half && -x >= half) x += half;
      track.style.transform = "translateX(" + x + "px)";
      requestAnimationFrame(step);
    }
    requestAnimationFrame(step);
  }

  function place(wrap, position) {
    var m = "18px";
    if (position === "corner") { wrap.style.right = m; wrap.style.bottom = m; return; }
    if (position === "left") { wrap.style.left = m; wrap.style.bottom = m; }
    else if (position === "bottom") { wrap.style.left = "50%"; wrap.style.bottom = m; wrap.style.transform = "translateX(-50%)"; }
    else if (position === "top") { wrap.style.left = "50%"; wrap.style.top = m; wrap.style.transform = "translateX(-50%)"; }
    else { wrap.style.right = m; wrap.style.bottom = m; } // right (default)
  }

  // ── Laden + Auto-Resume ────────────────────────────────────────────────────
  // Das Widget rendert die Anzeigen und lädt periodisch neu. So übernimmt eine
  // gepinnte (priorisierte) Anzeige automatisch das Widget und gibt es nach
  // Ablauf (PinnedUntil) wieder frei – ohne dass der Besucher neu laden muss.
  var currentHost = null;
  var lastSig = null;
  var resumeTimer = null;

  // Signatur aus IDs + Pin-Status: nur bei echter Änderung neu rendern (kein Flackern).
  function signature(ads) {
    return (ads || []).map(function (a) {
      return a.id + (a.pinned ? ":p" : "");
    }).join("|");
  }

  function render(ads) {
    if (!panelEnabled()) { removeWidget(); return; }  // per Schalter deaktiviert
    var sig = signature(ads);
    if (sig === lastSig) return;     // nichts geändert → laufende Anzeige nicht stören
    lastSig = sig;
    if (currentHost) { currentHost.remove(); currentHost = null; }
    if (!ads || !ads.length) return;
    start(ads);
    currentHost = document.getElementById("qubic-spotlight");

    // Kurz nach Pin-Ablauf gezielt neu laden, damit wieder rotiert wird.
    if (resumeTimer) { clearTimeout(resumeTimer); resumeTimer = null; }
    var until = ads.reduce(function (min, a) {
      if (!a.pinned || !a.pinnedUntil) return min;
      var t = Date.parse(a.pinnedUntil);
      return (min === null || t < min) ? t : min;
    }, null);
    if (until !== null) {
      var ms = Math.max(1000, until - Date.now() + 500);
      resumeTimer = setTimeout(load, ms);
    }
  }

  function removeWidget() {
    if (currentHost) { currentHost.remove(); currentHost = null; }
    lastSig = null;  // beim Wieder-Aktivieren neu rendern erzwingen
  }

  function load() {
    if (!panelEnabled()) { removeWidget(); return; }  // per Schalter deaktiviert
    fetch(base + "/api/ads")
      .then(function (r) { return r.ok ? r.json() : []; })
      .then(render)
      .catch(function () { /* Fremdseite nicht stören */ });
  }

  // Schalter in der Navigation umgelegt: sofort an-/ausschalten ohne Reload.
  window.addEventListener(PANEL_EVENT, function () {
    if (panelEnabled()) load(); else removeWidget();
  });

  load();
  // Sicherheits-Poll: fängt neu aktivierte Pins / abgelaufene Fenster ab.
  setInterval(load, 60000);
})();
