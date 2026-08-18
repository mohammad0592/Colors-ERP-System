import { describe, expect, it } from 'vitest';
import {
  cleanCode,
  isScanningAvailable,
  unavailableReason,
  type ScannerEnvironment,
} from './barcodeScanner';

const working: ScannerEnvironment = {
  hasDetector: true,
  hasCamera: true,
  isSecureContext: true,
};

describe('unavailableReason', () => {
  it('says nothing is wrong when the camera can be used', () => {
    expect(unavailableReason(working)).toBeNull();
  });

  it('blames the connection first, because an insecure page fails every check', () => {
    // Opening the development server from a phone by its network address hits all three
    // at once. Naming the browser would send somebody looking for a different tablet.
    const reason = unavailableReason({
      hasDetector: false,
      hasCamera: false,
      isSecureContext: false,
    });

    expect(reason).toContain('https');
  });

  it('names the missing camera when the page is secure', () => {
    expect(
      unavailableReason({ ...working, hasCamera: false }),
    ).toContain('no camera');
  });

  it('does not mind a browser with no reader of its own', () => {
    // Which is every browser on Windows. A decoder is loaded there instead, so the
    // camera is still offered — and telling somebody in Chrome to use Chrome was
    // exactly the message this replaced.
    expect(unavailableReason({ ...working, hasDetector: false })).toBeNull();
  });

  it('always tells the man what to do instead', () => {
    const broken: ScannerEnvironment[] = [
      { ...working, isSecureContext: false },
      { ...working, hasCamera: false },
    ];

    for (const env of broken) {
      expect(unavailableReason(env)).toContain('Type the code');
    }
  });
});

describe('isScanningAvailable', () => {
  it('needs a secure page and a camera, and nothing else', () => {
    expect(isScanningAvailable(working)).toBe(true);
    expect(isScanningAvailable({ ...working, hasCamera: false })).toBe(false);
    expect(isScanningAvailable({ ...working, isSecureContext: false })).toBe(false);

    // The one that used to fail here. A Windows laptop has no BarcodeDetector and can
    // still read a label.
    expect(isScanningAvailable({ ...working, hasDetector: false })).toBe(true);
  });
});

describe('cleanCode', () => {
  it('leaves a good code alone', () => {
    expect(cleanCode('B004501')).toBe('B004501');
  });

  it('drops the whitespace a damaged read can carry', () => {
    expect(cleanCode(' B004501 \n')).toBe('B004501');
    expect(cleanCode('B00 4501')).toBe('B004501');
  });

  it('puts a code back into capitals', () => {
    // Roll codes are read and typed by people too, and 09wn180726a is the same roll.
    expect(cleanCode('09wn180726a')).toBe('09WN180726A');
  });
});
