import type { ReactElement } from 'react';
import { Icon } from '../../components/ui/Icon';
import type { MaterialDto } from '../master-data/api';
import type { IngredientRow } from './ingredientRow';
import { emptyRow } from './ingredientRow';

interface IngredientEditorProps {
  rows: IngredientRow[];
  materials: MaterialDto[];
  disabled: boolean;
  onChange: (rows: IngredientRow[]) => void;
}

/**
 * The formula editor.
 *
 * The base-resin total is shown live, because the rule behind it is the one thing
 * about these recipes that surprises people: the percentages are parts per hundred
 * resin, so GPPS and Recycle must total 100 and every additive is measured against
 * them. The whole list deliberately does not add up to 100.
 */
export function IngredientEditor({
  rows,
  materials,
  disabled,
  onChange,
}: IngredientEditorProps): ReactElement {
  const baseTotal = rows
    .filter((r) => r.isBaseResin)
    .reduce((sum, r) => sum + (Number(r.target) || 0), 0);

  const hasBase = rows.some((r) => r.isBaseResin);
  const baseIsRight = Math.abs(baseTotal - 100) < 0.005;

  function update(index: number, patch: Partial<IngredientRow>): void {
    onChange(rows.map((row, i) => (i === index ? { ...row, ...patch } : row)));
  }

  return (
    <div>
      <div className="mb-2 flex items-center justify-between gap-3">
        <h3 className="text-sm font-semibold text-ink-soft">Materials</h3>

        {hasBase && (
          <span
            className={[
              'rounded-full px-3 py-1 text-xs font-semibold',
              baseIsRight ? 'bg-ok-soft text-ok' : 'bg-bad-soft text-bad',
            ].join(' ')}
          >
            Base resin {baseTotal.toFixed(2).replace(/\.00$/, '')}%
            {baseIsRight ? '' : ' — must be 100%'}
          </span>
        )}
      </div>

      <p className="mb-3 text-xs text-ink-muted">
        Tick <strong>base</strong> for GPPS and recycled material — together they must
        make 100%. Everything else is measured against that base, so the whole list does
        not add up to 100.
      </p>

      <div className="overflow-x-auto">
        <table className="w-full text-left text-sm">
          <thead>
            <tr className="text-xs tracking-wider text-ink-muted uppercase">
              <th className="pb-2 font-semibold">Material</th>
              <th className="w-16 pb-2 text-center font-semibold">Base</th>
              <th className="w-24 pb-2 font-semibold">Target %</th>
              <th className="w-24 pb-2 font-semibold">Min %</th>
              <th className="w-24 pb-2 font-semibold">Max %</th>
              <th className="w-10 pb-2" />
            </tr>
          </thead>
          <tbody>
            {rows.map((row, index) => {
              const chosen = new Set(
                rows.filter((_, i) => i !== index).map((r) => r.materialId),
              );

              return (
                <tr key={`ing-${String(index)}`}>
                  <td className="py-1 pr-2">
                    <select
                      aria-label="Material"
                      className="field-input h-touch text-base"
                      value={row.materialId}
                      onChange={(e) => {
                        update(index, { materialId: e.target.value });
                      }}
                      disabled={disabled}
                    >
                      <option value="">Choose…</option>
                      {materials
                        // A material may appear once per recipe, so hide the ones
                        // already used by another row.
                        .filter((m) => !chosen.has(String(m.id)))
                        .map((m) => (
                          <option key={m.id} value={m.id}>
                            {m.name}
                          </option>
                        ))}
                    </select>
                  </td>
                  <td className="py-1 text-center">
                    <input
                      type="checkbox"
                      aria-label="Base resin"
                      className="size-5"
                      checked={row.isBaseResin}
                      onChange={(e) => {
                        update(index, { isBaseResin: e.target.checked });
                      }}
                      disabled={disabled}
                    />
                  </td>
                  {(['target', 'min', 'max'] as const).map((field) => (
                    <td key={field} className="py-1 pr-2">
                      <input
                        type="number"
                        aria-label={`${field} percentage`}
                        min="0"
                        step="0.01"
                        className="field-input h-touch text-base"
                        value={row[field]}
                        onChange={(e) => {
                          update(index, { [field]: e.target.value });
                        }}
                        disabled={disabled}
                      />
                    </td>
                  ))}
                  <td className="py-1">
                    <button
                      type="button"
                      aria-label="Remove material"
                      className="grid size-9 place-items-center rounded-control text-ink-muted hover:bg-bad-soft hover:text-bad"
                      onClick={() => {
                        onChange(rows.filter((_, i) => i !== index));
                      }}
                      disabled={disabled}
                    >
                      <Icon name="close" className="size-4" />
                    </button>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      <button
        type="button"
        className="mt-2 min-h-9 rounded-control border border-dashed border-line px-3 text-sm font-medium text-ink-soft hover:border-brand-200 hover:text-brand-700"
        onClick={() => {
          onChange([...rows, emptyRow()]);
        }}
        disabled={disabled}
      >
        + Add material
      </button>
    </div>
  );
}
