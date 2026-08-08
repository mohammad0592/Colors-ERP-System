/**
 * Working out where the API is, which is a different answer in each of three places.
 *
 * | Where | Who serves the screens | Where the API is |
 * |---|---|---|
 * | A developer's machine | Vite, on port 5173 | a *different* port, 5211 |
 * | A phone on the factory network | Vite, on the developer's machine | that machine, port 5211 |
 * | The cloud trial, and the factory server | **the API itself** | the same address, no port |
 *
 * The middle row is why this cannot simply say `localhost`: a phone's localhost is the
 * phone. The bottom row is why it cannot simply say `:5211` either — in production the
 * API serves the screens, so the address the page came from is already the right one,
 * and adding a port points at nothing.
 *
 * Kept apart from `apiClient` so it can be tested without a browser or a server.
 */

export interface PageAddress {
  protocol: string;
  hostname: string;
}

/** The port the API listens on while developing, alongside Vite on 5173. */
const DEVELOPMENT_API_PORT = 5211;

export function resolveApiBaseUrl(
  configured: string | undefined,
  isDevelopment: boolean,
  page: PageAddress,
): string {
  // Someone said exactly where it is. Nothing else gets a say.
  if (configured !== undefined && configured !== '') {
    return configured;
  }

  // Production, in both of its forms — the cloud trial and the factory server. The API
  // served this page, so requests go back to wherever it came from. Returning nothing
  // makes every call relative, which is also the only answer that survives being opened
  // over HTTPS, on a custom domain, or behind whatever a host puts in front of it.
  if (!isDevelopment) {
    return '';
  }

  // Developing: Vite served this page, so the API is elsewhere. The host is taken from
  // the address in the bar rather than assumed, so a phone or a tablet on the factory
  // network reaches the developer's machine and not itself.
  return `${page.protocol}//${page.hostname}:${String(DEVELOPMENT_API_PORT)}`;
}
