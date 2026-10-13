import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useCallback, useMemo, useState, type ReactElement } from 'react';
import { navigation } from '../../components/layout/navigation';
import { ConfirmDialog, type ConfirmRequest } from '../../components/ui/ConfirmDialog';
import { PageHeader } from '../../components/ui/PageHeader';
import { useTranslation } from '../../hooks/useTranslation';
import { ApiError } from '../../lib/apiClient';
import { LabelPrintScreen } from '../labels/LabelPrintScreen';
import { printJobsApi, type PrintJobDto } from './api';

const autoPrintKey = 'colors.autoPrint';

function readAutoPrint(): boolean {
  try {
    return window.localStorage.getItem(autoPrintKey) === 'on';
  } catch {
    return false;
  }
}

function saveAutoPrint(on: boolean): void {
  try {
    window.localStorage.setItem(autoPrintKey, on ? 'on' : 'off');
  } catch {
    // The switch still works for this visit; it just will not be remembered.
  }
}

/**
 * The label printer's queue, open on the computer the printer hangs off
 * (specification section 19.13).
 *
 * The tablets have no printer, so their Print button sends the labels here. This screen
 * asks for waiting jobs every few seconds and prints them — by itself when Auto-print is
 * on and the computer's Chrome is set to print without asking, or at the press of a
 * button otherwise. A printed job stays in the history below, to print again.
 */
export function PrintQueuePage(): ReactElement {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [autoPrint, setAutoPrint] = useState(readAutoPrint);
  const [printing, setPrinting] = useState<{ job: PrintJobDto; auto: boolean } | null>(
    null,
  );
  const [error, setError] = useState<string | null>(null);
  // "Print all": automatic for as long as there is something waiting, then off again.
  const [draining, setDraining] = useState(false);
  const [confirm, setConfirm] = useState<ConfirmRequest | null>(null);

  // Asked often: a label is wanted on the roll while the man is still standing there.
  const pending = useQuery({
    queryKey: ['print-jobs', 'pending'],
    queryFn: () => printJobsApi.pending(),
    refetchInterval: 3000,
  });

  const recent = useQuery({
    queryKey: ['print-jobs', 'recent'],
    queryFn: () => printJobsApi.recent(30),
    refetchInterval: 15000,
  });

  function refresh(): void {
    void queryClient.invalidateQueries({ queryKey: ['print-jobs'] });
  }

  const markPrinted = useMutation({
    mutationFn: (id: number) => printJobsApi.markPrinted(id),
    onSettled: refresh,
    onError: (caught, id) => {
      // Back on the list: it printed, but the server did not hear so.
      setDone((current) => {
        const kept = new Set(current);
        kept.delete(id);
        return kept;
      });
      // Stop rather than print the same job over and over while the server refuses.
      setAutoPrint(false);
      setDraining(false);
      setError(
        caught instanceof ApiError ? caught.message : t('common.somethingWentWrong'),
      );
    },
  });

  const cancel = useMutation({
    mutationFn: (id: number) => printJobsApi.cancel(id),
    onSettled: refresh,
  });

  // Automatic: take the oldest waiting job whenever nothing is printing.
  // Jobs printed here and not yet gone from the list the server last sent. Without this a
  // job would print a second time in the moment between printing it and the list
  // catching up.
  const [done, setDone] = useState<ReadonlySet<number>>(() => new Set());
  const waiting = useMemo(
    () => (pending.data ?? []).filter((job) => !done.has(job.id)),
    [pending.data, done],
  );

  // Automatic: the oldest waiting job, whenever nothing else is printing. Worked out from
  // the list rather than stored, so it follows the queue as it changes.
  const automatic = autoPrint || draining;
  const nextAuto = automatic && printing === null ? waiting[0] : undefined;
  const active = useMemo(
    () => printing ?? (nextAuto === undefined ? null : { job: nextAuto, auto: true }),
    [printing, nextAuto],
  );

  const printed = useCallback(() => {
    if (active === null) {
      return;
    }

    const { job } = active;
    setPrinting(null);
    if (job.status === 'Pending') {
      setDone((current) => new Set(current).add(job.id));
      markPrinted.mutate(job.id);
    }

    // "Print all" ends with the last job it found waiting.
    if (draining && waiting.every((w) => w.id === job.id)) {
      setDraining(false);
    }
  }, [active, markPrinted, draining, waiting]);

  /** "Roll Production", from the path the job was sent from. */
  function sourceName(path: string): string {
    const item = navigation.flatMap((g) => g.items).find((i) => i.path === path);
    return item === undefined ? path : t(item.label);
  }

  function when(moment: string): string {
    return new Date(moment).toLocaleTimeString('en-GB', {
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  const history = (recent.data ?? []).filter((job) => job.status !== 'Pending');

  return (
    <>
      <PageHeader
        title={t('page.printQueue.title')}
        subtitle={t('page.printQueue.subtitle')}
        actions={
          <label className="flex items-center gap-3 rounded-control border border-line px-4 py-2 text-sm font-semibold text-ink">
            <input
              type="checkbox"
              className="size-5"
              checked={autoPrint}
              onChange={(event) => {
                setAutoPrint(event.target.checked);
                saveAutoPrint(event.target.checked);
                setError(null);
              }}
            />
            {t('print.autoPrint')}
          </label>
        }
      />

      {error !== null && (
        <p
          role="alert"
          className="mb-4 rounded-control border border-s-4 border-bad/30 border-s-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
        >
          {error}
        </p>
      )}

      <section className="mb-8">
        <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
          <h2 className="text-lg font-bold text-ink">
            {t('print.waiting')} ({waiting.length})
          </h2>
          {!autoPrint && waiting.length > 0 && (
            <button
              type="button"
              className="btn-primary w-auto px-5"
              disabled={active !== null}
              onClick={() => {
                setDraining(true);
              }}
            >
              {t('print.printAll')}
            </button>
          )}
        </div>

        {pending.isPending && <p className="text-ink-muted">{t('common.loading')}</p>}
        {pending.isError && <p className="text-bad">{t('print.loadFailed')}</p>}

        {pending.data !== undefined && waiting.length === 0 && (
          <p className="rounded-control border border-line bg-canvas px-4 py-6 text-center text-sm text-ink-muted">
            {t('print.nothingWaiting')}
          </p>
        )}

        <ul className="space-y-3">
          {waiting.map((job) => (
            <li
              key={job.id}
              className="card flex flex-wrap items-center justify-between gap-4 p-4"
            >
              <div className="min-w-0">
                <p className="font-semibold text-ink">
                  {job.headline ?? `${String(job.labelCount)} ${t('print.labels')}`}
                </p>
                <p className="mt-1 text-sm text-ink-muted">
                  {sourceName(job.source)} · {job.requestedByName} ·{' '}
                  {when(job.requestedAt)} · {job.labelCount} {t('print.labels')}
                </p>
                <p className="mt-1 truncate font-mono text-xs text-ink-muted">
                  {job.barcodes.join(', ')}
                </p>
              </div>
              <div className="flex shrink-0 gap-2">
                <button
                  type="button"
                  className="btn-primary w-auto px-5"
                  disabled={active !== null}
                  onClick={() => {
                    setPrinting({ job, auto: false });
                  }}
                >
                  {t('print.print')}
                </button>
                <button
                  type="button"
                  className="min-h-touch rounded-control border border-line px-4 text-sm font-medium text-ink-muted transition-colors hover:border-bad/40 hover:bg-bad-soft hover:text-bad"
                  onClick={() => {
                    setConfirm({
                      title: t('print.cancelTitle'),
                      message: t('print.cancelMessage'),
                      confirmLabel: t('print.cancel'),
                      onConfirm: () => {
                        cancel.mutate(job.id);
                      },
                    });
                  }}
                >
                  {t('print.cancel')}
                </button>
              </div>
            </li>
          ))}
        </ul>
      </section>

      <section className="mb-8">
        <h2 className="mb-3 text-lg font-bold text-ink">{t('print.history')}</h2>
        {history.length === 0 ? (
          <p className="text-sm text-ink-muted">—</p>
        ) : (
          <div className="card overflow-x-auto">
            <table className="w-full text-start text-sm">
              <tbody>
                {history.map((job) => (
                  <tr key={job.id} className="border-b border-line last:border-0">
                    <td className="px-4 py-3 text-ink">
                      {job.headline ?? `${String(job.labelCount)} ${t('print.labels')}`}
                      <span className="ms-2 text-xs text-ink-muted">
                        {sourceName(job.source)} · {job.requestedByName}
                      </span>
                    </td>
                    <td className="px-4 py-3 whitespace-nowrap text-ink-muted">
                      {job.status === 'Printed'
                        ? t('print.printedAt')
                        : t('print.cancelledAt')}{' '}
                      {job.doneAt === null ? '—' : when(job.doneAt)}
                      {job.doneByName !== null && ` · ${job.doneByName}`}
                    </td>
                    <td className="px-4 py-3 text-end">
                      <button
                        type="button"
                        className="min-h-9 rounded-control border border-line px-3 text-sm font-medium whitespace-nowrap text-ink-soft transition-colors hover:border-brand-200 hover:bg-brand-50 hover:text-brand-700"
                        disabled={active !== null}
                        onClick={() => {
                          setPrinting({ job, auto: false });
                        }}
                      >
                        {t('print.reprint')}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {/* How to make it print without anyone pressing anything. */}
      <details className="card mb-8 p-5 text-sm text-ink-soft">
        <summary className="cursor-pointer font-semibold text-ink">
          {t('print.setupTitle')}
        </summary>
        <ol className="mt-3 list-decimal space-y-2 ps-5">
          <li>{t('print.setup1')}</li>
          <li>{t('print.setup2')}</li>
          <li>
            {t('print.setup3')}
            <code className="mt-1 block rounded bg-canvas px-2 py-1 font-mono text-xs break-all text-ink">
              chrome.exe --kiosk-printing {window.location.origin}/print-queue
            </code>
          </li>
          <li>{t('print.setup4')}</li>
        </ol>
      </details>

      {active !== null && (
        <LabelPrintScreen
          key={active.job.id}
          barcodes={active.job.barcodes}
          {...(active.job.headline === null ? {} : { headline: active.job.headline })}
          direct
          autoPrint={active.auto}
          onPrinted={printed}
          onClose={() => {
            // Closed without printing. In automatic mode, stop, or the same job would open
            // again at once.
            if (active.auto) {
              setAutoPrint(false);
              saveAutoPrint(false);
              setDraining(false);
            }
            setPrinting(null);
          }}
        />
      )}

      {confirm !== null && (
        <ConfirmDialog
          request={confirm}
          onCancel={() => {
            setConfirm(null);
          }}
        />
      )}
    </>
  );
}
