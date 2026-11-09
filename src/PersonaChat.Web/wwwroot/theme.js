// Loaded before the page paints, so a saved dark theme never flashes white.
(function applySavedTheme() {
  try {
    const saved = localStorage.getItem("theme");
    if (saved) document.documentElement.dataset.theme = saved;
  } catch {
    // Storage can be blocked; the system theme is used instead.
  }
})();
