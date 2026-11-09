import { api, post } from "./api.js";
import { byId, showError } from "./dom.js";

const SVG_NS = "http://www.w3.org/2000/svg";
const RADIUS = 97;
const FULL_TURNS = 5;
const SPIN_MS = 4200;
const SLOT_COUNT = 100;
const WEDGE_CLASSES = ["wedge on", "wedge", "wedge hot", "wedge"];

let rotation = 0;
let scenario = null;
let spinning = false;

function point(degrees) {
  const radians = ((degrees - 90) * Math.PI) / 180;
  return `${(RADIUS * Math.cos(radians)).toFixed(3)} ${(RADIUS * Math.sin(radians)).toFixed(3)}`;
}

function buildWedge(index, step) {
  const wedge = document.createElementNS(SVG_NS, "path");
  wedge.setAttribute("d", `M0 0 L${point(index * step)} A${RADIUS} ${RADIUS} 0 0 1 ${point((index + 1) * step)} Z`);
  wedge.setAttribute("class", WEDGE_CLASSES[index % WEDGE_CLASSES.length]);
  return wedge;
}

function buildWheel() {
  const step = 360 / SLOT_COUNT;
  const wedges = Array.from({ length: SLOT_COUNT }, (_, index) => buildWedge(index, step));
  byId("wheel").replaceChildren(...wedges);
}

function reducedMotion() {
  return matchMedia("(prefers-reduced-motion: reduce)").matches;
}

function rotateTo(slot, count) {
  const target = 360 - ((slot + 0.5) * 360) / count;
  const distance = ((target - (rotation % 360)) + 360) % 360;
  rotation += 360 * FULL_TURNS + distance;
  byId("wheel").style.transform = `rotate(${rotation}deg)`;
  return new Promise((resolve) => setTimeout(resolve, reducedMotion() ? 0 : SPIN_MS));
}

function showScenario(result) {
  scenario = result.scenario;
  byId("wheel-number").textContent = String(result.slot + 1);
  byId("email-category").textContent = scenario.category;
  byId("email-title").textContent = scenario.title;
  byId("email-situation").textContent = scenario.situation;
  byId("email-task").textContent = scenario.task;
  byId("email-to").textContent = scenario.to;
  byId("email-subject").value = "";
  byId("email-body").value = "";
  byId("email-form").hidden = false;
  byId("email-subject").focus();
}

async function spin() {
  if (spinning) return;
  spinning = true;
  byId("wheel-spin").disabled = true;
  byId("email-form").hidden = true;
  byId("wheel-number").textContent = "…";
  showError("email-error");

  try {
    const result = await api("/emails/spin");
    await rotateTo(result.slot, result.slotCount);
    showScenario(result);
  } catch (error) {
    byId("wheel-number").textContent = "?";
    byId("email-form").hidden = false;
    showError("email-error", error.message);
  } finally {
    spinning = false;
    byId("wheel-spin").disabled = false;
  }
}

async function submit(event) {
  event.preventDefault();
  showError("email-error");
  byId("email-send").disabled = true;

  try {
    const { reportId } = await post(`/emails/${scenario.id}/submit`, {
      subject: byId("email-subject").value,
      body: byId("email-body").value,
    });
    location.hash = `#/reports/${reportId}`;
  } catch (error) {
    showError("email-error", error.message);
  } finally {
    byId("email-send").disabled = false;
  }
}

export function bindEmail(maxEmailLength) {
  buildWheel();
  byId("email-body").maxLength = maxEmailLength;
  byId("wheel-spin").addEventListener("click", spin);
  byId("email-form").addEventListener("submit", submit);
}
