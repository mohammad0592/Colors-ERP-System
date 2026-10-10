import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from '../../hooks/useTranslation';
import { useState, type ReactElement } from 'react';
import { ConfirmDialog, type ConfirmRequest } from '../../components/ui/ConfirmDialog';
import { Modal } from '../../components/ui/Modal';
import { ApiError } from '../../lib/apiClient';
import { recipesApi } from '../recipes/api';
import { productsApi, productTypesApi, type ProductDto, type SaveProduct } from './api';
import { RowButton, StatusBadge } from './LookupTab';

/**
 * The things the factory makes (specification section 4).
 *
 * Its own tab rather than a LookupTab, because a product carries the packing numbers —
 * and those numbers are the whole point: they are what stops 500 and 15 being written
 * into the code when the factory starts making meal boxes.
 */
export function ProductsTab(): ReactElement {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState<ProductDto | 'new' | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [confirm, setConfirm] = useState<ConfirmRequest | null>(null);

  const products = useQuery({
    queryKey: ['products'],
    queryFn: () => productsApi.list(true),
  });

  const types = useQuery({
    queryKey: ['product-types'],
    queryFn: () => productTypesApi.list(false),
  });
  // Only the main recipes in use: a product is never made from a retired one.
  const families = useQuery({
    queryKey: ['recipe-families'],
    queryFn: () => recipesApi.families(false),
  });

  function invalidate(): void {
    void queryClient.invalidateQueries({ queryKey: ['products'] });
  }

  const setActive = useMutation({
    mutationFn: ({ id, isActive }: { id: number; isActive: boolean }) =>
      productsApi.setActive(id, isActive),
    onSettled: invalidate,
  });

  const remove = useMutation({
    mutationFn: (id: number) => productsApi.remove(id),
    onSuccess: () => {
      setActionError(null);
    },
    onError: (caught) => {
      setActionError(
        caught instanceof ApiError ? caught.message : t('common.deleteFailed'),
      );
    },
    onSettled: invalidate,
  });

  if (products.isPending || types.isPending || families.isPending) {
    return <p className="p-6 text-ink-muted">{t('common.loading')}</p>;
  }

  if (products.isError || types.isError || families.isError) {
    return <p className="p-6 text-bad">{t('md.productsFailed')}</p>;
  }

  return (
    <div>
      <div className="mb-4 flex items-center justify-between gap-3">
        <p className="text-sm text-ink-muted">
          {products.data.length} product{products.data.length === 1 ? '' : 's'}
        </p>
        <button
          type="button"
          className="btn-primary h-touch w-auto px-5 text-base"
          onClick={() => {
            setEditing('new');
          }}
        >
          {t('md.addProduct')}
        </button>
      </div>

      {actionError !== null && (
        <p
          role="alert"
          className="mb-4 rounded-control border border-s-4 border-bad/30 border-s-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
        >
          {actionError}
        </p>
      )}

      <div className="card overflow-x-auto">
        <table className="w-full text-start text-sm">
          <thead>
            <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
              <th className="px-4 py-3 font-semibold">{t('term.product')}</th>
              <th className="px-4 py-3 font-semibold">{t('md.type')}</th>
              <th className="px-4 py-3 font-semibold">{t('md.madeFrom')}</th>
              <th className="px-4 py-3 font-semibold">{t('md.piecesPerBagShort')}</th>
              <th className="px-4 py-3 font-semibold">{t('md.smallBagsShort')}</th>
              <th className="px-4 py-3 font-semibold">{t('md.bagsPerPalletShort')}</th>
              <th className="px-4 py-3 font-semibold">{t('md.thickness')}</th>
              <th className="px-4 py-3 font-semibold">{t('field.status')}</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody>
            {products.data.map((product) => (
              <tr key={product.id} className="border-b border-line last:border-0">
                <td className="px-4 py-3 font-medium text-ink">{product.name}</td>
                <td className="px-4 py-3 text-ink-soft">{product.productTypeName}</td>
                <td className="px-4 py-3 text-ink-soft">
                  {product.recipeFamilyName ?? '—'}
                </td>
                <td className="px-4 py-3 text-ink-soft">{product.piecesPerBag}</td>
                <td className="px-4 py-3 text-ink-soft">{product.smallBagsPerBag}</td>
                <td className="px-4 py-3 text-ink-soft">{product.bagsPerPallet}</td>
                <td className="px-4 py-3 whitespace-nowrap text-ink-soft">
                  {rangeText(product.minThickness, product.maxThickness)}
                </td>
                <td className="px-4 py-3">
                  <StatusBadge isActive={product.isActive} />
                </td>
                <td className="px-4 py-3">
                  <div className="flex justify-end gap-2">
                    <RowButton
                      label={t('action.edit')}
                      onClick={() => {
                        setEditing(product);
                      }}
                    />
                    <RowButton
                      label={
                        product.isActive ? t('common.deactivate') : t('common.activate')
                      }
                      onClick={() => {
                        setActive.mutate({
                          id: product.id,
                          isActive: !product.isActive,
                        });
                      }}
                    />
                    {product.canDelete && (
                      <RowButton
                        label={t('action.delete')}
                        tone="danger"
                        onClick={() => {
                          setConfirm({
                            title: t('md.deleteProduct'),
                            message: (
                              <>
                                <strong>{product.name}</strong> will be removed for good.
                                Nothing uses it, so no records are affected.
                              </>
                            ),
                            confirmLabel: t('action.delete'),
                            onConfirm: () => {
                              remove.mutate(product.id);
                            },
                          });
                        }}
                      />
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {confirm !== null && (
        <ConfirmDialog
          request={confirm}
          onCancel={() => {
            setConfirm(null);
          }}
        />
      )}

      {editing !== null && (
        <ProductDialog
          product={editing === 'new' ? null : editing}
          types={types.data}
          families={families.data}
          onClose={() => {
            setEditing(null);
          }}
          onSaved={invalidate}
        />
      )}
    </div>
  );
}

interface Named {
  id: number;
  name: string;
}

function ProductDialog({
  product,
  types,
  families,
  onClose,
  onSaved,
}: {
  product: ProductDto | null;
  types: Named[];
  families: Named[];
  onClose: () => void;
  onSaved: () => void;
}): ReactElement {
  const { t } = useTranslation();
  const [name, setName] = useState(product?.name ?? '');
  const [productTypeId, setProductTypeId] = useState(
    product?.productTypeId ?? types[0]?.id ?? 0,
  );
  // No default for a new product: picking the wrong recipe would let rolls of it be
  // logged for this product, so the choice is made, not inherited.
  const [recipeFamilyId, setRecipeFamilyId] = useState(product?.recipeFamilyId ?? 0);
  const [piecesPerBag, setPiecesPerBag] = useState(String(product?.piecesPerBag ?? 250));
  const [smallBagsPerBag, setSmallBagsPerBag] = useState(
    String(product?.smallBagsPerBag ?? 1),
  );
  const [bagsPerPallet, setBagsPerPallet] = useState(
    String(product?.bagsPerPallet ?? 21),
  );
  // Text, so an empty box means "not set" rather than zero. Most products will stay
  // empty until the factory measures them, and that is fine (section 19.1).
  const [minThickness, setMinThickness] = useState(
    product?.minThickness === null || product?.minThickness === undefined
      ? ''
      : String(product.minThickness),
  );
  const [maxThickness, setMaxThickness] = useState(
    product?.maxThickness === null || product?.maxThickness === undefined
      ? ''
      : String(product.maxThickness),
  );
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  async function save(): Promise<void> {
    setError(null);
    setIsSaving(true);
    try {
      const body: SaveProduct = {
        name,
        productTypeId,
        recipeFamilyId,
        piecesPerBag: Number(piecesPerBag),
        smallBagsPerBag: Number(smallBagsPerBag),
        bagsPerPallet: Number(bagsPerPallet),
        minThickness: minThickness.trim() === '' ? null : Number(minThickness),
        maxThickness: maxThickness.trim() === '' ? null : Number(maxThickness),
      };

      if (product === null) {
        await productsApi.create(body);
      } else {
        await productsApi.update(product.id, body);
      }
      onSaved();
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
    <Modal
      title={product === null ? t('md.addProduct') : t('md.editProduct')}
      onClose={onClose}
    >
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
        noValidate
      >
        <div className="mb-4">
          <label className="field-label" htmlFor="prod-name">
            {t('field.name')}
          </label>
          <input
            id="prod-name"
            className="field-input"
            value={name}
            maxLength={100}
            disabled={isSaving}
            onChange={(event) => {
              setName(event.target.value);
            }}
          />
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <div className="mb-4">
            <label className="field-label" htmlFor="prod-type">
              {t('md.productType')}
            </label>
            <select
              id="prod-type"
              className="field-input"
              value={productTypeId}
              disabled={isSaving}
              onChange={(event) => {
                setProductTypeId(Number(event.target.value));
              }}
            >
              {types.map((type) => (
                <option key={type.id} value={type.id}>
                  {type.name}
                </option>
              ))}
            </select>
          </div>
        </div>

        <div className="mb-4">
          <label className="field-label" htmlFor="prod-family">
            {t('md.madeFrom')}
          </label>
          <select
            id="prod-family"
            className="field-input"
            value={recipeFamilyId}
            disabled={isSaving}
            onChange={(event) => {
              setRecipeFamilyId(Number(event.target.value));
            }}
          >
            <option value={0}>{t('action.choose')}</option>
            {families.map((family) => (
              <option key={family.id} value={family.id}>
                {family.name}
              </option>
            ))}
          </select>
          <p className="mt-1 text-xs text-ink-muted">{t('md.madeFromHint')}</p>
        </div>

        <div className="grid gap-4 sm:grid-cols-3">
          <NumberField
            id="prod-pieces"
            label={t('md.piecesPerBag')}
            value={piecesPerBag}
            onChange={setPiecesPerBag}
            disabled={isSaving}
          />
          <NumberField
            id="prod-small"
            label={t('md.smallBagsPerBag')}
            value={smallBagsPerBag}
            onChange={setSmallBagsPerBag}
            disabled={isSaving}
          />
          <NumberField
            id="prod-pallet"
            label={t('md.bagsPerPallet')}
            value={bagsPerPallet}
            onChange={setBagsPerPallet}
            disabled={isSaving}
          />
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <NumberField
            id="prod-min-thickness"
            label={t('md.minThickness')}
            value={minThickness}
            onChange={setMinThickness}
            disabled={isSaving}
            step="0.01"
            min="0"
          />
          <NumberField
            id="prod-max-thickness"
            label={t('md.maxThickness')}
            value={maxThickness}
            onChange={setMaxThickness}
            disabled={isSaving}
            step="0.01"
            min="0"
          />
        </div>
        <p className="-mt-2 mb-4 text-xs text-ink-muted">{t('md.thicknessNote')}</p>

        {error !== null && (
          <p
            role="alert"
            className="mb-4 rounded-control border border-s-4 border-bad/30 border-s-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
          >
            {error}
          </p>
        )}

        <button type="submit" className="btn-primary" disabled={isSaving}>
          {isSaving ? 'Saving…' : t('common.save')}
        </button>
      </form>
    </Modal>
  );
}

/** "2.8–3.0 mm", "≥ 3.0 mm", or a dash when nobody has set one yet. */
function rangeText(min: number | null, max: number | null): string {
  if (min !== null && max !== null) {
    return `${String(min)}–${String(max)} mm`;
  }
  if (min !== null) {
    return `≥ ${String(min)} mm`;
  }
  if (max !== null) {
    return `≤ ${String(max)} mm`;
  }
  return '—';
}

function NumberField({
  id,
  label,
  value,
  onChange,
  disabled,
  step = '1',
  min = '1',
}: {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  disabled: boolean;
  /** Whole numbers for the packing counts; hundredths of a millimetre for thickness. */
  step?: string;
  min?: string;
}): ReactElement {
  return (
    <div className="mb-4">
      <label className="field-label" htmlFor={id}>
        {label}
      </label>
      <input
        id={id}
        type="number"
        min={min}
        step={step}
        className="field-input"
        value={value}
        disabled={disabled}
        onChange={(event) => {
          onChange(event.target.value);
        }}
      />
    </div>
  );
}
