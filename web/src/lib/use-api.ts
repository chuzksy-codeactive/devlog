"use client";

import { useAuth } from "@clerk/nextjs";

// Milestone 11 — every dashboard page needs to call the authenticated .NET
// API with a real Clerk session token attached. Every prior creator-side
// call in this codebase was made by hand with curl and a manually-pasted
// bearer token; this is the first time the frontend mints and attaches one
// itself. getToken() (no template argument) returns Clerk's default session
// JWT — the same one the backend's Clerk:Authority/AuthorizedParty config
// was already built to validate (see DevLog.Api's JwtBearer setup), so no
// backend change was needed to support this.
const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5000";

export class ApiError extends Error {
  status: number;
  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }
}

export function useApi() {
  const { getToken } = useAuth();

  async function request<T = unknown>(path: string, options: RequestInit = {}): Promise<T> {
    const token = await getToken();

    const response = await fetch(`${API_BASE_URL}${path}`, {
      ...options,
      headers: {
        "Content-Type": "application/json",
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
        ...options.headers,
      },
    });

    if (!response.ok) {
      const text = await response.text().catch(() => "");
      throw new ApiError(response.status, text || `Request failed with status ${response.status}`);
    }

    if (response.status === 204) {
      return undefined as T;
    }

    return (await response.json()) as T;
  }

  return { request };
}
