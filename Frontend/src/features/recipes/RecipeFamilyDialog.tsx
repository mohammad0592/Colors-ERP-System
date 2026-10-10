import { useState, type ReactElement } from 'react';
import { Modal } from '../../components/ui/Modal';
import { useTranslation } from '../../hooks/useTranslation';
import { ApiError } from '../../lib/apiClient';
import { recipesApi, type RecipeFamilyDto } from './api';

interface RecipeFamilyDialogProps {
  onClose: () => void;
  /** Called with the new family, so its first formula can be written straight away. */
  onCreated: (family: RecipeFamilyDto) => void;
}

/**
 * A main recipe written from nothing — Normal, Absorbent, Lunch Box, or whatever the
 * factory tries next (specification section 5). Nothing is copied from an older one.
 *
 * Only the name, the code and whether it is absorbent are asked here. The formula is
 * its first version, written in the next dialog, because a version is what rolls point
 * at and what gets frozen once it is in production.
 */
export function RecipeFamilyDialog({
  onClose,
  onCreated,
}: RecipeFamilyDialogProps): ReactElement {
  const { t } = useTranslation();
  const [name, setName] = useState('');
  const [code, setCode] = useState('');
  const [isAbsorbent, setIsAbsorbent] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  async function save(): Promise<void> {
    setError(null);
    setIsSaving(true);
    try {
      const family = await recipesApi.createFamily({
        name: name.trim(),
        code: code.trim(),
        isAbsorbent,
        description: null,
      });
      onCreated(family);
    } catch (caught) {
      // The server owns the rules — a name or code already used — so its words are shown.
      setError(
        caught instanceof ApiError ? caught.message : t('common.somethingWentWrong'),
      );
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <Modal title={t('recipes.newMain')} onClose={onClose}>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
        noValidate
      >
        <div className="mb-4">
          <label className="field-label" htmlFor="family-name">
            {t('field.name')}
          </label>
          <input
            id="family-name"
            className="field-input"
            value={name}
            maxLength={100}
            disabled={isSaving}
            onChange={(event) => {
              setName(event.target.value);
            }}
          />
        </div>

        <div className="mb-4">
          <label className="field-label" htmlFor="family-code">
            {t('recipes.code')}
          </label>
          <input
            id="family-code"
            className="field-input w-32"
            value={code}
            maxLength={10}
            autoComplete="off"
            disabled={isSaving}
            onChange={(event) => {
              setCode(event.target.value);
            }}
          />
          <p className="mt-1 text-xs text-ink-muted">{t('recipes.codeHint')}</p>
        </div>

        <div className="mb-5">
          <label
            className="flex items-center gap-3 text-sm font-medium text-ink"
            htmlFor="family-abs"
          >
            <input
              id="family-abs"
              type="checkbox"
              className="size-5"
              checked={isAbsorbent}
              disabled={isSaving}
              onChange={(event) => {
                setIsAbsorbent(event.target.checked);
              }}
            />
            {t('term.absorbent')}
          </label>
          <p className="mt-1 ms-8 text-xs text-ink-muted">{t('recipes.absorbentHint')}</p>
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
          {isSaving ? t('common.saving') : t('recipes.createMain')}
        </button>
      </form>
    </Modal>
  );
}
