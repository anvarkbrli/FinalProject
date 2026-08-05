import { api } from "../lib/api";
import type {
  ContactMessageCreateRequest,
  ContactMessageReceipt,
} from "../types/contact";

export async function submitContactMessage(
  request: ContactMessageCreateRequest,
): Promise<ContactMessageReceipt> {
  const response = await api.post<ContactMessageReceipt>(
    "/contact-messages",
    request,
  );
  return response.data;
}
