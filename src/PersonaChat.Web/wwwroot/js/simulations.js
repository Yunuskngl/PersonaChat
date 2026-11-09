import { api, post } from "./api.js";
import { byId, element, showError, showNotice } from "./dom.js";
import { openConversation } from "./conversation.js";

const ALL_CATEGORIES = "All categories";
const TOO_SHORT_NOTICE = "That was too short for a report. Write a little more next time.";
const REPORT_NOTICE = "Your report is being prepared. You can find it under Reports.";

let scenarios = [];
let endCurrent = null;

export async function renderSimulations() {
  try {
    if (scenarios.length === 0) {
      scenarios = await api("/simulations");
      fillCategories();
    }
    renderList();
  } catch (error) {
    showError("sim-error", error.message);
  }
}

export async function openSimulation(id) {
  if (scenarios.length === 0) scenarios = await api("/simulations");
  const scenario = scenarios.find((item) => item.id === id);
  if (!scenario) {
    location.hash = "#/simulations";
    return;
  }

  let sessionId = null;
  let ended = false;

  const end = async () => {
    if (ended || sessionId === null) return null;
    ended = true;
    return post(`/simulation-sessions/${sessionId}/end`);
  };

  endCurrent = end;
  await openConversation({
    key: `sim:${id}`,
    title: scenario.title,
    meta: `${scenario.channel} · ${scenario.counterpart}`,
    backHref: "#/simulations",
    brief: `${scenario.situation} Your goal: ${scenario.goal}`,
    placeholder: "Write your reply…",
    emptyText: "",
    load: async () => {
      const session = await post(`/simulations/${id}/session`);
      sessionId = session.sessionId;
      return session.messages;
    },
    send: (text) => post(`/simulation-sessions/${sessionId}/messages`, { text }),
    onLeave: async () => {
      const result = await end().catch(() => null);
      if (result?.reportId) showNotice(REPORT_NOTICE);
    },
  });
}

async function finishSimulation() {
  try {
    const result = await endCurrent();
    if (!result?.reportId) showNotice(TOO_SHORT_NOTICE);
    location.hash = result?.reportId ? `#/reports/${result.reportId}` : "#/simulations";
  } catch (error) {
    showError("chat-error", error.message);
  }
}

function fillCategories() {
  const categories = [ALL_CATEGORIES, ...new Set(scenarios.map((item) => item.category))];
  byId("sim-category").replaceChildren(...categories.map((name) => element("option", "", name)));
}

function visibleScenarios() {
  const category = byId("sim-category").value;
  return category === ALL_CATEGORIES ? scenarios : scenarios.filter((item) => item.category === category);
}

function renderList() {
  byId("sim-list").replaceChildren(...visibleScenarios().map(buildItem));
}

function buildItem(scenario) {
  const item = element("button", "sim-card");
  item.type = "button";
  item.addEventListener("click", () => { location.hash = `#/sim/${scenario.id}`; });
  item.append(
    element("span", "eyebrow", scenario.channel),
    element("span", "sim-title", scenario.title),
    element("span", "sim-who", scenario.counterpart),
  );
  return item;
}

function openRandom() {
  const pool = visibleScenarios();
  location.hash = `#/sim/${pool[Math.floor(Math.random() * pool.length)].id}`;
}

export function bindSimulations() {
  byId("sim-category").addEventListener("change", renderList);
  byId("sim-random").addEventListener("click", openRandom);
  byId("sim-end").addEventListener("click", finishSimulation);
}
