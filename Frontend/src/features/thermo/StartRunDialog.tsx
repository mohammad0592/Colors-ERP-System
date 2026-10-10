import { useQuery } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { useTranslation } from '../../hooks/useTranslation';
import { Modal } from '../../components/ui/Modal';
import { ScanField } from '../../components/ui/ScanField';
import { ApiError } from '../../lib/apiClient';
import { cleanCode, type EntryMethod } from '../../lib/barcodeScanner';
import { productsApi } from '../master-data/api';
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
 * Nothing else is asked for. The recipe, the colour and the product all come from the
 * roll, so there is nothing here to get wrong — except for a roll made before rolls
 * named their product, which is asked for its product here, once.
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
  const needsProduct = chosen !== null && chosen.productId === null;
  const [productId, setProductId] = useState(0);

  // Only fetched for an old roll, which is the one case that asks.
  const products = useQuery({
    queryKey: ['products', 'active'],
    queryFn: () => productsApi.list(false),
    enabled: needsProduct,
  });
  const fitting = (products.data ?? []).filter(
    (p) => chosen === null || p.isAbsorbent === chosen.isAbsorbent,
  );

  async function start(code: string, entry: EntryMethod): Promise<void> {
    if (code === '') {
      return;
    }

    if (needsProduct && productId === 0) {
      setError(t('thermo.oldRollProduct'));
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
          productId: needsProduct ? productId : null,
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
              label: `${roll.rollCode} — ${roll.colorName}, ${roll.recipeFamilyName}, ${formatDate(roll.productionDate)}`,
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
            {chosen.isAbsorbent && (
              <span className="ms-2 rounded-full bg-brand-50 px-2 py-0.5 text-xs font-semibold text-brand-700">
                {t('term.absorbent')}
              </span>
            )}
          </p>
        )}

        {needsProduct && (
          <div className="mb-4">
            <label className="field-label" htmlFor="old-roll-product">
              {t('term.product')}
            </label>
            <select
              id="old-roll-product"
              className="field-input"
              value={productId}
              disabled={isSaving}
              onChange={(event) => {
                setProductId(Number(event.target.value));
              }}
            >
              <option value={0}>{t('action.choose')}</option>
              {fitting.map((product) => (
                <option key={product.id} value={product.id}>
                  {product.name}
                </option>
              ))}
            </select>
            <p className="mt-1 text-xs text-ink-muted">{t('thermo.oldRollProduct')}</p>
          </div>
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
