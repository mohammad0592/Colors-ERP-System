import type { ReactElement } from 'react';
import { useTranslation } from '../../hooks/useTranslation';
import type { DateRange } from './dateRange';

/** The days a range report covers. The value itself lives on the Reports screen. */
export function DateRangePicker({
  range,
  onChange,
}: {
  range: DateRange;
  onChange: (range: DateRange) => void;
}): ReactElement {
  const { t } = useTranslation();
  return (
    <section className="card mb-4 flex flex-wrap items-end gap-4 p-4">
      <div>
        <label className="field-label" htmlFor="range-from">
          {t('field.from')}
        </label>
        <input
          id="range-from"
          type="date"
          className="field-input"
          value={range.from}
          onChange={(event) => {
            onChange({ ...range, from: event.target.value });
          }}
        />
      </div>
      <div>
        <label className="field-label" htmlFor="range-to">
          To
        </label>
        <input
          id="range-to"
          type="date"
          className="field-input"
          value={range.to}
          onChange={(event) => {
            onChange({ ...range, to: event.target.value });
          }}
        />
      </div>
    </section>
  );
}
