import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { PageHeader } from '../../components/ui/PageHeader';
import { ApiError } from '../../lib/apiClient';
import { materialsApi } from '../master-data/api';
import { recipesApi, type RecipeVersionDto } from './api';
import { RecipeStatusBadge } from './RecipeStatusBadge';
import { RecipeVersionDialog } from './RecipeVersionDialog';

/**
 * Recipes — the four families and every version of them.
 *
 * Old versions are never hidden: a supervisor comparing what changed between
 * recipe 2 and recipe 10 is exactly the improvement loop the specification
 * describes (section 5).
 */
export function RecipesPage(): ReactElement {
  const queryClient = useQueryClient();
  const [familyFilter, setFamilyFilter] = useState<number | 'all'>('all');
  const [dialog, setDialog] = useState<RecipeVersionDto | 'new' | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const families = useQuery({
    queryKey: ['recipe-families'],
    queryFn: () => recipesApi.families(false),
  });

  const versions = useQuery({
    queryKey: ['recipe-versions', familyFilter],
    queryFn: () => recipesApi.versions(familyFilter === 'all' ? undefined : familyFilter),
  });

  // Only active materials can go into a new recipe.
  const materials = useQuery({
    queryKey: ['materials', 'active'],
    queryFn: () => materialsApi.list(false),
  });

  function invalidate(): void {
    void queryClient.invalidateQueries({ queryKey: ['recipe-versions'] });
    void queryClient.invalidateQueries({ queryKey: ['recipe-families'] });
  }

  function onActionError(caught: unknown): void {
    setActionError(caught instanceof ApiError ? caught.message : 'Something went wrong.');
  }

  const copy = useMutation({
    mutationFn: (id: number) => recipesApi.copyVersion(id, null),
    onSuccess: (draft) => {
      setActionError(null);
      // Straight into the copy, because the reason to copy is to change something.
      setDialog(draft);
    },
    onError: onActionError,
    onSettled: invalidate,
  });

  const promote = useMutation({
    mutationFn: (id: number) => recipesApi.promoteVersion(id),
    onSuccess: () => {
      setActionError(null);
    },
    onError: onActionError,
    onSettled: invalidate,
  });

  const discard = useMutation({
    mutationFn: (id: number) => recipesApi.deleteDraft(id),
    onSuccess: () => {
      setActionError(null);
    },
    onError: onActionError,
    onSettled: invalidate,
  });

  const open = useMutation({
    mutationFn: (id: number) => recipesApi.version(id),
    onSuccess: (full) => {
      setDialog(full);
    },
    onError: onActionError,
  });

  if (families.isPending || versions.isPending || materials.isPending) {
    return <p className="p-6 text-ink-muted">Loading…</p>;
  }

  if (families.isError || versions.isError || materials.isError) {
    return <p className="p-6 text-bad">Could not load recipes.</p>;
  }

  return (
    <>
      <PageHeader
        title="Recipes"
        subtitle="The four families and every version. A recipe in production is never edited — copy it instead."
        actions={
          <button
            type="button"
            className="btn-primary h-touch w-auto px-5 text-base"
            onClick={() => {
              setDialog('new');
            }}
          >
            New recipe
          </button>
        }
      />

      {/* Families, with the recipe number each one is running now. */}
      <section className="mb-6 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        {families.data.map((family) => (
          <button
            key={family.id}
            type="button"
            onClick={() => {
              setFamilyFilter((current) => (current === family.id ? 'all' : family.id));
            }}
            className={[
              'card p-4 text-left transition-colors',
              familyFilter === family.id
                ? 'border-brand-600 bg-brand-50'
                : 'hover:border-brand-200',
            ].join(' ')}
          >
            <p className="font-semibold text-ink">{family.name}</p>
            <p className="mt-1 text-sm text-ink-muted">
              {family.currentRecipeNumber === null
                ? 'No recipe in production'
                : `Running recipe ${String(family.currentRecipeNumber)}`}
            </p>
            <div className="mt-2 flex flex-wrap gap-1.5">
              {family.usesRecycle && <Tag label="Uses recycle" />}
              {family.isAbsorbent && <Tag label="Absorbent" />}
              <Tag
                label={`${String(family.versionCount)} version${family.versionCount === 1 ? '' : 's'}`}
              />
            </div>
          </button>
        ))}
      </section>

      {actionError !== null && (
        <p
          role="alert"
          className="mb-4 rounded-control border border-l-4 border-bad/30 border-l-bad bg-bad-soft px-4 py-3 text-sm font-medium text-bad"
        >
          {actionError}
        </p>
      )}

      <div className="mb-3 flex items-center justify-between gap-3">
        <p className="text-sm text-ink-muted">
          {versions.data.length} recipe{versions.data.length === 1 ? '' : 's'}
          {familyFilter === 'all' ? '' : ' in this family'}
        </p>
        {familyFilter !== 'all' && (
          <button
            type="button"
            className="text-sm font-medium text-brand-700 hover:underline"
            onClick={() => {
              setFamilyFilter('all');
            }}
          >
            Show all families
          </button>
        )}
      </div>

      <div className="card overflow-x-auto">
        <table className="w-full text-left text-sm">
          <thead>
            <tr className="border-b border-line text-xs tracking-wider text-ink-muted uppercase">
              <th className="px-4 py-3 font-semibold">Recipe</th>
              <th className="px-4 py-3 font-semibold">Family</th>
              <th className="px-4 py-3 font-semibold">Version</th>
              <th className="px-4 py-3 font-semibold">Status</th>
              <th className="px-4 py-3 font-semibold">Materials</th>
              <th className="px-4 py-3 font-semibold">Written by</th>
              <th className="px-4 py-3 font-semibold">Notes</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody>
            {versions.data.map((version) => (
              <tr key={version.id} className="border-b border-line last:border-0">
                <td className="px-4 py-3 font-bold text-ink">{version.recipeNumber}</td>
                <td className="px-4 py-3 text-ink-soft">{version.familyName}</td>
                <td className="px-4 py-3 text-ink-soft">v{version.versionNumber}</td>
                <td className="px-4 py-3">
                  <RecipeStatusBadge status={version.status} />
                </td>
                <td className="px-4 py-3 text-ink-soft">{version.ingredientCount}</td>
                <td className="px-4 py-3 text-ink-soft">{version.createdByName}</td>
                <td className="max-w-xs truncate px-4 py-3 text-ink-muted">
                  {version.notes ?? '—'}
                </td>
                <td className="px-4 py-3">
                  <div className="flex justify-end gap-2">
                    <Action
                      label={version.isEditable ? 'Edit' : 'View'}
                      onClick={() => {
                        open.mutate(version.id);
                      }}
                    />
                    <Action
                      label="Copy"
                      onClick={() => {
                        copy.mutate(version.id);
                      }}
                    />
                    {version.status === 'Draft' && (
                      <>
                        <Action
                          label="Put in production"
                          tone="primary"
                          onClick={() => {
                            if (
                              window.confirm(
                                `Put recipe ${String(version.recipeNumber)} into production? ` +
                                  'It can never be changed afterwards, and the family’s current recipe is archived.',
                              )
                            ) {
                              promote.mutate(version.id);
                            }
                          }}
                        />
                        <Action
                          label="Discard"
                          tone="danger"
                          onClick={() => {
                            if (
                              window.confirm(
                                `Discard draft recipe ${String(version.recipeNumber)}?`,
                              )
                            ) {
                              discard.mutate(version.id);
                            }
                          }}
                        />
                      </>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {dialog !== null && (
        <RecipeVersionDialog
          version={dialog === 'new' ? null : dialog}
          families={families.data}
          materials={materials.data}
          onClose={() => {
            setDialog(null);
          }}
          onSaved={invalidate}
        />
      )}
    </>
  );
}

function Tag({ label }: { label: string }): ReactElement {
  return (
    <span className="rounded-full bg-canvas px-2 py-0.5 text-xs font-medium text-ink-soft">
      {label}
    </span>
  );
}

function Action({
  label,
  onClick,
  tone = 'normal',
}: {
  label: string;
  onClick: () => void;
  tone?: 'normal' | 'primary' | 'danger';
}): ReactElement {
  const tones = {
    normal:
      'border-line text-ink-soft hover:border-brand-200 hover:bg-brand-50 hover:text-brand-700',
    primary: 'border-brand-600 bg-brand-600 text-white hover:bg-brand-700',
    danger:
      'border-line text-ink-muted hover:border-bad/40 hover:bg-bad-soft hover:text-bad',
  };

  return (
    <button
      type="button"
      onClick={onClick}
      className={`min-h-9 rounded-control border px-3 text-sm font-medium whitespace-nowrap transition-colors ${tones[tone]}`}
    >
      {label}
    </button>
  );
}
