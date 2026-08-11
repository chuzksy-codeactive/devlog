"use client";

import { useState } from "react";

function initials(name: string): string {
  return name
    .split(" ")
    .map((part) => part[0])
    .join("")
    .slice(0, 2)
    .toUpperCase();
}

function isGooglePhotosShareUrl(value: string): boolean {
  try {
    const url = new URL(value);
    const host = url.hostname.toLowerCase();
    return host === "photos.app.goo.gl" || host === "photos.google.com";
  } catch {
    return false;
  }
}

export function ProfileAvatar({
  displayName,
  photoUrl,
  githubVerified,
}: {
  displayName: string;
  photoUrl: string | null;
  githubVerified: boolean;
}) {
  const normalizedPhotoUrl = photoUrl?.trim() ?? "";
  const canAttemptImage = normalizedPhotoUrl.length > 0 && !isGooglePhotosShareUrl(normalizedPhotoUrl);
  const [imageFailed, setImageFailed] = useState(false);
  const showImage = canAttemptImage && !imageFailed;

  return (
    <div className="relative h-28 w-28 shrink-0">
      <div className="flex h-28 w-28 items-center justify-center overflow-hidden rounded-full border-2 border-[#d8d3cc] bg-indigo-50 text-3xl font-semibold text-indigo-600 shadow-lg">
        {showImage ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img
            src={normalizedPhotoUrl}
            alt={displayName}
            className="h-full w-full object-cover object-top"
            onError={() => setImageFailed(true)}
          />
        ) : (
          initials(displayName)
        )}
      </div>
      {githubVerified && (
        <span className="absolute -bottom-1 -right-1 rounded-full bg-emerald-500 px-2 py-0.5 text-[10px] font-semibold text-white shadow">
          ✓ Verified
        </span>
      )}
    </div>
  );
}
