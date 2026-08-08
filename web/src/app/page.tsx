"use client";

import Link from "next/link";
import { SignInButton, UserButton, useAuth } from "@clerk/nextjs";

const useCases = [
  "Developers",
  "Data analysts",
  "Designers",
  "Product managers",
  "DevOps engineers",
  "Security specialists",
];

const steps = [
  {
    title: "Capture real work",
    description: "Write monthly logs and sprint deep-dives around the work you actually delivered.",
  },
  {
    title: "Sanitize sensitive details",
    description: "Remove private employer data before anything is published or shared.",
  },
  {
    title: "Let recruiters explore",
    description: "Turn the portfolio into a searchable case study with AI Q&A on top.",
  },
];

const benefits = [
  "Show outcomes, not just job titles",
  "Surface relevant skills instantly",
  "Give recruiters a clearer story in less time",
  "Keep private notes separate from public proof",
];

export default function RootPage() {
  const { isLoaded, userId } = useAuth();
  const isSignedIn = isLoaded && !!userId;

  return (
    <main className="min-h-screen overflow-hidden bg-[radial-gradient(circle_at_top,_rgba(99,102,241,0.18),_transparent_34%),linear-gradient(180deg,#08090f_0%,#0f1220_45%,#0b0d14_100%)] text-white">
      <div className="mx-auto flex min-h-screen max-w-7xl flex-col px-6 pb-20 pt-6 lg:px-8">
        <header className="flex items-center justify-between rounded-full border border-white/10 bg-white/5 px-4 py-3 backdrop-blur">
          <div>
            <p className="text-sm font-semibold tracking-[0.24em] text-indigo-200 uppercase">Workfolio</p>
            <p className="text-xs text-white/55">Showcase real work for recruiters</p>
          </div>
          <div className="flex items-center gap-3 text-sm font-medium">
            <a href="#how-it-works" className="rounded-full px-3 py-2 text-white/70 transition hover:bg-white/8 hover:text-white">
              How it works
            </a>
            {!isSignedIn && (
              <SignInButton mode="modal">
                <button className="rounded-full border border-white/15 px-4 py-2 text-white transition hover:bg-white/10">
                  Sign in
                </button>
              </SignInButton>
            )}
            {isSignedIn && (
              <Link
                href="/dashboard"
                className="rounded-full bg-indigo-500 px-4 py-2 text-white shadow-lg shadow-indigo-500/25 transition hover:bg-indigo-400"
              >
                Go to dashboard
              </Link>
            )}
            {isSignedIn && <UserButton />}
          </div>
        </header>

        <section className="grid flex-1 items-center gap-14 py-14 lg:grid-cols-[1.05fr_0.95fr] lg:py-20">
          <div className="max-w-2xl">
            <div className="mb-6 inline-flex items-center gap-2 rounded-full border border-emerald-400/20 bg-emerald-400/10 px-4 py-2 text-sm text-emerald-100">
              <span className="h-2 w-2 rounded-full bg-emerald-300" />
              AI-powered portfolio storytelling for recruiters
            </div>

            <h1 className="text-5xl font-semibold tracking-tight text-white sm:text-6xl">
              Show the work.
              <span className="block text-indigo-200">Let recruiters understand the impact.</span>
            </h1>

            <p className="mt-6 max-w-xl text-lg leading-8 text-white/72">
              Capture your real projects, sanitize sensitive details, and publish a clean
              portfolio that recruiters can explore with AI-assisted questions.
            </p>

            <div className="mt-8 flex flex-wrap items-center gap-3">
              {!isSignedIn && (
                <SignInButton mode="modal">
                  <button className="rounded-full bg-indigo-500 px-6 py-3 font-medium text-white shadow-lg shadow-indigo-500/30 transition hover:bg-indigo-400">
                    Create your portfolio
                  </button>
                </SignInButton>
              )}
              {isSignedIn && (
                <Link
                  href="/dashboard"
                  className="rounded-full bg-indigo-500 px-6 py-3 font-medium text-white shadow-lg shadow-indigo-500/30 transition hover:bg-indigo-400"
                >
                  Open dashboard
                </Link>
              )}
              <a
                href="#sample"
                className="rounded-full border border-white/15 px-6 py-3 font-medium text-white transition hover:bg-white/10"
              >
                View sample layout
              </a>
            </div>

            <div className="mt-10 grid gap-3 sm:grid-cols-2">
              {benefits.map((benefit) => (
                <div key={benefit} className="rounded-2xl border border-white/10 bg-white/5 px-4 py-3 text-sm text-white/75 backdrop-blur">
                  {benefit}
                </div>
              ))}
            </div>

            <p className="mt-6 text-sm text-white/45">
              Built for {useCases.join(", ")}.
            </p>
          </div>

          <div id="sample" className="relative">
            <div className="absolute -left-6 -top-6 h-24 w-24 rounded-full bg-indigo-500/20 blur-3xl" />
            <div className="absolute -right-4 bottom-6 h-28 w-28 rounded-full bg-cyan-400/15 blur-3xl" />
            <div className="relative overflow-hidden rounded-[2rem] border border-white/10 bg-white/8 p-5 shadow-2xl shadow-black/30 backdrop-blur-xl">
              <div className="rounded-[1.5rem] border border-white/10 bg-[#101525] p-6">
                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-sm text-white/50">Sample portfolio</p>
                    <h2 className="mt-1 text-2xl font-semibold text-white">Jordan Lee</h2>
                  </div>
                  <span className="rounded-full bg-emerald-400/15 px-3 py-1 text-xs font-medium text-emerald-200">
                    Verified proof links
                  </span>
                </div>

                <div className="mt-6 grid gap-4">
                  <div className="rounded-2xl border border-white/10 bg-white/5 p-4">
                    <p className="text-xs font-medium uppercase tracking-[0.2em] text-white/45">
                      Recruiter question
                    </p>
                    <p className="mt-2 text-sm text-white/80">
                      “What cloud and SQL experience does this person have?”
                    </p>
                  </div>
                  <div className="rounded-2xl border border-indigo-400/20 bg-indigo-500/10 p-4">
                    <p className="text-xs font-medium uppercase tracking-[0.2em] text-indigo-200">
                      AI response
                    </p>
                    <p className="mt-2 text-sm leading-6 text-white/85">
                      They led Azure-based deployments, wrote SQL reporting queries, and
                      documented delivery outcomes across three product sprints.
                    </p>
                  </div>
                  <div className="grid gap-3 sm:grid-cols-3">
                    {["Azure", "SQL", "Delivery impact"].map((tag) => (
                      <div key={tag} className="rounded-2xl border border-white/10 bg-white/5 px-4 py-3 text-center text-sm text-white/75">
                        {tag}
                      </div>
                    ))}
                  </div>
                </div>
              </div>
            </div>
          </div>
        </section>

        <section id="how-it-works" className="grid gap-5 border-t border-white/10 py-12 lg:grid-cols-3">
          {steps.map((step, index) => (
            <article key={step.title} className="rounded-3xl border border-white/10 bg-white/5 p-6 backdrop-blur">
              <p className="text-sm font-medium text-indigo-200">0{index + 1}</p>
              <h3 className="mt-3 text-xl font-semibold text-white">{step.title}</h3>
              <p className="mt-2 text-sm leading-7 text-white/65">{step.description}</p>
            </article>
          ))}
        </section>
      </div>
    </main>
  );
}
