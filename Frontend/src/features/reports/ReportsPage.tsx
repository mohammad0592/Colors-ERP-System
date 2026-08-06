import { useQuery } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { PageHeader } from '../../components/ui/PageHeader';
import { shiftReportsApi } from '../shifts/api';
import { formatDate } from '../shifts/shiftFormat';
import { reportsApi } from './api';
import { ConsumptionReport } from './ConsumptionReport';
import { MaterialWasteReport } from './MaterialWasteReport';
import { ShiftSummaryReport } from './ShiftSummaryReport';

/**
 * Reports (specification section 13).
 *
 * Every one is a read over records that already exist — nothing here is stored, so a
 * report cannot disagree with the data underneath it.
 *
 * Two of them answer for one shift, so they share a shift picker. Consumption reads a
 * stretch of days instead and carries its own dates, so the picker steps out of its way.
 */
export function ReportsPage(): ReactElement {
  const [shiftReportId, setShiftReportId] = useState<number | null>(null);
  const [report, setReport] = useState<'waste' | 'summary' | 'consumption'>('waste');

  // Newest first: the shift being asked about is nearly always the last one.
  const shifts = useQuery({
    queryKey: ['shift-reports', 'for-reports'],
    queryFn: () => shiftReportsApi.list(),
  });

  const chosen = shiftReportId ?? shifts.data?.[0]?.id ?? null;

  const waste = useQuery({
    queryKey: ['report-waste', chosen],
    queryFn: () => reportsApi.materialWaste(chosen ?? 0),
    enabled: chosen !== null && report === 'waste',
  });

  const summary = useQuery({
    queryKey: ['report-summary', chosen],
    queryFn: () => reportsApi.shiftSummary(chosen ?? 0),
    enabled: chosen !== null && report === 'summary',
  });

  return (
    <>
      <PageHeader
        title="Reports"
        subtitle="Worked out from what the shift recorded, never typed and never stored — so a report cannot disagree with the data behind it."
      />

      {report !== 'consumption' && (
      <section className="card mb-6 p-4">
        <label className="field-label" htmlFor="report-shift">
          Shift
        </label>
        <select
          id="report-shift"
          className="field-input max-w-md"
          value={chosen ?? ''}
          disabled={shifts.isPending}
          onChange={(event) => {
            setShiftReportId(Number(event.target.value));
          }}
        >
          {(shifts.data ?? []).map((shift) => (
            <option key={shift.id} value={shift.id}>
              {formatDate(shift.productionDate)} · shift {shift.shiftName} ·{' '}
              {shift.status}
            </option>
          ))}
        </select>

        {shifts.data?.length === 0 && (
          <p className="mt-2 text-sm text-ink-muted">
            No shift has been opened yet, so there is nothing to report on.
          </p>
        )}
      </section>
      )}

      <section className="mb-6 flex flex-wrap gap-2">
        <Chip
          label="Material waste control"
          active={report === 'waste'}
          onClick={() => {
            setReport('waste');
          }}
        />
        <Chip
          label="Shift production summary"
          active={report === 'summary'}
          onClick={() => {
            setReport('summary');
          }}
        />
        <Chip
          label="Consumption"
          active={report === 'consumption'}
          onClick={() => {
            setReport('consumption');
          }}
        />
      </section>

      {report === 'consumption' ? (
        <ConsumptionReport />
      ) : chosen === null ? null : report === 'waste' ? (
        <>
          {waste.isPending && <p className="p-6 text-ink-muted">Loading…</p>}
          {waste.isError && <p className="p-6 text-bad">Could not load the report.</p>}
          {waste.data !== undefined && <MaterialWasteReport report={waste.data} />}
        </>
      ) : (
        <>
          {summary.isPending && <p className="p-6 text-ink-muted">Loading…</p>}
          {summary.isError && <p className="p-6 text-bad">Could not load the report.</p>}
          {summary.data !== undefined && <ShiftSummaryReport report={summary.data} />}
        </>
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
