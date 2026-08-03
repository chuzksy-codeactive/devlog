import type { Discipline } from "./types";

// Multi-role expansion — static label overrides per discipline, same shape
// as types.ts's experienceTypeLabel map. Discipline is a pure config key
// (see PortfolioDiscipline's doc comment in Enums.cs): nothing here branches
// app behavior, it only swaps copy so a Product Manager's portfolio doesn't
// say "Core technologies" the same way a Software Engineer's does.
//
// Extending this later (a new field, a new discipline) is a pure code
// change — no migration, no backend involvement, matching the
// string-backed-enum reasoning in DevLogDbContext.
export interface DisciplineLabelSet {
  // Field label for Portfolio.CoreTechnologies — a chip list on Hero.tsx and
  // the corresponding form field on dashboard/profile/page.tsx.
  coreTechnologiesLabel: string;
  // Placeholder/help text shown under the core-technologies field in the
  // profile form, to steer what a non-engineer actually types there.
  coreTechnologiesHint: string;
  // Heading for the ProofLinks section on Hero.tsx and the profile form.
  proofLinksLabel: string;
}

export const disciplineLabels: Record<Discipline, DisciplineLabelSet> = {
  SoftwareEngineer: {
    coreTechnologiesLabel: "Core technologies",
    coreTechnologiesHint: "Languages, frameworks, and tools you work with.",
    proofLinksLabel: "Proof links",
  },
  ProductManager: {
    coreTechnologiesLabel: "Core skills & tools",
    coreTechnologiesHint: "Methodologies, analytics tools, and platforms you use.",
    proofLinksLabel: "Case studies & links",
  },
  BusinessAnalyst: {
    coreTechnologiesLabel: "Core skills & tools",
    coreTechnologiesHint: "Analysis methods, BI tools, and platforms you use.",
    proofLinksLabel: "Reports & links",
  },
  DevOps: {
    coreTechnologiesLabel: "Core technologies",
    coreTechnologiesHint: "Cloud platforms, IaC tools, and CI/CD systems you work with.",
    proofLinksLabel: "Proof links",
  },
  ScrumMaster: {
    coreTechnologiesLabel: "Core skills & tools",
    coreTechnologiesHint: "Frameworks, facilitation tools, and platforms you use.",
    proofLinksLabel: "Certifications & links",
  },
  Other: {
    coreTechnologiesLabel: "Core skills & tools",
    coreTechnologiesHint: "Skills, tools, and platforms relevant to your work.",
    proofLinksLabel: "Proof links",
  },
};

// Human-readable label for the Discipline select in dashboard/profile —
// separate from DisciplineLabelSet above since this describes the
// discipline itself, not a field it drives.
export const disciplineDisplayName: Record<Discipline, string> = {
  SoftwareEngineer: "Software Engineer",
  ProductManager: "Product Manager",
  BusinessAnalyst: "Business Analyst",
  DevOps: "DevOps",
  ScrumMaster: "Scrum Master",
  Other: "Other",
};
