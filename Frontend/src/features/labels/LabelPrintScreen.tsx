import { useQuery } from '@tanstack/react-query';
import { useTranslation } from '../../hooks/useTranslation';
import QRCode from 'qrcode';
import { useCallback, useEffect, useRef, useState, type ReactElement } from 'react';
import { createPortal } from 'react-dom';
import { Icon } from '../../components/ui/Icon';
import { ApiError } from '../../lib/apiClient';
import { printJobsApi } from '../printing/api';
import { producedStockApi, type BarcodeLabelDto } from './api';

interface LabelPrintScreenProps {
  /** One label, or a whole run's worth. */
  barcodes: string[];
  /** Shown above the sheet when the thing was only just made. */
  headline?: string;
  onClose: () => void;
  /**
   * Print on this device's own printer rather than sending to the queue. The Print Queue
   * screen on the computer uses it; a tablet only when somebody chooses "Print here".
   */
  direct?: boolean;
  /** With `direct`: print the moment the labels are drawn, without a button. */
  autoPrint?: boolean;
  /** With `direct`: told once the print has been sent to the printer. */
  onPrinted?: () => void;
}

/**
 * The screen for printing labels (specification sections 12 and 19.13).
 *
 * It opens by itself the moment something is produced, because that is when the label
 * is needed — a worker who has just logged a roll should not have to go and find it in
 * a list of five hundred to print the sticker for it. A thermo run makes a dozen or
 * more bags at once, so the sheet holds all of them and prints as one job.
 *
 * <b>The tablets have no printer.</b> The label printer hangs off one computer, so the
 * main button sends the labels to its queue, and the computer's Print Queue screen prints
 * them. "Print here instead" stays for a device that does have a printer.
 *
 * <b>Rendered straight into the document body</b>, not nested inside the page like a
 * dialog. That is what makes the print rule <i>hide every child of body except this
 * one</i> — a single selector that does not care where in the app the print was
 * started from. Nested, the rule would have had to walk down through four wrappers,
 * and hiding a wrapper hides the labels inside it.
 *
 * A QR rather than a striped barcode, because the workers scan with Android tablets:
 * a camera reads QR at an angle and a damaged QR still reads, where a torn linear
 * barcode is simply gone. The human-readable code sits above it so a man can type it
 * when the label is ruined.
 */
export function LabelPrintScreen({
  barcodes,
  headline,
  onClose,
  direct = false,
  autoPrint = false,
  onPrinted,
}: LabelPrintScreenProps): ReactElement {
  const { t } = useTranslation();
  const labels = useQuery({
    queryKey: ['labels', barcodes],
    queryFn: () => producedStockApi.labels(barcodes),
  });

  const [sending, setSending] = useState(false);
  const [sent, setSent] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Every QR drawn, so nothing goes to the printer with an empty square on it.
  const [ready, setReady] = useState<ReadonlySet<string>>(() => new Set());
  // Stable, so a label does not draw its QR again every time this screen re-renders.
  const markReady = useCallback((barcode: string) => {
    setReady((current) =>
      current.has(barcode) ? current : new Set(current).add(barcode),
    );
  }, []);
  const allReady =
    labels.data !== undefined &&
    labels.data.length > 0 &&
    labels.data.every((label) => ready.has(label.barcode));

  function printHere(): void {
    // Blocks until the printer has the job (or the dialog is closed), so what follows
    // happens after the print, not before it.
    window.print();
    onPrinted?.();
  }

  // The latest callback, kept aside: the queue page re-renders every few seconds and
  // hands a new one each time, which must not restart (and so cancel) a print under way.
  const onPrintedRef = useRef(onPrinted);
  useEffect(() => {
    onPrintedRef.current = onPrinted;
  }, [onPrinted]);

  // The computer's queue in automatic mode: print once, as soon as every label is drawn.
  const printedOnce = useRef(false);
  useEffect(() => {
    if (!direct || !autoPrint || !allReady || printedOnce.current) {
      return;
    }

    printedOnce.current = true;
    // One frame, so the drawn QR codes are on the page before it goes to the printer.
    window.requestAnimationFrame(() => {
      window.print();
      onPrintedRef.current?.();
    });
  }, [direct, autoPrint, allReady]);

  async function sendToPrinter(): Promise<void> {
    setError(null);
    setSending(true);
    try {
      await printJobsApi.queue({
        barcodes,
        headline: headline ?? null,
        source: window.location.pathname,
      });
      setSent(true);
    } catch (caught) {
      setError(
        caught instanceof ApiError ? caught.message : t('common.somethingWentWrong'),
      );
    } finally {
      setSending(false);
    }
  }

  useEffect(() => {
    function onKeyDown(event: KeyboardEvent): void {
      if (event.key === 'Escape') {
        onClose();
      }
    }

    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [onClose]);

  const many = barcodes.length > 1;

  return createPortal(
    <div id="label-print-root" className="fixed inset-0 z-50 grid place-items-center p-4">
      {/* Everything marked `no-print` is the dialog around the labels — it must not
          reach the printer, and it must not take up space there either. */}
      <button
        type="button"
        aria-label={t('common.close')}
        onClick={onClose}
        className="no-print absolute inset-0 cursor-default bg-ink/50"
      />

      <div
        role="dialog"
        aria-modal="true"
        aria-label={
          many ? `Print ${String(barcodes.length)} labels` : t('labels.printLabel')
        }
        className="label-card relative max-h-[90dvh] w-full max-w-md overflow-y-auto rounded-card bg-surface p-5 shadow-raised"
      >
        <div className="no-print mb-4 flex items-start justify-between gap-4">
          <div>
            <h2 className="text-lg font-bold text-ink">
              {many ? `${String(barcodes.length)} labels` : t('stock.label')}
            </h2>
            {headline !== undefined && (
              <p className="mt-1 text-sm font-medium text-ok">{headline}</p>
            )}
          </div>
          <button
            type="button"
            aria-label={t('common.close')}
            onClick={onClose}
            className="grid size-touch shrink-0 place-items-center rounded-control text-ink-muted transition-colors hover:bg-canvas hover:text-ink"
          >
            <Icon name="close" />
          </button>
        </div>

        {labels.isPending && (
          <p className="no-print text-ink-muted">{t('common.loading')}</p>
        )}
        {labels.isError && <p className="no-print text-bad">{t('labels.loadFailed')}</p>}

        {labels.data?.length === 0 && (
          <p className="no-print text-ink-muted">{t('labels.none')}</p>
        )}

        {labels.data !== undefined && labels.data.length > 0 && (
          <>
            <div className="label-sheets">
              {labels.data.map((label) => (
                <LabelSheet key={label.barcode} label={label} onReady={markReady} />
              ))}
            </div>

            {error !== null && (
              <p
                role="alert"
                className="no-print mb-4 rounded-control border border-s-4 border-bad/30 border-s-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
              >
                {error}
              </p>
            )}

            {direct ? (
              <button
                type="button"
                className="no-print btn-primary"
                disabled={!allReady}
                onClick={printHere}
              >
                {many
                  ? `Print all ${String(labels.data.length)} labels`
                  : t('labels.printThis')}
              </button>
            ) : sent ? (
              <div className="no-print">
                <p className="mb-3 rounded-control border border-s-4 border-ok/30 border-s-ok bg-ok-soft px-4 py-3 text-sm font-medium text-ok">
                  {t('print.sent')}
                </p>
                <button type="button" className="btn-primary" onClick={onClose}>
                  {t('common.close')}
                </button>
              </div>
            ) : (
              <>
                <button
                  type="button"
                  className="no-print btn-primary"
                  disabled={sending}
                  onClick={() => {
                    void sendToPrinter();
                  }}
                >
                  {sending ? t('print.sending') : t('print.sendToPrinter')}
                </button>
                <button
                  type="button"
                  className="no-print mt-3 w-full text-center text-sm font-medium text-brand-700 hover:underline"
                  onClick={printHere}
                >
                  {t('print.printHere')}
                </button>
              </>
            )}
            <p className="no-print mt-2 text-xs text-ink-muted">
              4 × 4 in (102 × 102 mm), one to a page. Reprinting is allowed.
            </p>
          </>
        )}
      </div>
    </div>,
    document.body,
  );
}

/**
 * One sticker. Each kind has a layout of its own, so a roll, a bag and a pallet are told
 * apart across the floor before anybody reads a word: the roll's label is the one with
 * the black band and the roll drawn on it, the bag's has the plate and its product code
 * in big letters, and the pallet's is almost nothing but its QR.
 */
function LabelSheet({
  label,
  onReady,
}: {
  label: BarcodeLabelDto;
  /** Told once the QR is drawn, or has failed to be — either way it is final. */
  onReady: (barcode: string) => void;
}): ReactElement {
  const qr = useQr(label.barcode, onReady);

  return label.kind === 'Roll' ? (
    <RollLabel label={label} qr={qr} />
  ) : label.kind === 'Bag' ? (
    <BagLabel label={label} qr={qr} />
  ) : (
    <PalletLabel label={label} qr={qr} />
  );
}

/** The QR as an SVG string, or null when it could not be drawn. */
function useQr(barcode: string, onReady: (barcode: string) => void): string | null {
  const [qr, setQr] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    // The QR carries the barcode and nothing else. A scan has to resolve to one object,
    // and the system already knows everything else about it.
    void QRCode.toString(barcode, {
      type: 'svg',
      margin: 0,
      errorCorrectionLevel: 'M',
    }).then(
      (svg) => {
        if (!cancelled) {
          setQr(svg);
          onReady(barcode);
        }
      },
      () => {
        if (!cancelled) {
          setQr(null);
          onReady(barcode);
        }
      },
    );

    return () => {
      cancelled = true;
    };
  }, [barcode, onReady]);

  return qr;
}

/**
 * A black band across the top with the roll drawn in it, the QR on the left, and the
 * roll's own facts on the right. Weight and length are printed on a roll's label only
 * (sections 19.25 and 19.26), so they get the room.
 */
function RollLabel({
  label,
  qr,
}: {
  label: BarcodeLabelDto;
  qr: string | null;
}): ReactElement {
  const { t } = useTranslation();

  return (
    <div className="print-label mx-auto mb-4 flex flex-col rounded-card border-2 border-ink bg-white p-[3mm] text-ink">
      <div className="mb-[2.5mm] flex items-center gap-[3mm] rounded bg-ink px-[3mm] py-[2mm] text-white">
        <RollIcon className="h-[13mm] w-[19mm] shrink-0" />
        <div className="min-w-0 flex-1">
          <p className="text-[9px] font-bold tracking-widest uppercase">
            Roll · رول — Colors
          </p>
          <p className="truncate font-mono text-lg leading-tight font-bold">
            {label.headlineCode}
          </p>
        </div>
      </div>

      <div className="flex min-h-0 flex-1 gap-[3mm]">
        <QrBlock barcode={label.barcode} qr={qr} />

        <div className="min-w-0 flex-1 text-[11px] leading-tight">
          {/* A roll's type is its recipe; its product is named at the thermo (19.17). */}
          {label.recipeName !== null && (
            <p className="mb-[1mm] border-b-2 border-ink pb-[1mm] text-base leading-tight font-bold">
              {label.recipeName}
            </p>
          )}
          {label.colorName !== null && (
            <Field label={t('term.colour')} value={label.colorName} />
          )}
          <div className="grid grid-cols-2 gap-x-[2mm]">
            {label.weight !== null && (
              <Field label={t('field.weight')} value={`${String(label.weight)} kg`} />
            )}
            {label.length !== null && (
              <Field label={t('field.length')} value={String(label.length)} />
            )}
          </div>
          {(label.minThickness !== null || label.maxThickness !== null) && (
            <Field
              label={t('md.thickness')}
              value={labelRange(label.minThickness, label.maxThickness)}
            />
          )}
          {label.shiftName !== null && (
            <Field label="Shift · الوردية" value={label.shiftName} />
          )}
          <Field label="Date · الوقت" value={madeAt(label.createdAt)} />
        </div>
      </div>
    </div>
  );
}

/**
 * The plate and the product code across the top, big enough to read from the next
 * aisle — that code is what the packers sort by. The QR is on the right, the other way
 * round from a roll's, and the piece count has a box of its own: it is what the customer
 * counts.
 */
function BagLabel({
  label,
  qr,
}: {
  label: BarcodeLabelDto;
  qr: string | null;
}): ReactElement {
  const { t } = useTranslation();

  return (
    <div className="print-label mx-auto mb-4 flex flex-col rounded-card border-2 border-ink bg-white p-[3mm] text-ink">
      <div className="mb-[2mm] flex items-center gap-[3mm] border-b-[3px] border-double border-ink pb-[2mm]">
        <PlateIcon className="h-[14mm] w-[20mm] shrink-0" />
        <div className="min-w-0 flex-1">
          <p className="truncate font-mono text-2xl leading-none font-black">
            {label.headlineCode}
          </p>
          {label.productName !== null && (
            <p className="mt-[1mm] truncate text-xs font-semibold">{label.productName}</p>
          )}
        </div>
        <span className="shrink-0 rounded-full border-2 border-ink px-2 py-0.5 text-[10px] font-bold uppercase">
          Bag · كيس
        </span>
      </div>

      <div className="flex min-h-0 flex-1 gap-[3mm]">
        <div className="min-w-0 flex-1 text-[11px] leading-tight">
          {label.pieceCount !== null && (
            <div className="mb-[1mm] rounded border-2 border-ink px-[2mm] py-[1mm] text-center">
              <p className="text-[9px]">Pieces · العدد</p>
              <p className="text-2xl leading-none font-black">{label.pieceCount}</p>
            </div>
          )}
          {label.colorName !== null && (
            <Field label={t('term.colour')} value={label.colorName} />
          )}
          {label.rollCode !== null && (
            /* رقم الرول — already on the factory's own bag label today. */
            <Field label="Roll · رقم الرول" value={label.rollCode} mono />
          )}
          {label.shiftName !== null && (
            <Field label="Shift · الوردية" value={label.shiftName} />
          )}
          <Field label="Date · الوقت" value={madeAt(label.createdAt)} />
        </div>

        <QrBlock barcode={label.barcode} qr={qr} />
      </div>
    </div>
  );
}

/**
 * Nothing but the pallet's code, a QR as big as the sticker allows, and the general code
 * under it. A pallet is scanned from a forklift's distance and sorted by its general
 * code; what is on it is one scan away.
 */
function PalletLabel({
  label,
  qr,
}: {
  label: BarcodeLabelDto;
  qr: string | null;
}): ReactElement {
  return (
    <div className="print-label mx-auto mb-4 flex flex-col items-center justify-between rounded-card border-2 border-ink bg-white p-[3mm] text-ink">
      <p className="font-mono text-2xl leading-none font-black">{label.barcode}</p>
      <QrSquare qr={qr} size="h-[76mm] w-[76mm]" />
      <p className="font-mono text-2xl leading-none font-black">
        {label.productCode ?? '—'}
      </p>
    </div>
  );
}

/**
 * The QR with its code above, so a torn label can still be typed in. It takes the larger
 * half of the square: it is scanned by a tablet camera from arm's length on the floor,
 * and a bigger code reads from further away and at a steeper angle.
 */
function QrBlock({ barcode, qr }: { barcode: string; qr: string | null }): ReactElement {
  return (
    <div className="flex shrink-0 flex-col items-center justify-center">
      <p className="mb-[1mm] font-mono text-sm font-bold">{barcode}</p>
      <QrSquare qr={qr} size="h-[50mm] w-[50mm]" />
    </div>
  );
}

function QrSquare({ qr, size }: { qr: string | null; size: string }): ReactElement {
  return qr === null ? (
    <div className={`grid place-items-center border border-ink text-xs ${size}`}>
      no code
    </div>
  ) : (
    <div
      className={`${size} [&>svg]:h-full [&>svg]:w-full`}
      // The library returns a complete SVG string; there is no user input in it, only
      // the barcode this server issued.
      dangerouslySetInnerHTML={{ __html: qr }}
    />
  );
}

/** A roll lying on its side, the wound end towards you. */
function RollIcon({ className }: { className: string }): ReactElement {
  return (
    <svg
      viewBox="0 0 48 32"
      className={className}
      fill="none"
      stroke="currentColor"
      aria-hidden="true"
    >
      <path d="M12 3 H40 A6 13 0 0 1 40 29 H12" strokeWidth="2.5" />
      <ellipse cx="12" cy="16" rx="6" ry="13" strokeWidth="2.5" />
      <ellipse cx="12" cy="16" rx="3.6" ry="8" strokeWidth="1.5" />
      <ellipse cx="12" cy="16" rx="1.6" ry="3.5" fill="currentColor" stroke="none" />
    </svg>
  );
}

/** A plate seen from a little above: the rim, the well, and the edge underneath. */
function PlateIcon({ className }: { className: string }): ReactElement {
  return (
    <svg
      viewBox="0 0 48 32"
      className={className}
      fill="none"
      stroke="currentColor"
      aria-hidden="true"
    >
      <path d="M2 15 v3 a22 11 0 0 0 44 0 v-3" strokeWidth="2.5" />
      <ellipse cx="24" cy="15" rx="22" ry="11" strokeWidth="2.5" />
      <ellipse cx="24" cy="15" rx="13" ry="6" strokeWidth="1.8" />
    </svg>
  );
}

/** "01/11/2026 12:44", on the clock of the device printing it. */
function madeAt(createdAt: string): string {
  const made = new Date(createdAt);
  return `${made.toLocaleDateString('en-GB')} ${made.toLocaleTimeString('en-GB', {
    hour: '2-digit',
    minute: '2-digit',
  })}`;
}

/** The range as it goes on paper: "2.8–3.0 mm" or "≥ 3.0 mm". */
function labelRange(min: number | null, max: number | null): string {
  if (min !== null && max !== null) {
    return `${String(min)}–${String(max)} mm`;
  }
  return min !== null ? `≥ ${String(min)} mm` : `≤ ${String(max)} mm`;
}

function Field({
  label,
  value,
  mono = false,
}: {
  label: string;
  value: string;
  mono?: boolean;
}): ReactElement {
  return (
    // Stacked, the name above the value: the column beside the QR is narrow, and a roll
    // code next to its name would not fit on one line.
    <div className="border-b border-dotted border-ink/30 py-[0.6mm]">
      <p className="text-[9px] text-ink/70">{label}</p>
      <p className={`line-clamp-2 font-semibold break-words ${mono ? 'font-mono' : ''}`}>
        {value}
      </p>
    </div>
  );
}
