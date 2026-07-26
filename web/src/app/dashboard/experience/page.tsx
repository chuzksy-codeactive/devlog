"use client";

import { useEffect, useState } from "react";
import { useApi } from "@/lib/use-api";
import { ExperienceType, MyExperience, experienceTypeLabel } from "@/lib/types";

type FormState = {
  experienceType: ExperienceType;
  companyLabel: string;
  title: string;
  startDate: string;
  endDate: string;
  technologies: string;
  keyAchievement: string;
};

const EMPTY_FORM: FormState = {
  experienceType: "Employer",
  companyLabel: "",
  title: "",
  startDate: "",
  endDate: "",
  technologies: "",
  keyAchievement: "",
};

function toFormState(e: MyExperience): FormState {
  return {
    experienceType: e.experienceType,
    companyLabel: e.companyLabel,
    title: e.title,
    startDate: e.startDate,
    endDate: e.endDate ?? "",
    technologies: e.technologies.join(", "),
    keyAchievement: e.keyAchievement,
  };
}

export default function ExperiencePage() {
  const { request } = useApi();
  const [experiences, setExperiences] = useState<MyExperience[]>([]);
  const [status, setStatus] = useState<"loading" | "ready" | "error">("loading");
  const [editingId, setEditingId] = useState<string | "new" | null>(null);

  function load() {
    request<MyExperience[]>("/api/experiences/me")
      .then((data) => {
        setExperiences(data);
        setStatus("ready");
      })
      .catch(() => setStatus("error"));
  }

  useEffect(load, []); // eslint-disable-line react-hooks/exhaustive-deps

  async function handleDelete(id: string) {
    if (!confirm("Delete this experience? This can't be undone.")) return;
    await request(`/api/experiences/${id}`, { method: "DELETE" });
    load();
  }

  return (
    <div className="max-w-2xl">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold tracking-tight text-zinc-900">Experience</h1>
        {editingId !== "new" && (
          <button
            onClick={() => setEditingId("new")}
            className="rounded-lg bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-indigo-700"
          >
            Add experience
          </button>
        )}
      </div>

      {status === "error" && (
        <p className="mt-4 text-sm text-red-600">Couldn&apos;t load your experience — check that the API is running.</p>
      )}

      {editingId === "new" && (
        <div className="mt-4 rounded-xl border border-zinc-200 bg-white p-5 shadow-sm">
          <ExperienceForm
            initial={EMPTY_FORM}
            onCancel={() => setEditingId(null)}
            onSubmit={async (form) => {
              await request("/api/experiences", { method: "POST", body: JSON.stringify(toPayload(form)) });
              setEditingId(null);
              load();
            }}
          />
        </div>
      )}

      <div className="mt-6 flex flex-col gap-4">
        {experiences.map((exp) =>
          editingId === exp.id ? (
            <div key={exp.id} className="rounded-xl border border-zinc-200 bg-white p-5 shadow-sm">
              <ExperienceForm
                initial={toFormState(exp)}
                onCancel={() => setEditingId(null)}
                onSubmit={async (form) => {
                  await request(`/api/experiences/${exp.id}`, {
                    method: "PUT",
                    body: JSON.stringify(toPayload(form)),
                  });
                  setEditingId(null);
                  load();
                }}
              />
            </div>
          ) : (
            <div key={exp.id} className="rounded-xl border border-zinc-200 bg-white p-5 shadow-sm">
              <div className="flex items-start justify-between">
                <div>
                  <p className="text-xs font-medium uppercase tracking-wide text-indigo-600">
                    {experienceTypeLabel[exp.experienceType]}
                  </p>
                  <p className="mt-0.5 font-medium text-zinc-900">
                    {exp.title} — {exp.companyLabel}
                  </p>
                  <p className="text-xs text-zinc-400">
                    {exp.startDate} — {exp.endDate ?? "Present"}
                  </p>
                </div>
                <div className="flex gap-3 text-xs font-medium">
                  <button onClick={() => setEditingId(exp.id)} className="text-indigo-600 underline">
                    Edit
                  </button>
                  <button onClick={() => handleDelete(exp.id)} className="text-red-600 underline">
                    Delete
                  </button>
                </div>
              </div>
              <p className="mt-2 text-sm text-zinc-600">{exp.keyAchievement}</p>
              {exp.technologies.length > 0 && (
                <div className="mt-2 flex flex-wrap gap-1.5">
                  {exp.technologies.map((t) => (
                    <span key={t} className="rounded-full border border-zinc-200 bg-zinc-50 px-2 py-0.5 text-xs text-zinc-600">
                      {t}
                    </span>
                  ))}
                </div>
              )}
            </div>
          )
        )}
        {status === "ready" && experiences.length === 0 && editingId !== "new" && (
          <p className="text-sm text-zinc-500">No experience entries yet — add your first one above.</p>
        )}
      </div>
    </div>
  );
}

function toPayload(form: FormState) {
  return {
    experienceType: form.experienceType,
    companyLabel: form.companyLabel.trim(),
    title: form.title.trim(),
    startDate: form.startDate,
    endDate: form.endDate || null,
    technologies: form.technologies
      .split(",")
      .map((t) => t.trim())
      .filter(Boolean),
    keyAchievement: form.keyAchievement.trim(),
  };
}

const inputClass =
  "mt-1 w-full rounded-lg border border-zinc-200 bg-white px-3 py-2 text-sm text-zinc-900 outline-none focus:border-indigo-400";

function ExperienceForm({
  initial,
  onSubmit,
  onCancel,
}: {
  initial: FormState;
  onSubmit: (form: FormState) => Promise<void>;
  onCancel: () => void;
}) {
  const [form, setForm] = useState<FormState>(initial);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function update<K extends keyof FormState>(key: K, value: FormState[K]) {
    setForm((prev) => ({ ...prev, [key]: value }));
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!form.companyLabel.trim() || !form.startDate) {
      setError("Company/project label and start date are required.");
      return;
    }
    setSaving(true);
    setError(null);
    try {
      await onSubmit(form);
    } catch {
      setError("Couldn't save — check that the API is running.");
      setSaving(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-3">
      <div className="grid grid-cols-2 gap-3">
        <label className="text-xs font-medium text-zinc-500">
          Type
          <select
            value={form.experienceType}
            onChange={(e) => update("experienceType", e.target.value as ExperienceType)}
            className={inputClass}
          >
            {Object.entries(experienceTypeLabel).map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
        </label>
        <label className="text-xs font-medium text-zinc-500">
          Role / title
          <input
            type="text"
            value={form.title}
            onChange={(e) => update("title", e.target.value)}
            className={inputClass}
          />
        </label>
      </div>

      <label className="text-xs font-medium text-zinc-500">
        Company / project label
        <input
          type="text"
          required
          placeholder="e.g. A Series B fintech, payments team"
          value={form.companyLabel}
          onChange={(e) => update("companyLabel", e.target.value)}
          className={inputClass}
        />
      </label>

      <div className="grid grid-cols-2 gap-3">
        <label className="text-xs font-medium text-zinc-500">
          Start date
          <input
            type="date"
            required
            value={form.startDate}
            onChange={(e) => update("startDate", e.target.value)}
            className={inputClass}
          />
        </label>
        <label className="text-xs font-medium text-zinc-500">
          End date
          <input
            type="date"
            value={form.endDate}
            onChange={(e) => update("endDate", e.target.value)}
            className={inputClass}
            placeholder="Leave blank for Present"
          />
        </label>
      </div>

      <label className="text-xs font-medium text-zinc-500">
        Technologies (comma-separated)
        <input
          type="text"
          value={form.technologies}
          onChange={(e) => update("technologies", e.target.value)}
          className={inputClass}
        />
      </label>

      <label className="text-xs font-medium text-zinc-500">
        Key achievement
        <textarea
          rows={3}
          value={form.keyAchievement}
          onChange={(e) => update("keyAchievement", e.target.value)}
          className={inputClass}
        />
      </label>

      {error && <p className="text-xs text-red-600">{error}</p>}

      <div className="flex justify-end gap-2">
        <button type="button" onClick={onCancel} className="text-xs font-medium text-zinc-500 underline">
          Cancel
        </button>
        <button
          type="submit"
          disabled={saving}
          className="rounded-lg bg-indigo-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
        >
          {saving ? "Saving…" : "Save"}
        </button>
      </div>
    </form>
  );
}
