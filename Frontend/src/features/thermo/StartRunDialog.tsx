import { useQuery } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { useTranslation } from '../../hooks/useTranslation';
import { Modal } from '../../components/ui/Modal';
import { ScanField } from '../../components/ui/ScanField';
import { ApiError } from '../../lib/apiClient';
import { cleanCode, type EntryMethod } from '../../lib/barcodeScanner';
import { productsApi } from '../master-data/api';
import { productsFor } from '../production/rollOptions';
import { formatDate } from '../shifts/shiftFormat';
import { thermoApi, type AvailableRollDto, type ThermoRunDto } from './api';

interface StartRunDialogProps {
  line: {
    shiftLineId: number;
    lineName: string;
    shiftLabel: string;
  };
  rolls: AvailableRollDto[];
  onClose: () => void;
  onStarted: (run: ThermoRunDto) => void;
}

/**
 * Putting a roll into the thermo (specification section 9).
 *
 * The scanner comes first, because that is what the floor does. Picking from the list is
 * there for the office, and for a label too torn to read — both in the one box, which is
 * `ScanField` and behaves the same wherever a code is asked for.
 *
 * The recipe and the colour come from the roll. The product is chosen here — the mould on
 * the machine is the product, and the extruder names only the recipe (section 19.17).
 *
 * It must be chosen for every roll, and starts empty. It used to be remembered from the
 * last roll, so a scan went straight through — and after a mould change the first rolls
 * went in under the old product, with nobody having chosen it. The bags take their code
 * from it, so a wrong one is a pallet of wrong labels. A roll that already has its product
 * — it went in before and came back unfinished — shows it, locked: it keeps it.
 */
export function StartRunDialog({
  line,
  rolls,
  onClose,
  onStarted,
}: StartRunDialogProps): ReactElement {
  const { t } = useTranslation();
  const [barcode, setBarcode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  // The list offers barcodes, so scanning and picking end at the same value and there is
  // no second piece of state that can disagree with the box.
  const chosen = rolls.find((r) => r.barcode === cleanCode(barcode)) ?? null;
  // Every roll asks, unless it was logged when the extruder still named its product.
  const needsProduct = (chosen?.productId ?? null) === null;
  const [picked, setPicked] = useState(0);
  // Set when a start is refused for want of a product, so the box itself turns red.
  const [productMissing, setProductMissing] = useState(false);

  const products = useQuery({
    queryKey: ['products', 'active'],
    queryFn: () => productsApi.list(false),
  });
  // Narrowed to the chosen roll's recipe once there is one; the server checks it anyway.
  const fitting =
    chosen === null ? (products.data ?? []) : productsFor(products.data ?? [], chosen);
  const productId = fitting.some((p) => p.id === picked) ? picked : 0;

  async function start(code: string, entry: EntryMethod): Promise<void> {
    if (code === '') {
      return;
    }

    // The roll a scan names may not be the one on screen yet, so the product is checked
    // against the roll the code belongs to.
    const scanned = rolls.find((r) => r.barcode === cleanCode(code)) ?? null;
    const asks = (scanned?.productId ?? null) === null;
    if (asks && productId === 0) {
      setProductMissing(true);
      setError(t('thermo.chooseProductFirst'));
      return;
    }

    setError(null);
    setIsSaving(true);
    try {
      const run = await thermoApi.startRun(
        {
          rollBarcode: code,
          rollId: null,
          shiftLineId: line.shiftLineId,
          startedAt: null,
          notes: null,
          productId: asks ? productId : null,
        },
        entry,
      );
      onStarted(run);
      onClose();
    } catch (caught) {
      setError(
        caught instanceof ApiError ? caught.message : t('common.somethingWentWrong'),
      );
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <Modal title={t('thermo.putRollIn')} onClose={onClose}>
      <div>
        <div className="mb-5 border-b border-line pb-4 text-sm text-ink-muted">
          <p>
            {line.lineName} — {line.shiftLabel}
          </p>
        </div>

        <div className="mb-4">
          <label className="field-label" htmlFor="thermo-product">
            {t('term.product')}
            {needsProduct && (
              <span className="ms-1 text-bad" aria-hidden="true">
                *
              </span>
            )}
          </label>
          {needsProduct ? (
            <select
              id="thermo-product"
              className={`field-input ${productMissing && productId === 0 ? 'border-bad' : ''}`}
              value={productId}
              disabled={isSaving}
              required
              aria-invalid={productMissing && productId === 0}
              onChange={(event) => {
                setPicked(Number(event.target.value));
                setProductMissing(false);
                setError(null);
              }}
            >
              <option value={0}>{t('action.choose')}</option>
              {fitting.map((product) => (
                <option key={product.id} value={product.id}>
                  {product.name}
                </option>
              ))}
            </select>
          ) : (
            <select id="thermo-product" className="field-input" disabled>
              <option>{chosen?.productName}</option>
            </select>
          )}
          <p className="mt-1 text-xs text-ink-muted">
            {needsProduct ? t('thermo.productBeingFormed') : t('thermo.productKept')}
          </p>
        </div>

        <div className="mb-4">
          <ScanField
            label={t('thermo.scanTheRoll')}
            placeholder="R000123"
            value={barcode}
            onChange={setBarcode}
            onSubmit={(code, entry) => {
              void start(code, entry);
            }}
            options={rolls.map((roll) => ({
              value: roll.barcode,
              label:
                `${roll.rollCode} — ${roll.colorName}, ${roll.recipeFamilyName}, ${formatDate(roll.productionDate)}` +
                (roll.notCompleted ? ` (${t('status.roll.notCompleted')})` : ''),
              code: roll.rollCode,
              title: `${t('term.recipe')} ${String(roll.recipeNumber)} · ${roll.recipeFamilyName}`,
              colour: roll.colorName,
              details: [
                formatDate(roll.productionDate),
                ...(roll.weight === null ? [] : [`${String(roll.weight)} kg`]),
                ...(roll.length === null
                  ? []
                  : [`${t('field.length')} ${String(roll.length)}`]),
                ...(roll.productName === null ? [] : [roll.productName]),
              ],
              // Back from the thermo unfinished: worth seeing before it goes in again.
              flag: roll.notCompleted ? t('status.roll.notCompleted') : undefined,
            }))}
            optionsHint="measured rolls in stock, oldest first"
            submitLabel="Start forming"
            busy={isSaving}
          />
        </div>

        {chosen !== null && (
          <p className="mb-4 rounded-control bg-canvas px-4 py-3 text-sm text-ink-soft">
            <strong className="font-mono text-ink">{chosen.rollCode}</strong> ·{' '}
            {chosen.colorName} · recipe {chosen.recipeNumber} {chosen.recipeFamilyName}
            {chosen.weight !== null && <> · {chosen.weight} kg</>}
            {chosen.length !== null && <> · length {chosen.length}</>}
            {chosen.productName !== null && (
              <>
                {' '}
                · <strong className="text-ink">{chosen.productName}</strong>
              </>
            )}
            {/* In before and taken out unfinished: it goes in again (section 19.23). */}
            {chosen.notCompleted && (
              <span className="ms-2 rounded-full bg-warn-soft px-2 py-0.5 text-xs font-semibold text-warn">
                {t('status.roll.notCompleted')}
              </span>
            )}
            {chosen.isAbsorbent && (
              <span className="ms-2 rounded-full bg-brand-50 px-2 py-0.5 text-xs font-semibold text-brand-700">
                {t('term.absorbent')}
              </span>
            )}
          </p>
        )}

        {rolls.length === 0 && (
          <p className="mb-4 rounded-control border border-line bg-canvas px-4 py-3 text-sm text-ink-soft">
            No roll has been measured and left in stock. A roll cannot come here until it
            has been measured, because once it is formed into plates there is nothing left
            to measure.
          </p>
        )}

        {error !== null && (
          <p
            role="alert"
            className="mb-4 rounded-control border border-s-4 border-bad/30 border-s-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
          >
            {error}
          </p>
        )}

        <p className="mt-2 text-xs text-ink-muted">{t('thermo.rollLeavesStore')}</p>
      </div>
    </Modal>
  );
}
