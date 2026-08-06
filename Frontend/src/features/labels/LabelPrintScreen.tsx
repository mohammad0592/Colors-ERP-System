import { useQuery } from '@tanstack/react-query';
import QRCode from 'qrcode';
import { useEffect, useState, type ReactElement } from 'react';
import { createPortal } from 'react-dom';
import { Icon } from '../../components/ui/Icon';
import { producedStockApi, type BarcodeLabelDto } from './api';

interface LabelPrintScreenProps {
  /** One label, or a whole run's worth. */
  barcodes: string[];
  /** Shown above the sheet when the thing was only just made. */
  headline?: string;
  onClose: () => void;
}

/**
 * The screen for printing labels (specification section 12).
 *
 * It opens by itself the moment something is produced, because that is when the label
 * is needed — a worker who has just logged a roll should not have to go and find it in
 * a list of five hundred to print the sticker for it. A thermo run makes a dozen or
 * more bags at once, so the sheet holds all of them and prints as one job.
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
}: LabelPrintScreenProps): ReactElement {
  const labels = useQuery({
    queryKey: ['labels', barcodes],
    queryFn: () => producedStockApi.labels(barcodes),
  });

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
    <div
      id="label-print-root"
      role="dialog"
      aria-modal="true"
      aria-label={many ? `Print ${String(barcodes.length)} labels` : 'Print label'}
      className="fixed inset-0 z-50 overflow-y-auto bg-canvas"
    >
      {/* Everything with `no-print` is the screen around the labels — it must not reach
          the printer, and it must not take up space there either. */}
      <header className="no-print sticky top-0 z-10 flex flex-wrap items-center justify-between gap-3 border-b border-line bg-surface px-6 py-4">
        <div>
          <h2 className="text-lg font-bold text-ink">
            {many ? `${String(barcodes.length)} labels to print` : 'Label'}
          </h2>
          {headline !== undefined && (
            <p className="mt-1 text-sm font-medium text-ok">{headline}</p>
          )}
        </div>

        <div className="flex items-center gap-2">
          {labels.data !== undefined && labels.data.length > 0 && (
            <button
              type="button"
              className="h-touch rounded-control bg-brand-600 px-5 font-semibold text-white transition-colors hover:bg-brand-700"
              onClick={() => {
                window.print();
              }}
            >
              {many ? `Print all ${String(labels.data.length)}` : 'Print'}
            </button>
          )}
          <button
            type="button"
            aria-label="Close"
            onClick={onClose}
            className="grid size-touch place-items-center rounded-control text-ink-muted transition-colors hover:bg-canvas hover:text-ink"
          >
            <Icon name="close" />
          </button>
        </div>
      </header>

      <div className="p-6">
        {labels.isPending && <p className="no-print text-ink-muted">Loading…</p>}
        {labels.isError && <p className="no-print text-bad">Could not load these labels.</p>}

        {labels.data?.length === 0 && (
          <p className="no-print text-ink-muted">No label was found for this.</p>
        )}

        {labels.data !== undefined && labels.data.length > 0 && (
          <>
            <div className="label-sheets">
              {labels.data.map((label) => (
                <LabelSheet key={label.barcode} label={label} />
              ))}
            </div>

            <p className="no-print mt-4 text-center text-xs text-ink-muted">
              Each label prints at 100 × 70 mm, one to a page. Reprinting is allowed —
              the barcode never changes, so an old label and a new one name the same
              thing.
            </p>
          </>
        )}
      </div>
    </div>,
    document.body,
  );
}

function LabelSheet({ label }: { label: BarcodeLabelDto }): ReactElement {
  const [qr, setQr] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    // The QR carries the barcode and nothing else. A scan has to resolve to one object,
    // and the system already knows everything else about it.
    void QRCode.toString(label.barcode, {
      type: 'svg',
      margin: 0,
      errorCorrectionLevel: 'M',
    }).then(
      (svg) => {
        if (!cancelled) {
          setQr(svg);
        }
      },
      () => {
        if (!cancelled) {
          setQr(null);
        }
      },
    );

    return () => {
      cancelled = true;
    };
  }, [label.barcode]);

  const made = new Date(label.createdAt);

  return (
    <div className="print-label mx-auto mb-4 flex flex-col rounded-card border-2 border-ink bg-white p-3 text-ink">
      <div className="mb-1.5 flex items-start justify-between gap-3 border-b-2 border-ink pb-1.5">
        <div className="min-w-0">
          <p className="text-[8px] font-bold tracking-widest uppercase">
            Colors — Paper &amp; Plastic
          </p>
          <p className="truncate text-base leading-tight font-bold">{label.headlineCode}</p>
        </div>
        <span className="shrink-0 rounded border border-ink px-1.5 py-0.5 text-[9px] font-bold uppercase">
          {label.kind}
        </span>
      </div>

      <div className="flex min-h-0 flex-1 gap-3">
        <div className="min-w-0 flex-1 text-[10px] leading-tight">
          {label.productName !== null && <Field label="Product" value={label.productName} />}
          {label.colorName !== null && <Field label="Colour" value={label.colorName} />}
          {label.rollCode !== null && (
            /* رقم الرول — already on the factory's own bag label today. */
            <Field label="Roll · رقم الرول" value={label.rollCode} mono />
          )}
          {label.pieceCount !== null && (
            <Field label="Pieces · العدد" value={String(label.pieceCount)} />
          )}
          {label.weight !== null && <Field label="Weight" value={`${String(label.weight)} kg`} />}
          {label.shiftName !== null && <Field label="Shift · الوردية" value={label.shiftName} />}
          <Field
            label="Date · الوقت"
            value={`${made.toLocaleDateString('en-GB')} ${made.toLocaleTimeString('en-GB', {
              hour: '2-digit',
              minute: '2-digit',
            })}`}
          />
        </div>

        <div className="flex w-[26mm] shrink-0 flex-col items-center">
          {/* The code above the barcode, so a torn label can still be typed in. */}
          <p className="mb-0.5 font-mono text-xs font-bold">{label.barcode}</p>
          {qr === null ? (
            <div className="grid h-[24mm] w-[24mm] place-items-center border border-ink text-[9px]">
              no code
            </div>
          ) : (
            <div
              className="h-[24mm] w-[24mm] [&>svg]:h-full [&>svg]:w-full"
              // The library returns a complete SVG string; there is no user input in it,
              // only the barcode this server issued.
              dangerouslySetInnerHTML={{ __html: qr }}
            />
          )}
          {label.productCode !== null && (
            <p className="mt-0.5 font-mono text-[9px]">{label.productCode}</p>
          )}
        </div>
      </div>
    </div>
  );
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
    <div className="flex justify-between gap-2 border-b border-dotted border-ink/30 py-[1px]">
      <span className="shrink-0 text-ink/70">{label}</span>
      <span className={`truncate font-semibold ${mono ? 'font-mono' : ''}`}>{value}</span>
    </div>
  );
}
