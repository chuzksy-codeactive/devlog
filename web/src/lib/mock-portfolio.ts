import { PortfolioData } from "./types";

// Stand-in for the real API call (GET /api/portfolio/:slug, not built yet).
// Swap getPortfolioBySlug's body for a fetch() once that endpoint exists —
// callers below already treat this as async so that swap won't ripple
// through page components.
const MOCK_PORTFOLIOS: Record<string, PortfolioData> = {
  chika: {
    slug: "chika",
    displayName: "Chika",
    positioningLine:
      "I build multi-tenant SaaS backends and make the messy parts — auth, isolation, data pipelines — boring and reliable.",
    currentRoleTitle: "Backend Engineer",
    coreTechnologies: [".NET / C#", "PostgreSQL", "EF Core", "Next.js", "Docker"],
    githubUrl: "https://github.com/chika",
    cvUrl: null,
    photoUrl: null,
    experiences: [
      {
        id: "exp-1",
        experienceType: "Employer",
        companyLabel: "A Series B fintech, payments platform team",
        title: "Backend Engineer",
        startDate: "2023-02-01",
        endDate: null,
        technologies: [".NET", "PostgreSQL", "Docker", "Azure"],
        keyAchievement:
          "Redesigned the tenant-isolation layer for a multi-tenant billing service, cutting cross-tenant data-leak risk to zero in a security audit.",
      },
      {
        id: "exp-2",
        experienceType: "PersonalProject",
        companyLabel: "DevLog",
        title: "Creator",
        startDate: "2026-07-01",
        endDate: null,
        technologies: [".NET", "Next.js", "Anthropic API", "pgvector"],
        keyAchievement:
          "Designed and built a multi-tenant developer-portfolio platform with an AI-queryable, anonymized sprint journal — this project.",
      },
      {
        id: "exp-3",
        experienceType: "Education",
        companyLabel: "University",
        title: "B.Sc. Computer Science",
        startDate: "2018-09-01",
        endDate: "2022-06-01",
        technologies: [],
        keyAchievement: "Graduated with a focus on distributed systems.",
      },
    ],
  },
};

// A second fixture exercising the "no job yet" state on purpose — Product
// Brief: "treat this as a real state to design for, not an edge case to
// patch later." Useful while building/reviewing Home's fallback rendering.
MOCK_PORTFOLIOS["new-grad-demo"] = {
  slug: "new-grad-demo",
  displayName: "Sam",
  positioningLine:
    "Bootcamp grad who ships. I learn a stack by building something real in it, not by watching tutorials.",
  currentRoleTitle: null,
  coreTechnologies: ["JavaScript", "React", "Node.js"],
  githubUrl: "https://github.com/sam-example",
  cvUrl: null,
  photoUrl: null,
  experiences: [
    {
      id: "exp-1",
      experienceType: "PersonalProject",
      companyLabel: "Habit tracker app",
      title: "Solo builder",
      startDate: "2026-03-01",
      endDate: null,
      technologies: ["React", "Node.js", "PostgreSQL"],
      keyAchievement: "Shipped a full-stack app end to end, including deploy and a small paying user base.",
    },
    {
      id: "exp-2",
      experienceType: "Education",
      companyLabel: "Coding bootcamp",
      title: "Full-Stack Web Development",
      startDate: "2025-09-01",
      endDate: "2026-02-01",
      technologies: ["JavaScript", "React", "Express"],
      keyAchievement: "Top of cohort capstone project.",
    },
  ],
};

export async function getPortfolioBySlug(slug: string): Promise<PortfolioData | null> {
  return MOCK_PORTFOLIOS[slug] ?? null;
}
