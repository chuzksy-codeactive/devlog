import type { Discipline } from "./types";

export interface DisciplineLabelSet {
  coreTechnologiesLabel: string;
  coreTechnologiesHint: string;
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

export const disciplineDisplayName: Record<Discipline, string> = {
  SoftwareEngineer: "Software Engineer",
  ProductManager: "Product Manager",
  BusinessAnalyst: "Business Analyst",
  DevOps: "DevOps",
  ScrumMaster: "Scrum Master",
  Other: "Other",
};
