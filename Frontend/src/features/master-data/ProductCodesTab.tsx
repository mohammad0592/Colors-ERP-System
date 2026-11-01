import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { useTranslation } from '../../hooks/useTranslation';
import { ConfirmDialog, type ConfirmRequest } from '../../components/ui/ConfirmDialog';
import { Modal } from '../../components/ui/Modal';
import { ApiError } from '../../lib/apiClient';
import { colorsApi, productCodesApi, productsApi, type ProductCodeDto } from './api';
import { RowButton } from './LookupTab';

/**
 * The factory's product codes — the code printed on every bag of a product in a colour,
 * AB500B, LB03W (specification section 19.25).
 *
 * A list rather than a rule, because the factory's codes follow none: blue is BLUE on one
 * plate and BL on another, and the burger boxes have one code whatever their colour,
 * written here under "Every colour".
 */
export function ProductCodesTab(): ReactElement {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState<ProductCodeDto | 'new' | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [confirm, setConfirm] = useState<ConfirmRequest | null>(null);

  const codes = useQuery({ queryKey: ['product-codes'], queryFn: productCodesApi.list });
  const products = useQuery({
    queryKey: ['products'],
    queryFn: () => productsApi.list(true),
  });
  const colours = useQuery({
    queryKey: ['colors'],
    queryFn: () => colorsApi.list(true),
  });

  function invalidate(): void {
    void queryClient.invalidateQueries({ queryKey: ['product-codes'] });
  }

  const remove = useMutation({
    mutationFn: (id: number) => productCodesApi.remove(id),
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

  if (codes.isPending || products.isPending || colours.isPending) {
    return <p className="p-6 text-ink-muted">{t('common.loading')}</p>;
  }

  if (codes.isError || products.isError || colours.isError) {
    return <p className="p-6 text-bad">{t('md.productCodesFailed')}</p>;
  }

  return (
    <div>
      <div className="mb-4 flex items-center justify-between gap-3">
        <p className="text-sm text-ink-muted">{t('md.productCodesHint')}</p>
        <button
          type="button"
          className="btn-primary h-touch w-auto shrink-0 px-5 text-base"
          onClick={() => {
            setEditing('new');
          }}
        >
          {t('md.addProductCode')}
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
              <th className="px-4 py-3 font-semibold">{t('md.productCode')}</th>
              <th className="px-4 py-3 font-semibold">{t('term.product')}</th>
              <th className="px-4 py-3 font-semibold">{t('term.colour')}</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody>
            {codes.data.map((code) => (
              <tr key={code.id} className="border-b border-line last:border-0">
                <td className="px-4 py-3 font-mono font-semibold text-ink">
                  {code.code}
                </td>
                <td className="px-4 py-3 text-ink-soft">{code.productName}</td>
                <td className="px-4 py-3 text-ink-soft">
                  {code.colorName ?? t('md.everyColour')}
                </td>
                <td className="px-4 py-3">
                  <div className="flex justify-end gap-2">
                    <RowButton
                      label={t('action.edit')}
                      onClick={() => {
                        setEditing(code);
                      }}
                    />
                    <RowButton
                      label={t('action.delete')}
                      tone="danger"
                      onClick={() => {
                        setConfirm({
                          title: t('md.deleteProductCode'),
                          message: (
                            <>
                              <strong>{code.code}</strong> —{' '}
                              {t('md.deleteProductCodeNote')}
                            </>
                          ),
                          confirmLabel: t('action.delete'),
                          onConfirm: () => {
                            remove.mutate(code.id);
                          },
                        });
                      }}
                    />
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
        <ProductCodeDialog
          code={editing === 'new' ? null : editing}
          products={products.data}
          colours={colours.data}
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

function ProductCodeDialog({
  code,
  products,
  colours,
  onClose,
  onSaved,
}: {
  code: ProductCodeDto | null;
  products: Named[];
  colours: Named[];
  onClose: () => void;
  onSaved: () => void;
}): ReactElement {
  const { t } = useTranslation();
  const [productId, setProductId] = useState(code?.productId ?? 0);
  // 0 is "every colour" — the burger boxes' one code.
  const [colorId, setColorId] = useState(code?.colorId ?? 0);
  const [text, setText] = useState(code?.code ?? '');
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  async function save(): Promise<void> {
    setError(null);
    setIsSaving(true);
    try {
      const body = { productId, colorId: colorId === 0 ? null : colorId, code: text };
      if (code === null) {
        await productCodesApi.create(body);
      } else {
        await productCodesApi.update(code.id, body);
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
      title={code === null ? t('md.addProductCode') : t('md.editProductCode')}
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
          <label className="field-label" htmlFor="pc-code">
            {t('md.productCode')}
          </label>
          <input
            id="pc-code"
            className="field-input font-mono uppercase"
            value={text}
            maxLength={20}
            disabled={isSaving}
            onChange={(event) => {
              setText(event.target.value);
            }}
          />
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <div className="mb-4">
            <label className="field-label" htmlFor="pc-product">
              {t('term.product')}
            </label>
            <select
              id="pc-product"
              className="field-input"
              value={productId}
              disabled={isSaving}
              onChange={(event) => {
                setProductId(Number(event.target.value));
              }}
            >
              <option value={0}>{t('action.choose')}</option>
              {products.map((product) => (
                <option key={product.id} value={product.id}>
                  {product.name}
                </option>
              ))}
            </select>
          </div>

          <div className="mb-4">
            <label className="field-label" htmlFor="pc-colour">
              {t('term.colour')}
            </label>
            <select
              id="pc-colour"
              className="field-input"
              value={colorId}
              disabled={isSaving}
              onChange={(event) => {
                setColorId(Number(event.target.value));
              }}
            >
              <option value={0}>{t('md.everyColour')}</option>
              {colours.map((colour) => (
                <option key={colour.id} value={colour.id}>
                  {colour.name}
                </option>
              ))}
            </select>
          </div>
        </div>

        {error !== null && (
          <p
            role="alert"
            className="mb-4 rounded-control border border-s-4 border-bad/30 border-s-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
          >
            {error}
          </p>
        )}

        <button type="submit" className="btn-primary" disabled={isSaving}>
          {isSaving ? t('common.saving') : t('common.save')}
        </button>
      </form>
    </Modal>
  );
}
