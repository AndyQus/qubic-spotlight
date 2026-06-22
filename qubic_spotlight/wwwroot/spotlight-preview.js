/*
 * Qubic Spotlight – Vorschau für die Admin-Seite (/admin/embed).
 *
 * Rendert das Widget in einen übergebenen Container (statt fixiert über die
 * ganze Seite) und kann aus Blazor jederzeit mit neuer Konfiguration neu
 * aufgebaut werden – so spiegelt die Vorschau die Einstellungen live wider.
 *
 * Bewusst getrennt vom produktiven spotlight.js, damit der Embed-Pfad schlank
 * und unverändert bleibt.
 */
window.qubicSpotlightPreview = (function () {
  "use strict";

  function clickUrl(base, ad) { return base + "/api/ads/" + ad.id + "/click"; }

  function isDark(theme) {
    if (theme === "dark") return true;
    if (theme === "light") return false;
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
    // Relative Helligkeit (0–255); > 180 gilt als hell.
    return (0.2126 * r + 0.7152 * g + 0.0722 * b) > 180;
  }

  // cfg: { baseUrl, mode, position, interval, theme, bg, max, closable }
  function render(containerId, cfg) {
    var container = document.getElementById(containerId);
    if (!container) return;
    container.innerHTML = "";

    var base = cfg.baseUrl || window.location.origin;

    fetch(base + "/api/ads")
      .then(function (r) { return r.ok ? r.json() : []; })
      .then(function (ads) { build(container, base, cfg, ads || []); })
      .catch(function () { /* still */ });
  }

  function build(container, base, cfg, ads) {
    ads = ads.slice(0, parseInt(cfg.max || 10, 10));
    if (!ads.length) {
      container.innerHTML =
        "<div style='padding:24px;text-align:center;color:#90a0b5;font-size:13px;'>Keine aktiven Anzeigen.</div>";
      return;
    }

    var dark = isDark(cfg.theme);
    var bg = dark ? "#1a2332" : "#ffffff";
    if (cfg.bg) bg = cfg.bg;
    // Dunkle Schrift nur auf einem klar HELLEN, einfarbigen Hintergrund
    // (z. B. "Hell" #ffffff). Standard-/Qubic-Verlauf, Dark-Karte und jede
    // andere Farbe → weiße Überschrift + fast-weiße Beschreibung.
    var lightBg = isLightSolid(bg);
    var border = lightBg ? "#e2e6ee" : "rgba(255,255,255,.18)";
    var title = lightBg ? "#0d1117" : "#ffffff";
    var text = lightBg ? "#42505f" : "#f0f3f8";
    var accent = "#4EE0FC";

    // Eigener Shadow-Root, damit die Admin-Styles die Vorschau nicht beeinflussen.
    var hostEl = document.createElement("div");
    container.appendChild(hostEl);
    var root = hostEl.attachShadow({ mode: "open" });

    var css = document.createElement("style");
    css.textContent = [
      "*{box-sizing:border-box;font-family:Roboto,Segoe UI,system-ui,sans-serif;}",
      ".card{background:" + bg + ";border:1px solid " + border + ";border-radius:12px;",
      "  box-shadow:0 6px 24px rgba(0,0,0,.18);overflow:hidden;position:relative;}",
      ".row{display:flex;align-items:center;gap:12px;text-decoration:none;padding:0;}",
      ".thumb{width:64px;height:64px;object-fit:cover;border-radius:8px;flex:0 0 auto;background:#0d1117;}",
      ".t{font-size:14px;font-weight:500;color:" + title + ";margin:0 0 3px;}",
      ".p{font-size:12px;color:" + text + ";margin:0;line-height:1.35;}",
      ".eco{display:inline-block;margin-top:5px;font-size:10px;color:#0d1117;border:1px solid rgba(13,17,23,.35);border-radius:999px;padding:1px 8px;}",
      ".close{position:absolute;top:6px;right:8px;cursor:pointer;border:none;background:none;color:" + text + ";font-size:16px;line-height:1;}",
      ".brand{font-size:10px;color:" + text + ";opacity:.7;text-align:right;padding:2px 8px;}",
      ".brand b{color:#0d1117;font-weight:600;}",
      ".panel{width:320px;max-width:100%;padding:16px;}",
      ".bar{overflow:hidden;white-space:nowrap;padding:8px 0;}",
      ".track{display:inline-flex;gap:26px;white-space:nowrap;}",
      ".track a{display:inline-flex;align-items:center;gap:8px;text-decoration:none;}",
      ".track .t{display:inline;font-size:13px;} .track .p{display:inline;margin-left:6px;}"
    ].join("\n");
    root.appendChild(css);

    if (cfg.mode === "edge-marquee") buildMarquee(root, base, cfg, ads);
    else buildCycler(root, base, cfg, ads);
  }

  function adNode(base, ad, closable) {
    var card = document.createElement("div");
    card.className = "card panel";

    if (closable) {
      var x = document.createElement("button");
      x.className = "close"; x.textContent = "×";
      x.onclick = function () { card.style.display = "none"; };
      card.appendChild(x);
    }

    var a = document.createElement("a");
    a.className = "row"; a.href = clickUrl(base, ad); a.target = "_blank"; a.rel = "noopener";
    if (ad.imageUrl) {
      var img = document.createElement("img");
      img.className = "thumb"; img.src = ad.imageUrl; img.alt = "";
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
    return card;
  }

  function buildCycler(root, base, cfg, ads) {
    var card = adNode(base, ads[0], cfg.closable);
    root.appendChild(card);

    if (ads.length > 1) {
      var i = 0;
      var interval = Math.max(2000, parseInt(cfg.interval || 5000, 10));
      var timer = setInterval(function () {
        if (!root.host || !root.host.isConnected) { clearInterval(timer); return; }
        i = (i + 1) % ads.length;
        var fresh = adNode(base, ads[i], cfg.closable);
        root.replaceChild(fresh, card);
        card = fresh;
      }, interval);
    }
  }

  function buildMarquee(root, base, cfg, ads) {
    var bar = document.createElement("div");
    bar.className = "card bar"; bar.style.borderRadius = "8px";
    if (cfg.bg) bar.style.background = cfg.bg;
    var track = document.createElement("div"); track.className = "track";
    ads.forEach(function (ad) {
      var a = document.createElement("a");
      a.href = clickUrl(base, ad); a.target = "_blank"; a.rel = "noopener";
      a.innerHTML = "<span class='t'></span><span class='p'></span>";
      a.querySelector(".t").textContent = ad.title;
      a.querySelector(".p").textContent = ad.description;
      track.appendChild(a);
    });
    bar.appendChild(track);
    root.appendChild(bar);
  }

  return { render: render };
})();
