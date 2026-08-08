import { describe, expect, it } from 'vitest';
import { resolveApiBaseUrl } from './apiBaseUrl';

/**
 * Where the screens look for the API.
 *
 * This was wrong once, in the way that is hardest to catch: it worked perfectly on the
 * developer's machine and on a phone on the factory network, and failed on the only
 * copy anybody else would ever use. The built screens asked for port 5211 on the public
 * address, nothing was listening there, and every screen said "Cannot reach the server"
 * while the server itself was answering /health quite happily.
 *
 * Nothing about a development machine can reveal that, so it is pinned here instead.
 */

const railway = { protocol: 'https:', hostname: 'colors-erp.up.railway.app' };
const laptop = { protocol: 'http:', hostname: 'localhost' };
const onTheFactoryNetwork = { protocol: 'http:', hostname: '192.168.68.127' };

describe('in production', () => {
  it('asks the address that served the page, with no port', () => {
    // The API serves the screens, so there is one address and it is already known.
    // Empty means every call is relative — "/api/..." on whatever host this is.
    expect(resolveApiBaseUrl(undefined, false, railway)).toBe('');
  });

  it('does not care about the host, the protocol, or a custom domain later', () => {
    for (const page of [railway, laptop, onTheFactoryNetwork]) {
      expect(resolveApiBaseUrl(undefined, false, page)).toBe('');
    }
  });

  it('never invents a port', () => {
    // The bug in one line. A built copy that asks for :5211 reaches nothing, on any
    // host that is not a developer's own machine.
    const result = resolveApiBaseUrl(undefined, false, railway);

    expect(result).not.toContain('5211');
    expect(result).not.toContain('railway');
  });
});

describe('while developing', () => {
  it('sends requests to the API on its own port', () => {
    // Vite serves the screens on 5173; the API is a separate process on 5211.
    expect(resolveApiBaseUrl(undefined, true, laptop)).toBe('http://localhost:5211');
  });

  it('uses the address in the bar, so a phone reaches the right machine', () => {
    // Never "localhost" — a phone's localhost is the phone. This one word is the
    // difference between the screens working on the factory floor and every request
    // failing.
    expect(resolveApiBaseUrl(undefined, true, onTheFactoryNetwork)).toBe(
      'http://192.168.68.127:5211',
    );
  });

  it('keeps the protocol of the page', () => {
    expect(resolveApiBaseUrl(undefined, true, { protocol: 'https:', hostname: 'box' })).toBe(
      'https://box:5211',
    );
  });
});

describe('when someone says exactly where it is', () => {
  it('uses that, in development or not', () => {
    const given = 'https://api.example.com';

    expect(resolveApiBaseUrl(given, true, laptop)).toBe(given);
    expect(resolveApiBaseUrl(given, false, railway)).toBe(given);
  });

  it('treats an empty setting as nothing said at all', () => {
    // An unset variable in a .env file arrives as "" rather than undefined. Taking that
    // literally in development would point every request at the page's own address,
    // where no API is listening.
    expect(resolveApiBaseUrl('', true, laptop)).toBe('http://localhost:5211');
    expect(resolveApiBaseUrl('', false, railway)).toBe('');
  });
});
