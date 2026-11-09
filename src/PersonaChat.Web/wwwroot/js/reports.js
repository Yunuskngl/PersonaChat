import { api, post } from "./api.js";
import { byId, element, formatDate, showError } from "./dom.js";

const POLL_MS = 4000;
const KIND_LABELS = { chat: "Chat", simulation: "Simulation", email: "Email" };
const STATUS_LABELS = { pending: "Preparing…", ready: "Ready", failed: "Failed" };

let pollTimer = null;

export function stopReportPolling() {
  clearTimeout(pollTimer);
  pollTimer = null;
}

function pollAgain(action) {
  stopReportPolling();
  pollTimer = setTimeout(action, POLL_MS);
}

export async function renderReports() {
  try {
    const reports = await api("/reports");
    const list = byId("report-list");
    list.replaceChildren(...reports.map(buildRow));
    if (reports.length === 0) list.append(element("p", "muted-line", "No reports yet. Finish a chat, a simulation or an email."));
    if (reports.some((report) => report.status === "pending")) pollAgain(renderReports);
  } catch (error) {
    showError("reports-error", error.message);
  }
}

function buildRow(report) {
  const row = element("a", "report-row");
  row.href = `#/reports/${report.id}`;
  row.append(
    element("span", "eyebrow", KIND_LABELS[report.kind]),
    element("span", "report-title", report.title),
    element("span", "report-date", formatDate(report.createdAt)),
    element("span", "report-status", STATUS_LABELS[report.status]),
  );
  return row;
}

export async function renderReport(id) {
  showError("report-error");
  try {
    const report = await api(`/reports/${id}`);
    const body = byId("report-body");
    body.replaceChildren(buildHeader(report), ...buildBody(report));
    if (report.status === "pending") pollAgain(() => renderReport(id));
  } catch (error) {
    showError("report-error", error.message);
  }
}

function buildHeader(report) {
  const header = element("header", "report-header");
  const line = `${KIND_LABELS[report.kind]} · ${formatDate(report.createdAt)}`;
  const level = report.content?.level ? ` · Estimated level: ${report.content.level}` : "";
  header.append(element("h1", "", report.title), element("p", "", line + level));

  if (report.status === "ready") {
    const link = element("a", "send small", "Download PDF");
    link.href = `/api/reports/${report.id}/pdf`;
    link.download = `personachat-report-${report.id}.pdf`;
    header.append(link);
  }
  return header;
}

function buildBody(report) {
  if (report.status === "pending") return [buildWaiting()];
  if (report.status === "failed") return [buildFailed(report.id)];
  return buildSections(report.content);
}

function buildWaiting() {
  const box = element("div", "report-wait");
  const dots = element("span", "typing");
  dots.append(element("i"), element("i"), element("i"));
  box.append(dots, element("p", "", "Your teacher is reading your text. You can leave this page; the report will be here."));
  return box;
}

function buildFailed(id) {
  const box = element("div", "report-wait");
  const retry = element("button", "send small", "Try again");
  retry.type = "button";
  retry.addEventListener("click", async () => {
    await post(`/reports/${id}/retry`).catch((error) => showError("report-error", error.message));
    renderReport(id);
  });
  box.append(element("p", "", "The AI model could not finish this report. Check that it is running, then try again."), retry);
  return box;
}

function section(title, ...children) {
  const node = element("section", "report-section");
  node.append(element("h2", "", title), ...children);
  return node;
}

function bulletList(items) {
  const list = element("ul");
  list.append(...items.map((item) => element("li", "", item)));
  return list;
}

function buildMistake(mistake) {
  const item = element("div", "fix");
  const line = element("p", "fix-line");
  line.append(element("s", "", mistake.original), element("span", "fix-arrow", " → "), element("strong", "", mistake.corrected));
  item.append(line, element("p", "fix-note", `${mistake.type}: ${mistake.explanation}`));
  return item;
}

function buildPhrasing(phrase) {
  const item = element("div", "fix");
  item.append(
    element("p", "", `You wrote: ${phrase.original}`),
    element("p", "strong", `A native would say: ${phrase.natural}`),
    element("p", "fix-note", phrase.why),
  );
  return item;
}

function buildWords(words) {
  const box = element("div", "words");
  box.append(...words.map((word) => {
    const chip = element("span", word.correct ? "word" : "word wrong", `${word.word} ×${word.count}`);
    if (!word.correct) chip.title = "Used incorrectly at least once";
    return chip;
  }));
  return box;
}

function buildSections(content) {
  const sections = [];
  if (content.summary) sections.push(section("Teacher's comment", element("p", "report-summary", content.summary)));

  sections.push(content.mistakes.length > 0
    ? section(`Mistakes and corrections (${content.mistakes.length})`, ...content.mistakes.map(buildMistake))
    : section("Mistakes and corrections", element("p", "", "No mistakes found. Well done.")));

  if (content.phrasing.length > 0) sections.push(section("Sound more natural", ...content.phrasing.map(buildPhrasing)));
  if (content.strengths.length > 0) sections.push(section("What you did well", bulletList(content.strengths)));
  if (content.nextSteps.length > 0) sections.push(section("What to practise next", bulletList(content.nextSteps)));
  if (content.rewrite) sections.push(section("How a native speaker would write it", element("p", "rewrite", content.rewrite)));

  sections.push(section(`Words you used (${content.totalWords} in total)`,
    buildWords(content.words),
    element("p", "fix-note", "Highlighted words were used incorrectly at least once.")));
  return sections;
}
