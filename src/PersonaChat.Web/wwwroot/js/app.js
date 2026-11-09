import { api } from "./api.js";
import { byId, showError } from "./dom.js";
import { bindConversation, leaveConversation } from "./conversation.js";
import { bindEmail } from "./email.js?v=2";
import { bindPersonaTools, findPersona, loadPersonas, openPersonaChat, renderHome } from "./personas.js?v=2";
import { renderReport, renderReports, stopReportPolling } from "./reports.js?v=2";
import { bindSimulations, openSimulation, renderSimulations } from "./simulations.js";

const VIEWS = ["home", "simulations", "email", "reports", "report", "chat"];

function showView(name, nav) {
  VIEWS.forEach((view) => { byId(`view-${view}`).hidden = view !== name; });
  document.body.classList.toggle("in-chat", name === "chat");
  document.querySelectorAll("[data-nav]").forEach((link) => {
    if (link.dataset.nav === nav) link.setAttribute("aria-current", "page");
    else link.removeAttribute("aria-current");
  });
  window.scrollTo(0, 0);
}

function showTools(kind) {
  byId("tools-persona").hidden = kind !== "persona";
  byId("tools-sim").hidden = kind !== "sim";
}

function routeChat(id) {
  const persona = findPersona(id);
  if (!persona) {
    location.hash = "#/";
    return;
  }
  showView("chat", "home");
  showTools("persona");
  document.title = `${persona.name} · PersonaChat`;
  openPersonaChat(persona);
}

function routeSimulation(id) {
  showView("chat", "simulations");
  showTools("sim");
  document.title = "Simulation · PersonaChat";
  openSimulation(id);
}

function routeHome() {
  showView("home", "home");
  document.title = "PersonaChat";
  renderHome();
}

function routeList(view, title, render) {
  showView(view, view);
  document.title = `${title} · PersonaChat`;
  render();
}

function route() {
  leaveConversation();
  stopReportPolling();

  const [page, id] = location.hash.replace(/^#\/?/, "").split("/");
  if (page === "chat" && id) routeChat(id);
  else if (page === "sim" && id) routeSimulation(id);
  else if (page === "simulations") routeList("simulations", "Simulations", renderSimulations);
  else if (page === "email") routeList("email", "Email wheel", () => {});
  else if (page === "reports" && id) { showView("report", "reports"); document.title = "Report · PersonaChat"; renderReport(id); }
  else if (page === "reports") routeList("reports", "Reports", renderReports);
  else routeHome();
}

function currentTheme() {
  const explicit = document.documentElement.dataset.theme;
  if (explicit) return explicit;
  return matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

function renderThemeLabel() {
  byId("theme-toggle").textContent = currentTheme() === "dark" ? "Light" : "Dark";
}

function toggleTheme() {
  const next = currentTheme() === "dark" ? "light" : "dark";
  document.documentElement.dataset.theme = next;
  try {
    localStorage.setItem("theme", next);
  } catch {
    // The choice just won't survive a reload.
  }
  renderThemeLabel();
}

async function start() {
  renderThemeLabel();
  byId("theme-toggle").addEventListener("click", toggleTheme);
  bindPersonaTools();
  bindSimulations();

  try {
    const [settings] = await Promise.all([api("/settings"), loadPersonas()]);
    bindConversation(settings.maxInputLength);
    bindEmail(settings.maxEmailLength);
    window.addEventListener("hashchange", route);
    window.addEventListener("pagehide", leaveConversation);
    // Tells the packaged app that a browser tab is still open.
    setInterval(() => fetch("/api/ping").catch(() => {}), 30000);
    route();
  } catch (error) {
    byId("view-home").hidden = false;
    showError("home-error", error.message);
  }
}

start();
