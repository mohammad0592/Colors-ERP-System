import { useEffect, useState } from 'react';

/**
 * The absorbency sample's ten minutes in water, counted down on the screens
 * (specification section 19.15).
 *
 * The server says when the sample is due out — it keeps when the dry weight was saved —
 * so a tablet put down and picked up again, or a second tablet, counts to the same moment.
 */

/** "9:05" — minutes and seconds left, never below zero. */
export function formatCountdown(msLeft: number): string {
  const seconds = Math.max(0, Math.ceil(msLeft / 1000));
  return `${String(Math.floor(seconds / 60))}:${String(seconds % 60).padStart(2, '0')}`;
}

/** The time now, moving once a second while `ticking`, so a countdown redraws itself. */
export function useNow(ticking: boolean): number {
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    if (!ticking) {
      return undefined;
    }
    const timer = window.setInterval(() => {
      setNow(Date.now());
    }, 1000);
    return () => {
      window.clearInterval(timer);
    };
  }, [ticking]);

  return now;
}

/**
 * Three short beeps when a sample is due out of the water. The man has walked away from
 * the tablet to do something else for ten minutes; a sound reaches him where the screen
 * does not. Silent where the browser has no sound or will not play one.
 */
export function beep(): void {
  try {
    const context = new AudioContext();
    [0, 0.3, 0.6].forEach((start) => {
      const tone = context.createOscillator();
      const volume = context.createGain();
      tone.frequency.value = 880;
      volume.gain.value = 0.2;
      tone.connect(volume);
      volume.connect(context.destination);
      tone.start(context.currentTime + start);
      tone.stop(context.currentTime + start + 0.18);
    });
    window.setTimeout(() => {
      void context.close();
    }, 1500);
  } catch {
    // No sound on this device. The screen still shows it is due.
  }
}
