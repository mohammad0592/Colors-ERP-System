import { useQuery } from '@tanstack/react-query';
import { useTranslation } from '../../hooks/useTranslation';
import { useState, type ReactElement } from 'react';
import { Link } from 'react-router-dom';
import { formatDate } from '../shifts/shiftFormat';
import { producedStockApi, type ProducedKind } from './api';
import { LabelPrintScreen } from './LabelPrintScreen';

/**
 * Rolls, bags and pallets as stock (specification sections 8 to 10).
 *
 * Three tables, one list. The storekeeper asking "where is this?" has a label in his
 * hand and does not know or care which of the three he is holding, so the search box
 * takes any of them — a barcode, a roll code, a colour, a product.
 *
 * Every status is shown, not only the usable ones. A roll that was scrapped and a bag
 * that is already on a pallet are exactly what somebody is looking for when they cannot
 * find something.
 */
export function ProducedStockTab(): ReactElement {
  const { t } = useTranslation();
  const [kind, setKind] = useState<ProducedKind | 'All'>('All');
  const [status, setStatus] = useState('');
  const [search, setSearch] = useState('');
  const [labelFor, setLabelFor] = useState<string | null>(null);

  const items = useQuery({
    queryKey: ['produced-stock', kind, status, search],
    queryFn: () =>
      producedStockApi.list({
        ...(kind === 'All' ? {} : { kind }),
        status,
        search,
      }),
  });

  // The statuses actually present, so the filter never offers one that finds nothing.
  const statuses = [...new Set((items.data ?? []).map((i) => i.status))].sort();

  return (
    <>
      <div className="mb-5 flex flex-wrap items-end gap-3">
        <div className="min-w-60 flex-1">
          <label className="field-label" htmlFor="produced-search">
            {t('stock.find')}
          </label>
          <input
            id="produced-search"
            className="field-input"
            placeholder="Scan or type a barcode, a roll code, a colour…"
            autoComplete="off"
            value={search}
            onChange={(event) => {
              setSearch(event.target.value);
            }}
          />
        </div>

        <div>
          <label className="field-label" htmlFor="produced-status">
            {t('field.status')}
          </label>
          <select
            id="produced-status"
            className="field-input w-auto"
            value={status}
            onChange={(event) => {
              setStatus(event.target.value);
            }}
          >
            <option value="">{t('stock.anyStatus')}</option>
            {statuses.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </select>
        </div>
      </div>

      <section className="mb-5 flex flex-wrap gap-2">
        {(['All', 'Roll', 'Bag', 'Pallet'] as const).map((option) => (
          <Chip
            key={option}
            label={option === 'All' ? t('stock.everything') : `${option}s`}
            active={kind === option}
            onClick={() => {
              setKind(option);
              // A status that belongs to rolls means nothing once bags are showing.
              setStatus('');
            }}
          />
        ))}
      </section>

      {items.isPending && <p className="p-6 text-ink-muted">{t('common.loading')}</p>}
      {items.isError && <p className="p-6 text-bad">{t('stock.loadFailed')}</p>}

      {items.data !== undefined && (
        <div className="card overflow-x-auto">
          <table className="w-full text-start text-sm">
            <thead>
              <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
                <th className="px-4 py-3 font-semibold">{t('term.barcode')}</th>
                <th className="px-4 py-3 font-semibold">{t('stock.kind')}</th>
                <th className="px-4 py-3 font-semibold">{t('field.code')}</th>
                <th className="px-4 py-3 font-semibold">{t('stock.whatItIs')}</th>
                <th className="px-4 py-3 font-semibold">{t('field.status')}</th>
                <th className="px-4 py-3 font-semibold">{t('stock.where')}</th>
                <th className="px-4 py-3 text-end font-semibold">{t('field.weight')}</th>
                <th className="px-4 py-3 text-end font-semibold">{t('field.length')}</th>
                <th className="px-4 py-3 text-end font-semibold">{t('field.pieces')}</th>
                <th className="px-4 py-3 font-semibold">{t('field.made')}</th>
                <th className="px-4 py-3" />
              </tr>
            </thead>
            <tbody>
              {items.data.length === 0 && (
                <tr>
                  <td colSpan={11} className="px-4 py-8 text-center text-ink-muted">
                    {search === '' && status === ''
                      ? t('stock.nothingMade')
                      : t('stock.noMatch')}
                  </td>
                </tr>
              )}
              {items.data.map((item) => (
                <tr
                  key={`${item.kind}-${String(item.id)}`}
                  className="border-b border-line last:border-0"
                >
                  <td className="px-4 py-3 font-mono font-semibold text-ink">
                    {item.barcode}
                  </td>
                  <td className="px-4 py-3 text-ink-soft">{item.kind}</td>
                  <td className="px-4 py-3 font-mono text-xs text-ink-soft">
                    {item.code}
                  </td>
                  <td className="px-4 py-3 text-ink-soft">{item.description}</td>
                  <td className="px-4 py-3">
                    <span
                      className={[
                        'rounded-full px-2.5 py-0.5 text-xs font-semibold whitespace-nowrap',
                        item.isAvailable
                          ? 'bg-ok-soft text-ok'
                          : 'bg-line text-ink-muted',
                      ].join(' ')}
                    >
                      {item.status}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-ink-soft">{item.whereabouts}</td>
                  <td className="px-4 py-3 text-end tabular-nums text-ink-soft">
                    {item.weight ?? '—'}
                  </td>
                  <td className="px-4 py-3 text-end tabular-nums text-ink-soft">
                    {item.length ?? '—'}
                  </td>
                  <td className="px-4 py-3 text-end tabular-nums text-ink-soft">
                    {item.pieceCount?.toLocaleString('en-GB') ?? '—'}
                  </td>
                  <td className="px-4 py-3 whitespace-nowrap text-ink-muted">
                    {formatDate(item.productionDate)}
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex justify-end gap-2">
                      {item.barcode !== '' && (
                        <Link
                          to={`/trace?code=${encodeURIComponent(item.barcode)}`}
                          className="grid min-h-9 place-items-center rounded-control border border-line px-3 text-sm font-medium whitespace-nowrap text-ink-soft transition-colors hover:border-brand-200 hover:bg-brand-50 hover:text-brand-700"
                        >
                          {t('stock.trace')}
                        </Link>
                      )}
                      {item.barcode !== '' && (
                        <button
                          type="button"
                          className="min-h-9 rounded-control border border-line px-3 text-sm font-medium whitespace-nowrap text-ink-soft transition-colors hover:border-brand-200 hover:bg-brand-50 hover:text-brand-700"
                          onClick={() => {
                            setLabelFor(item.barcode);
                          }}
                        >
                          {t('stock.label')}
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {labelFor !== null && (
        <LabelPrintScreen
          barcodes={[labelFor]}
          onClose={() => {
            setLabelFor(null);
          }}
        />
      )}
    </>
  );
}

function Chip({
  label,
  active,
  onClick,
}: {
  label: string;
  active: boolean;
  onClick: () => void;
}): ReactElement {
  return (
    <button
      type="button"
      onClick={onClick}
      className={[
        'min-h-9 rounded-full border px-4 text-sm font-medium transition-colors',
        active
          ? 'border-brand-600 bg-brand-50 text-brand-700'
          : 'border-line text-ink-soft hover:border-brand-200 hover:bg-brand-50',
      ].join(' ')}
    >
      {label}
    </button>
  );
}
