export const byId = (id) => document.getElementById(id);

export function element(tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  // Always textContent: model output must never be parsed as HTML.
  if (text !== undefined) node.textContent = text;
  return node;
}

export function showError(id, message) {
  const box = byId(id);
  box.textContent = message ?? "";
  box.hidden = !message;
}

const NOTICE_MS = 6000;
let noticeTimer;

export function showNotice(message) {
  const box = byId("notice");
  box.textContent = message;
  box.hidden = false;
  clearTimeout(noticeTimer);
  noticeTimer = setTimeout(() => { box.hidden = true; }, NOTICE_MS);
}

export function formatDate(value) {
  return new Date(value).toLocaleString("en-GB", { dateStyle: "medium", timeStyle: "short" });
}
