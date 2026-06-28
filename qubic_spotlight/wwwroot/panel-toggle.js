/*
 * Brücke zwischen dem Navigations-Schalter (Blazor) und dem Slide-Panel-Widget
 * (spotlight.js, Shadow DOM). Beide tauschen sich über localStorage + ein
 * CustomEvent ("qspot-panel-toggle") aus, damit Schalter und Panel ohne Reload
 * synchron bleiben.
 */
window.qspotPanel = {
  KEY: "qspot_panel",
  EVENT: "qspot-panel-toggle",

  // Liest den gemerkten Zustand (Standard: aktiv).
  enabled: function () {
    try { return localStorage.getItem(this.KEY) !== "false"; } catch (e) { return true; }
  },

  // Setzt den Zustand (vom Schalter) und meldet ihn dem Widget.
  set: function (on) {
    try { localStorage.setItem(this.KEY, on ? "true" : "false"); } catch (e) { /* egal */ }
    window.dispatchEvent(new CustomEvent(this.EVENT, { detail: { enabled: on, source: "switch" } }));
  },

  // Registriert einen .NET-Callback, der bei jeder Zustandsänderung (auch durch
  // den Schließen-Button im Panel) den Schalter nachzieht.
  register: function (dotNet) {
    var self = this;
    this._handler = function () { dotNet.invokeMethodAsync("OnPanelToggled", self.enabled()); };
    window.addEventListener(this.EVENT, this._handler);
  },

  unregister: function () {
    if (this._handler) { window.removeEventListener(this.EVENT, this._handler); this._handler = null; }
  }
};
