import { byId, element, showError } from "./dom.js";

// Shared chat screen. An adapter supplies the data: persona chat or simulation.
let active = null;
let pending = null; // The turn waiting for a reply: { key, text }.

export function isOpen(adapter) {
  return active === adapter;
}

export function leaveConversation() {
  if (!active) return;
  const adapter = active;
  active = null;
  adapter.onLeave?.();
}

export async function openConversation(adapter) {
  active = adapter;
  byId("chat-name").textContent = adapter.title;
  byId("chat-meta").textContent = adapter.meta;
  byId("chat-back").href = adapter.backHref;
  byId("input").placeholder = adapter.placeholder;
  byId("chat-brief").textContent = adapter.brief ?? "";
  byId("chat-brief").hidden = !adapter.brief;
  byId("log").replaceChildren();
  showError("chat-error");

  try {
    const messages = await adapter.load();
    if (active === adapter) renderLog(messages);
  } catch (error) {
    showError("chat-error", error.message);
  }
  byId("input").focus();
}

export function renderLog(messages) {
  const log = byId("log");
  log.replaceChildren();

  if (messages.length === 0 && pending?.key !== active.key) {
    log.append(element("p", "empty", active.emptyText));
    return;
  }

  messages.forEach((message) => log.append(element("div", `message ${message.role}`, message.content)));
  if (pending?.key === active.key) {
    addMessage("user", pending.text);
    showTyping();
  }
  scrollToEnd();
}

export function setPlaceholder(text) {
  byId("input").placeholder = text;
}

function scrollToEnd() {
  const log = byId("log");
  log.scrollTop = log.scrollHeight;
}

function addMessage(role, text) {
  byId("log").querySelector(".empty")?.remove();
  byId("log").append(element("div", `message ${role}`, text));
  scrollToEnd();
}

function showTyping() {
  const bubble = element("div", "message assistant");
  bubble.id = "typing";
  bubble.setAttribute("aria-label", "Typing");
  const dots = element("span", "typing");
  dots.append(element("i"), element("i"), element("i"));
  bubble.append(dots);
  byId("log").append(bubble);
  scrollToEnd();
}

function setSending(sending) {
  byId("send").disabled = sending;
  byId("reset-open").disabled = sending;
  byId("sim-end").disabled = sending;
}

function resizeInput() {
  const input = byId("input");
  input.style.height = "auto";
  input.style.height = `${input.scrollHeight + 2}px`;
}

function finishTurn(key) {
  pending = null;
  setSending(false);
  if (active?.key === key) byId("typing")?.remove();
}

async function sendMessage(event) {
  event.preventDefault();
  const input = byId("input");
  const text = input.value.trim();
  if (!text || pending || !active) return;

  const { key, send } = active;
  pending = { key, text };
  setSending(true);
  showError("chat-error");
  input.value = "";
  resizeInput();
  addMessage("user", text);
  showTyping();

  try {
    const { reply } = await send(text);
    finishTurn(key);
    if (active?.key === key) addMessage("assistant", reply);
  } catch (error) {
    finishTurn(key);
    if (active?.key === key) restoreFailedText(text, error.message);
  }
}

// The failed turn was not saved, so the text goes back into the box for a retry.
function restoreFailedText(text, message) {
  [...byId("log").querySelectorAll(".message.user")].pop()?.remove();
  byId("input").value = text;
  resizeInput();
  showError("chat-error", message);
  if (!byId("log").querySelector(".message")) renderLog([]);
}

function handleInputKey(event) {
  if (event.key !== "Enter" || event.shiftKey || event.isComposing) return;
  event.preventDefault();
  byId("composer").requestSubmit();
}

export function bindConversation(maxInputLength) {
  byId("input").maxLength = maxInputLength;
  byId("composer").addEventListener("submit", sendMessage);
  byId("input").addEventListener("input", resizeInput);
  byId("input").addEventListener("keydown", handleInputKey);
}
