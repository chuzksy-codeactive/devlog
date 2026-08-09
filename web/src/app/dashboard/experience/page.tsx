"use client";

import { useEffect, useMemo, useState } from "react";
import { useApi } from "@/lib/use-api";
import { formatDateRange } from "@/lib/portfolio-utils";
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

const typeTone: Record<ExperienceType, string> = {
  Employer: "bg-indigo-500/12 text-indigo-100 border-indigo-300/20",
  PersonalProject: "bg-cyan-500/12 text-cyan-100 border-cyan-300/20",
  OpenSource: "bg-emerald-500/12 text-emerald-100 border-emerald-300/20",
  Freelance: "bg-amber-500/12 text-amber-100 border-amber-300/20",
  Education: "bg-fuchsia-500/12 text-fuchsia-100 border-fuchsia-300/20",
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

function summarizeExperience(experiences: MyExperience[]) {
  const active = experiences.filter((exp) => !exp.endDate).length;
  const technologies = new Set(experiences.flatMap((exp) => exp.technologies.map((tech) => tech.trim()).filter(Boolean)));

  return [
    {
      label: "Entries",
      value: String(experiences.length),
      helper: experiences.length > 0 ? "Career moments documented" : "Start documenting your experience",
    },
    {
      label: "Active roles",
      value: String(active),
      helper: active > 0 ? "Currently ongoing" : "No current role marked as present",
    },
    {
      label: "Technologies",
      value: String(technologies.size),
      helper: technologies.size > 0 ? "Unique tools surfaced to recruiters" : "Add skills across your entries",
    },
  ];
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

  const summary = useMemo(() => summarizeExperience(experiences), [experiences]);

  return (
    <div className="flex flex-col gap-8">
      <section className="overflow-hidden rounded-[2rem] border border-white/10 bg-white/6 p-8 shadow-2xl shadow-black/20 backdrop-blur-xl">
        <div className="grid gap-8 lg:grid-cols-[1.05fr_0.95fr]">
          <div>
            <div className="inline-flex items-center gap-2 rounded-full border border-indigo-400/20 bg-indigo-500/10 px-4 py-2 text-sm text-indigo-100">
              <span className="h-2 w-2 rounded-full bg-indigo-300" />
              Experience timeline
            </div>
            <h1 className="mt-6 text-4xl font-semibold tracking-tight text-white">Turn roles and projects into recruiter-ready proof.</h1>
            <p className="mt-4 max-w-2xl text-base leading-7 text-white/68">
              Capture work that matters, highlight the outcomes, and make each entry easy to scan for titles, context, and technologies.
            </p>

            <div className="mt-8 flex flex-wrap gap-3">
              {editingId !== "new" && (
                <button
                  onClick={() => setEditingId("new")}
                  className="rounded-full bg-indigo-500 px-5 py-3 text-sm font-medium text-white shadow-lg shadow-indigo-500/25 transition hover:bg-indigo-400"
                >
                  Add experience
                </button>
              )}
              <span className="rounded-full border border-white/10 bg-white/6 px-4 py-3 text-sm text-white/65">
                Focus on impact, scope, and the stack behind the work.
              </span>
            </div>
          </div>

          <div className="grid gap-3 rounded-[1.5rem] border border-white/10 bg-[#101525] p-5">
            <p className="text-xs font-medium uppercase tracking-[0.22em] text-white/42">Timeline health</p>
            {summary.map((item) => (
              <div key={item.label} className="rounded-2xl border border-white/10 bg-white/5 p-4">
                <p className="text-sm text-white/50">{item.label}</p>
                <p className="mt-1 text-2xl font-semibold tracking-tight text-white">{item.value}</p>
                <p className="mt-2 text-sm leading-6 text-white/62">{item.helper}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {status === "error" && (
        <p className="rounded-2xl border border-rose-400/20 bg-rose-500/10 px-4 py-3 text-sm text-rose-200">
          Couldn&apos;t load your experience — check that the API is running.
        </p>
      )}

      {editingId === "new" && (
        <section className="rounded-[1.75rem] border border-white/10 bg-white/6 p-6 backdrop-blur">
          <div className="mb-5">
            <p className="text-xs font-medium uppercase tracking-[0.22em] text-white/42">New entry</p>
            <h2 className="mt-2 text-2xl font-semibold text-white">Add a new experience</h2>
          </div>
          <ExperienceForm
            initial={EMPTY_FORM}
            onCancel={() => setEditingId(null)}
            onSubmit={async (form) => {
              await request("/api/experiences", { method: "POST", body: JSON.stringify(toPayload(form)) });
              setEditingId(null);
              load();
            }}
          />
        </section>
      )}

      <section className="flex flex-col gap-5">
        {experiences.map((exp) =>
          editingId === exp.id ? (
            <div key={exp.id} className="rounded-[1.75rem] border border-white/10 bg-white/6 p-6 backdrop-blur">
              <div className="mb-5">
                <p className="text-xs font-medium uppercase tracking-[0.22em] text-white/42">Editing</p>
                <h2 className="mt-2 text-2xl font-semibold text-white">{exp.title || "Experience entry"}</h2>
              </div>
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
            <article key={exp.id} className="rounded-[1.75rem] border border-white/10 bg-white/6 p-6 transition hover:border-white/15 hover:bg-white/8">
              <div className="flex flex-col gap-5 lg:flex-row lg:items-start lg:justify-between">
                <div className="max-w-3xl">
                  <div className="flex flex-wrap items-center gap-3">
                    <span className={`rounded-full border px-3 py-1 text-xs font-medium uppercase tracking-[0.18em] ${typeTone[exp.experienceType]}`}>
                      {experienceTypeLabel[exp.experienceType]}
                    </span>
                    <span className="text-sm text-white/45">{formatDateRange(exp.startDate, exp.endDate)}</span>
                  </div>
                  <h2 className="mt-4 text-2xl font-semibold tracking-tight text-white">
                    {exp.title} <span className="text-white/50">— {exp.companyLabel}</span>
                  </h2>
                  <p className="mt-4 max-w-3xl text-base leading-8 text-white/72">{exp.keyAchievement}</p>
                </div>

                <div className="flex gap-3 text-sm font-medium">
                  <button
                    onClick={() => setEditingId(exp.id)}
                    className="rounded-full border border-white/12 px-4 py-2 text-white/75 transition hover:bg-white/10 hover:text-white"
                  >
                    Edit
                  </button>
                  <button
                    onClick={() => handleDelete(exp.id)}
                    className="rounded-full border border-rose-300/15 px-4 py-2 text-rose-200 transition hover:bg-rose-500/12"
                  >
                    Delete
                  </button>
                </div>
              </div>

              {exp.technologies.length > 0 && (
                <div className="mt-5 flex flex-wrap gap-2">
                  {exp.technologies.map((t) => (
                    <span key={t} className="rounded-full border border-white/10 bg-white/5 px-3 py-1 text-xs font-medium text-white/72">
                      {t}
                    </span>
                  ))}
                </div>
              )}
            </article>
          )
        )}

        {status === "loading" && <p className="text-sm text-white/60">Loading your experience…</p>}

        {status === "ready" && experiences.length === 0 && editingId !== "new" && (
          <div className="rounded-[1.75rem] border border-dashed border-white/12 bg-white/4 p-8 text-center">
            <h2 className="text-xl font-semibold text-white">No experience entries yet</h2>
            <p className="mt-2 text-sm leading-7 text-white/58">
              Add your first role, project, or learning experience to start building your public proof of work.
            </p>
            <button
              onClick={() => setEditingId("new")}
              className="mt-5 rounded-full bg-indigo-500 px-5 py-3 text-sm font-medium text-white shadow-lg shadow-indigo-500/25 transition hover:bg-indigo-400"
            >
              Add first experience
            </button>
          </div>
        )}
      </section>
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
  "mt-1 w-full rounded-xl border border-white/12 bg-white px-3 py-2 text-sm text-zinc-950 outline-none placeholder:text-zinc-500 focus:border-indigo-300 focus:bg-white";

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
    <form onSubmit={handleSubmit} className="flex flex-col gap-4">
      <div className="grid gap-4 md:grid-cols-2">
        <label className="text-sm font-medium text-white/78">
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
        <label className="text-sm font-medium text-white/78">
          Role / title
          <input
            type="text"
            value={form.title}
            onChange={(e) => update("title", e.target.value)}
            className={inputClass}
          />
        </label>
      </div>

      <label className="text-sm font-medium text-white/78">
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

      <div className="grid gap-4 md:grid-cols-2">
        <label className="text-sm font-medium text-white/78">
          Start date
          <input
            type="date"
            required
            value={form.startDate}
            onChange={(e) => update("startDate", e.target.value)}
            className={inputClass}
          />
        </label>
        <label className="text-sm font-medium text-white/78">
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

      <label className="text-sm font-medium text-white/78">
        Technologies
        <input
          type="text"
          value={form.technologies}
          onChange={(e) => update("technologies", e.target.value)}
          className={inputClass}
          placeholder="C#, .NET, Azure, SQL"
        />
        <span className="mt-1 block text-xs font-normal text-white/45">Comma-separated so they render cleanly on your public portfolio.</span>
      </label>

      <label className="text-sm font-medium text-white/78">
        Key achievement
        <textarea
          rows={4}
          value={form.keyAchievement}
          onChange={(e) => update("keyAchievement", e.target.value)}
          className={inputClass}
          placeholder="Describe the outcome, why it mattered, and the stack behind it."
        />
      </label>

      {error && <p className="text-sm text-rose-300">{error}</p>}

      <div className="flex justify-end gap-3">
        <button
          type="button"
          onClick={onCancel}
          className="rounded-full border border-white/12 px-4 py-2 text-sm font-medium text-white/70 transition hover:bg-white/10 hover:text-white"
        >
          Cancel
        </button>
        <button
          type="submit"
          disabled={saving}
          className="rounded-full bg-indigo-500 px-4 py-2 text-sm font-medium text-white shadow-lg shadow-indigo-500/25 transition hover:bg-indigo-400 disabled:opacity-50"
        >
          {saving ? "Saving…" : "Save experience"}
        </button>
      </div>
    </form>
  );
}
