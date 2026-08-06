import { apiRequest } from '../../lib/apiClient';

/**
 * The recycler, mirroring Colors.Application.Features.Recycler.
 * Specification section 11.
 */

export interface RecyclerProductionDto {
  id: number;
  shiftLineId: number;
  productionLineName: string;
  shiftName: string;
  productionDate: string;
  scrapWeight: number;
  recycledMaterialWeight: number;
  /**
   * How much of the scrap the grinder lost: (scrap − recycled) ÷ scrap.
   *
   * Not the thermoforming waste, which is scrap ÷ roll weight and answers a different
   * question. Calculated, never stored. Null where no scrap was weighed, because a share
   * of nothing is not a number. Negative where more came out than went in.
   */
  lossPercentage: number | null;
  recordedByName: string;
  recordedAt: string;
  notes: string | null;
}

export interface RecyclerDraftDto {
  shiftLineId: number;
  productionLineName: string;
  shiftName: string;
  productionDate: string;
  /** What the thermo lines of this shift lost by their own arithmetic. Shown, never enforced. */
  thermoCalculatedScrap: number | null;
  /**
   * What those rolls weighed. Scrap over this is the thermoforming waste — a different
   * share from the recycler's own loss, which is scrap lost over scrap ground.
   */
  thermoRollWeight: number | null;
  /** The material the output is added to. */
  recycledMaterialName: string | null;
  alreadyRecorded: boolean;
  recorded: RecyclerProductionDto | null;
}

export const recyclerApi = {
  list: (shiftReportId?: number): Promise<RecyclerProductionDto[]> => {
    const query = shiftReportId === undefined ? '' : `?shiftReportId=${String(shiftReportId)}`;
    return apiRequest<RecyclerProductionDto[]>(`/api/recycler${query}`);
  },

  /** The form, carrying the thermo's own figure for the free check. */
  draft: (shiftLineId: number): Promise<RecyclerDraftDto> =>
    apiRequest<RecyclerDraftDto>(`/api/recycler/draft/${String(shiftLineId)}`),

  save: (body: {
    shiftLineId: number;
    scrapWeight: number;
    recycledMaterialWeight: number;
    notes: string | null;
  }): Promise<RecyclerProductionDto> =>
    apiRequest<RecyclerProductionDto>('/api/recycler', { method: 'POST', body }),
};
