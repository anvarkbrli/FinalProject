import { api } from "../lib/api";
import type {
  CreateUserReportRequest,
  UserReportDTO,
} from "../types/userReports";

export async function createUserReport(
  request: CreateUserReportRequest,
): Promise<UserReportDTO> {
  const formData = new FormData();
  formData.append("category", request.category);
  formData.append(
    "reportedContentType",
    request.reportedContentType,
  );

  if (request.reportedContentId !== null) {
    formData.append(
      "reportedContentId",
      request.reportedContentId.toString(),
    );
  }

  if (request.description.trim()) {
    formData.append(
      "description",
      request.description.trim(),
    );
  }

  request.evidenceImages.forEach((evidenceImage) => {
    formData.append(
      "evidenceImages",
      evidenceImage,
    );
  });

  const response = await api.post<UserReportDTO>(
    `/user-reports/users/${request.reportedUserId}`,
    formData,
    {
      headers: {
        "Content-Type": "multipart/form-data",
      },
    },
  );

  return response.data;
}
