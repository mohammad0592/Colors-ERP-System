/**
 * Reading a label with the tablet's camera (specification section 12).
 *
 * The labels carry a **QR code holding the barcode and nothing else** — `B004501` — and
 * section 12 chose QR precisely because "the workers use Android tablets and a camera
 * reads QR at an angle and survives a damaged label better than a linear barcode".
 *
 * ### Why the browser's own detector, and no library
 *
 * Chrome — including Android Chrome, which is what the factory holds — has `BarcodeDetector`
 * built in. It decodes in native code, costs nothing in the bundle, and needs no
 * maintenance. A JavaScript decoder would add a quarter of a megabyte to a bundle already
 * over the warning threshold, and decode more slowly on exactly the cheap tablets that
 * need it most.
 *
 * The cost is that Safari and Firefox have no such thing. There the camera is simply not
 * offered and the man types the code or picks it from the list, which is the same fallback
 * a torn label already needs. `unavailableReason` says which case applies so a screen can
 * explain itself instead of showing a button that does nothing.
 */

/** How a code reached a screen. Sent to the API, which records it (section 12). */
export type EntryMethod = 'Scanned' | 'Typed' | 'Picked';

/**
 * The parts of the browser this needs, named so they can be handed in by a test.
 *
 * `isSecureContext` is the one that surprises people: a browser gives no camera to a
 * page served over plain http, whatever the permissions say. The factory server and the
 * cloud trial are both https and localhost counts as secure, so the case this catches is
 * opening the development server from a phone by its network address.
 */
export interface ScannerEnvironment {
  hasDetector: boolean;
  hasCamera: boolean;
  isSecureContext: boolean;
}

/**
 * Whether the browser will hand over a camera at all.
 *
 * `navigator.mediaDevices` is typed as always present and is not: a page served over
 * plain http has no such property. The cast is what lets the check be written, and this
 * is the one case the whole module is here to catch.
 */
function hasGetUserMedia(): boolean {
  const media = navigator.mediaDevices as MediaDevices | undefined;

  return typeof media?.getUserMedia === 'function';
}

export function currentEnvironment(): ScannerEnvironment {
  return {
    hasDetector: typeof window !== 'undefined' && 'BarcodeDetector' in window,
    hasCamera: typeof navigator !== 'undefined' && hasGetUserMedia(),
    isSecureContext: typeof window !== 'undefined' && window.isSecureContext,
  };
}

/**
 * Why the camera cannot be offered, in words a man on the floor can act on — or null
 * when it can.
 *
 * The order matters. An insecure page fails every check, and being told "this browser
 * cannot read labels" would send somebody looking for a different tablet when the real
 * answer is the address they opened.
 */
export function unavailableReason(env: ScannerEnvironment): string | null {
  if (!env.isSecureContext) {
    return 'The camera only works over a secure connection (https). Type the code instead.';
  }

  if (!env.hasCamera) {
    return 'This device has no camera the browser can use. Type the code instead.';
  }

  if (!env.hasDetector) {
    return 'This browser cannot read labels with the camera. Chrome can. Type the code instead.';
  }

  return null;
}

export function isScanningAvailable(env: ScannerEnvironment = currentEnvironment()): boolean {
  return unavailableReason(env) === null;
}

/**
 * A code the camera read, cleaned up.
 *
 * A QR holds exactly what was printed into it, but a damaged read or a label somebody
 * re-encoded by hand can carry spaces or a newline. The codes themselves never contain
 * either, so trimming is safe and saves a puzzling "that label is not one of ours".
 */
export function cleanCode(raw: string): string {
  return raw.replace(/\s+/g, '').toUpperCase();
}

// The browser API, declared because TypeScript's own libraries do not have it yet.
interface DetectedBarcode {
  rawValue: string;
}

interface BarcodeDetectorLike {
  detect: (source: CanvasImageSource) => Promise<DetectedBarcode[]>;
}

type BarcodeDetectorConstructor = new (options?: {
  formats?: string[];
}) => BarcodeDetectorLike;

declare global {
  interface Window {
    BarcodeDetector?: BarcodeDetectorConstructor;
  }
}

export interface ScannerHandle {
  /** Turns the camera off and releases it. Safe to call twice. */
  stop: () => void;
}

/**
 * Points the camera at the label and calls back with the first code it reads.
 *
 * Stops itself on the first result: every screen here acts on one code at a time, and a
 * scanner that kept firing would send the same bag twice while the request was still in
 * flight.
 *
 * The rear camera is asked for by name. `facingMode: 'environment'` is a preference
 * rather than a guarantee, but on a tablet held up to a pallet it is the difference
 * between reading the label and filming the man's face.
 */
export async function startScanning(
  video: HTMLVideoElement,
  onCode: (code: string) => void,
): Promise<ScannerHandle> {
  const Detector = window.BarcodeDetector;
  if (Detector === undefined) {
    throw new Error('This browser cannot read labels with the camera.');
  }

  const stream = await navigator.mediaDevices.getUserMedia({
    video: { facingMode: 'environment' },
    audio: false,
  });

  const detector = new Detector({ formats: ['qr_code'] });

  let stopped = false;
  let frame = 0;

  const stop = (): void => {
    if (stopped) {
      return;
    }
    stopped = true;
    cancelAnimationFrame(frame);
    for (const track of stream.getTracks()) {
      track.stop();
    }
    video.srcObject = null;
  };

  video.srcObject = stream;
  // Older Android Chrome will not start the stream without this, and the promise it
  // returns rejects when the element is removed mid-play — which is not an error worth
  // showing anybody.
  await video.play().catch(() => undefined);

  const look = (): void => {
    if (stopped) {
      return;
    }

    detector.detect(video).then(
      (found) => {
        if (stopped) {
          return;
        }

        const first = found[0]?.rawValue;
        if (first !== undefined && first !== '') {
          stop();
          onCode(cleanCode(first));
          return;
        }

        frame = requestAnimationFrame(look);
      },
      () => {
        // A frame that cannot be decoded is the normal case, not a failure — the camera
        // spends most of its time looking at nothing in particular. Keep going.
        if (!stopped) {
          frame = requestAnimationFrame(look);
        }
      },
    );
  };

  frame = requestAnimationFrame(look);

  return { stop };
}
