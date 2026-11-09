import { api, post } from "./api.js";
import { byId, element, showError, showNotice } from "./dom.js";
import { openConversation, renderLog, setPlaceholder } from "./conversation.js";

const REPORT_NOTICE = "Your report is being prepared. You can find it under Reports.";

let personas = [];
let mode = "chat";
let current = null;

export async function loadPersonas() {
  personas = await api("/personas");
}

export function findPersona(id) {
  return personas.find((persona) => persona.id === id);
}

export function renderHome() {
  const words = searchWords(byId("persona-search").value);
  const found = personas.filter((persona) => matches(persona, words));
  byId("cards").replaceChildren(...found.map(buildCard));
  byId("cards-empty").hidden = found.length > 0;
}

// Lets "izmir" find "İzmir" and "aydin" find "Aydın".
function simplify(text) {
  return text.toLowerCase().normalize("NFD").replace(/\p{M}/gu, "").replace(/ı/g, "i");
}

function searchWords(query) {
  return simplify(query).split(/\s+/).filter(Boolean);
}

function matches(persona, words) {
  const { name, location, backstory, personality, expertise } = persona;
  const text = simplify([name, location, backstory, personality, ...expertise].join(" "));
  return words.every((word) => text.includes(word));
}

export function openPersonaChat(persona) {
  current = persona;
  renderMode();
  return openConversation({
    key: `persona:${persona.id}`,
    title: persona.name,
    meta: `${persona.age} · ${persona.location.split(",").pop().trim()}`,
    backHref: "#/",
    placeholder: placeholder(),
    emptyText: `You haven't met ${persona.name} yet. Say hello.`,
    load: () => api(`/personas/${persona.id}/messages`),
    send: (text) => post(`/personas/${persona.id}/messages`, { text, mode }),
    onLeave: () => endChat(persona.id),
  });
}

async function endChat(id) {
  const result = await post(`/personas/${id}/end`).catch(() => null);
  if (result?.reportId) showNotice(REPORT_NOTICE);
}

function initials(name) {
  return name.replace(/^Dr\.\s*/, "").split(/\s+/).map((part) => part[0]).join("").slice(0, 2);
}

function buildCard(persona) {
  const card = element("button", "card");
  card.type = "button";
  card.addEventListener("click", () => { location.hash = `#/chat/${persona.id}`; });

  const top = element("div", "card-top");
  top.append(element("span", "monogram", initials(persona.name)), element("span", "age", String(persona.age)));

  const tags = element("div", "card-tags");
  persona.expertise.forEach((item) => tags.append(element("span", "", item)));

  card.append(
    top,
    element("div", "card-name", persona.name),
    element("div", "card-place", persona.location),
    element("p", "card-role", persona.personality),
    tags,
  );
  return card;
}

function placeholder() {
  return mode === "ask" ? "Ask an expert question…" : "Write in English…";
}

function renderMode() {
  document.querySelectorAll("[data-mode]").forEach((button) => {
    button.setAttribute("aria-pressed", String(button.dataset.mode === mode));
  });
  setPlaceholder(placeholder());
}

async function openMemory() {
  byId("memory-name").textContent = current.name;
  byId("memory-list").replaceChildren();
  byId("memory-empty").hidden = true;
  byId("memory-dialog").showModal();

  try {
    const notes = await api(`/personas/${current.id}/memory`);
    byId("memory-list").replaceChildren(...notes.map((note) => element("li", "", note)));
    byId("memory-empty").hidden = notes.length > 0;
  } catch (error) {
    byId("memory-dialog").close();
    showError("chat-error", error.message);
  }
}

async function confirmReset() {
  byId("reset-dialog").close();
  showError("chat-error");
  try {
    await api(`/personas/${current.id}/messages`, { method: "DELETE" });
    renderLog([]);
  } catch (error) {
    showError("chat-error", error.message);
  }
}

export function bindPersonaTools() {
  byId("persona-search").addEventListener("input", renderHome);

  document.querySelectorAll("[data-mode]").forEach((button) => {
    button.addEventListener("click", () => {
      mode = button.dataset.mode;
      renderMode();
    });
  });

  byId("memory-open").addEventListener("click", openMemory);
  byId("memory-close").addEventListener("click", () => byId("memory-dialog").close());
  byId("reset-open").addEventListener("click", () => byId("reset-dialog").showModal());
  byId("reset-cancel").addEventListener("click", () => byId("reset-dialog").close());
  byId("reset-confirm").addEventListener("click", confirmReset);
}
