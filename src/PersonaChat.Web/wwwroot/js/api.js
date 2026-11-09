const NETWORK_ERROR = "Could not reach the app. Is it still running?";
const GENERIC_ERROR = "Something went wrong. Please try again.";

export async function api(path, options) {
  let response;
  try {
    response = await fetch(`/api${path}`, options);
  } catch {
    throw new Error(NETWORK_ERROR);
  }

  if (response.status === 204) return null;
  const body = await response.json().catch(() => null);
  if (!response.ok) throw new Error(body?.error ?? GENERIC_ERROR);
  return body;
}

// keepalive lets the request finish even when the page is closing.
export function post(path, body = {}) {
  return api(path, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
    keepalive: true,
  });
}
