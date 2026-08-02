import { api } from "../lib/api";
import type {
  CatalogFilterItemDTO,
  GameDetailDTO,
} from "../types/games";
import type {
  CatalogSource,
  GameType,
} from "../types/home";

export type AdminCatalogKind = "genres" | "platforms" | "companies";

export interface CatalogReferenceInput {
  source: CatalogSource;
  id: number;
}

export interface CompanyReferenceInput extends CatalogReferenceInput {
  role: "Developer" | "Publisher";
}

export interface AdminGameInput {
  gameType: GameType;
  title: string;
  description?: string;
  releaseDate?: string;
  coverImageUrl?: string;
  companyReferences: CompanyReferenceInput[];
  genreReferences: CatalogReferenceInput[];
  platformReferences: CatalogReferenceInput[];
}

export async function getDeletedAdminGames(
  signal?: AbortSignal,
): Promise<GameDetailDTO[]> {
  const response = await api.get<GameDetailDTO[]>("/games/deleted", { signal });
  return response.data;
}

export async function uploadAdminGameCover(file: File): Promise<string> {
  const formData = new FormData();
  formData.append("image", file);

  const response = await api.post<{ url: string }>(
    "/games/cover-image",
    formData,
  );

  return response.data.url;
}

export async function createAdminGame(
  request: AdminGameInput,
): Promise<{ source: CatalogSource; id: number }> {
  const response = await api.post<{ source: CatalogSource; id: number }>(
    "/games",
    request,
  );
  return response.data;
}

export async function updateAdminGame(
  source: CatalogSource,
  id: number,
  request: Partial<AdminGameInput>,
): Promise<void> {
  await api.patch(`/games/${source}/${id}`, request);
}

export async function deleteAdminGame(
  source: CatalogSource,
  id: number,
): Promise<void> {
  await api.delete(`/games/${source}/${id}`);
}

export async function restoreAdminGame(
  source: CatalogSource,
  id: number,
): Promise<void> {
  await api.patch(`/games/${source}/${id}/restore`);
}

export async function addAdminGameRelation(
  source: CatalogSource,
  id: number,
  request: {
    relatedSource: CatalogSource;
    relatedId: number;
    relationType: GameType;
  },
): Promise<number> {
  const response = await api.post<{ id: number }>(
    `/games/${source}/${id}/relations`,
    request,
  );
  return response.data.id;
}

export async function removeAdminGameRelation(
  source: CatalogSource,
  id: number,
  relatedSource: CatalogSource,
  relatedId: number,
  relationType: GameType,
): Promise<void> {
  await api.delete(
    `/games/${source}/${id}/relations/${relatedSource}/${relatedId}`,
    { params: { relationType } },
  );
}

export async function getDeletedCatalogItems(
  kind: AdminCatalogKind,
  signal?: AbortSignal,
): Promise<CatalogFilterItemDTO[]> {
  const response = await api.get<CatalogFilterItemDTO[]>(
    `/${kind}/deleted`,
    { signal },
  );
  return response.data;
}

export async function createCatalogItem(
  kind: AdminCatalogKind,
  name: string,
): Promise<{ source: CatalogSource; id: number }> {
  const response = await api.post<{ source: CatalogSource; id: number }>(
    `/${kind}`,
    { name },
  );
  return response.data;
}

export async function updateCatalogItem(
  kind: AdminCatalogKind,
  source: CatalogSource,
  id: number,
  name: string,
): Promise<void> {
  await api.patch(`/${kind}/${source}/${id}`, { name });
}

export async function deleteCatalogItem(
  kind: AdminCatalogKind,
  source: CatalogSource,
  id: number,
): Promise<void> {
  await api.delete(`/${kind}/${source}/${id}`);
}

export async function restoreCatalogItem(
  kind: AdminCatalogKind,
  source: CatalogSource,
  id: number,
): Promise<void> {
  await api.patch(`/${kind}/${source}/${id}/restore`);
}
