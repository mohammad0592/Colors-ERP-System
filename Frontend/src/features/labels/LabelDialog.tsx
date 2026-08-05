import { useQuery } from '@tanstack/react-query';
import QRCode from 'qrcode';
import { useEffect, useState, type ReactElement } from 'react';
import { Modal } from '../../components/ui/Modal';
import { producedStockApi, type BarcodeLabelDto } from './api';

interface LabelDialogProps {
  /** One label, or a whole run's worth. */
  barcodes: string[];
  /** Shown above the sheet when the thing was only just made. */
  headline?: string;
  onClose: () => void;
}

/**
 * The labels that get stuck on rolls, bags and pallets (specification section 12).
 *
 * This opens by itself the moment something is produced, because that is when the label
 * is needed — a worker who has just logged a roll should not have to go and find it in
 * a list of five hundred to print the sticker for it.
 *
 * A thermo run makes a dozen or more bags at once, so the sheet holds all of them and
 * prints as one job, in the order they were made.
 *
 * A QR rather than a striped barcode, because the workers scan with Android tablets:
 * a camera reads QR at an angle and a damaged QR still reads, where a torn linear
 * barcode is simply gone. The human-readable code sits above it so a man can type it
 * when the label is ruined.
 */
export function LabelDialog({
  barcodes,
  headline,
  onClose,
}: LabelDialogProps): ReactElement {
  const labels = useQuery({
    queryKey: ['labels', barcodes],
    queryFn: () => producedStockApi.labels(barcodes),
  });

  const many = barcodes.length > 1;

  return (
    <Modal
      title={many ? `Print ${String(barcodes.length)} labels` : `Label ${barcodes[0] ?? ''}`}
      onClose={onClose}
      printable
    >
      {headline !== undefined && (
        <p className="mb-4 rounded-control border border-l-4 border-ok/30 border-l-ok bg-ok-soft px-4 py-3 text-sm font-medium text-ok">
          {headline}
        </p>
      )}

      {labels.isPending && <p className="text-ink-muted">Loading…</p>}
      {labels.isError && <p className="text-bad">Could not load these labels.</p>}

      {labels.data !== undefined && (
        <>
          {labels.data.length === 0 ? (
            <p className="text-ink-muted">No label was found for this.</p>
          ) : (
            <>
              <div className="mb-5 max-h-[45dvh] overflow-y-auto print:max-h-none print:overflow-visible">
                {labels.data.map((label) => (
                  <LabelSheet key={label.barcode} label={label} />
                ))}
              </div>

              <button
                type="button"
                className="btn-primary"
                onClick={() => {
                  window.print();
                }}
              >
                {many ? `Print all ${String(labels.data.length)} labels` : 'Print this label'}
              </button>
              <p className="mt-2 text-xs text-ink-muted">
                Each label prints at 100 × 70 mm, one to a page. Reprinting is allowed —
                the barcode never changes, so an old label and a new one name the same
                thing.
              </p>
            </>
          )}
        </>
      )}
    </Modal>
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
    <div className="print-label mx-auto mb-4 rounded-card border-2 border-ink bg-white p-4 text-ink">
      <div className="mb-2 flex items-start justify-between gap-3 border-b-2 border-ink pb-2">
        <div>
          <p className="text-[10px] font-bold tracking-widest uppercase">
            Colors — Paper &amp; Plastic
          </p>
          <p className="text-lg leading-tight font-bold">{label.headlineCode}</p>
        </div>
        <span className="rounded border border-ink px-2 py-0.5 text-xs font-bold uppercase">
          {label.kind}
        </span>
      </div>

      <div className="flex gap-4">
        <div className="flex-1 text-xs">
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

        <div className="flex w-[30mm] shrink-0 flex-col items-center">
          {/* The code above the barcode, so a torn label can still be typed in. */}
          <p className="mb-1 font-mono text-sm font-bold">{label.barcode}</p>
          {qr === null ? (
            <div className="grid h-[26mm] w-[26mm] place-items-center border border-ink text-[9px]">
              no code
            </div>
          ) : (
            <div
              className="h-[26mm] w-[26mm] [&>svg]:h-full [&>svg]:w-full"
              // The library returns a complete SVG string; there is no user input in it,
              // only the barcode this server issued.
              dangerouslySetInnerHTML={{ __html: qr }}
            />
          )}
          {label.productCode !== null && (
            <p className="mt-1 font-mono text-[10px]">{label.productCode}</p>
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
    <div className="flex justify-between gap-2 border-b border-dotted border-ink/30 py-0.5">
      <span className="text-ink/70">{label}</span>
      <span className={`font-semibold ${mono ? 'font-mono' : ''}`}>{value}</span>
    </div>
  );
}
