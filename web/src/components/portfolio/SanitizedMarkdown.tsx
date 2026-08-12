import ReactMarkdown, { type Components } from "react-markdown";
import remarkGfm from "remark-gfm";
import rehypeSanitize, { defaultSchema } from "rehype-sanitize";
import rehypeHighlight from "rehype-highlight";
import { MermaidBlock } from "@/components/portfolio/MermaidBlock";

const mermaidStarters = new Set([
  "flowchart",
  "graph",
  "sequencediagram",
  "classdiagram",
  "statediagram",
  "statediagram-v2",
  "erdiagram",
  "journey",
  "gantt",
  "pie",
  "mindmap",
  "timeline",
  "gitgraph",
  "quadrantchart",
  "requirementdiagram",
  "c4context",
  "sankey-beta",
  "xychart-beta",
  "block-beta",
]);

function normalizeMarkdownContent(content: string): string {
  const lines = content.split(/\r?\n/);
  const normalized: string[] = [];
  let i = 0;
  let insideCodeFence = false;

  while (i < lines.length) {
    const line = lines[i];
    const trimmed = line.trim();

    if (trimmed.startsWith("```") || trimmed.startsWith("~~~")) {
      insideCodeFence = !insideCodeFence;
      normalized.push(line);
      i += 1;
      continue;
    }

    if (!insideCodeFence) {
      const firstToken = trimmed.split(/\s+/)[0]?.toLowerCase();
      if (firstToken && mermaidStarters.has(firstToken)) {
        const blockLines = [line];
        let j = i + 1;
        while (j < lines.length && lines[j].trim() !== "" && /^\s+/.test(lines[j])) {
          blockLines.push(lines[j]);
          j += 1;
        }

        normalized.push("```mermaid");
        normalized.push(...blockLines);
        normalized.push("```");
        i = j;
        continue;
      }
    }

    normalized.push(line);
    i += 1;
  }

  return normalized.join("\n");
}

const markdownSchema = {
  ...defaultSchema,
  attributes: {
    ...defaultSchema.attributes,
    code: [
      ...(defaultSchema.attributes?.code ?? []),
      ["className", /^language-[\w-]+$/],
      ["className", "hljs"],
    ],
    pre: [...(defaultSchema.attributes?.pre ?? []), ["className", "hljs"]],
    span: [...(defaultSchema.attributes?.span ?? []), ["className", /^hljs(?:-[\w-]+)?$/]],
  },
};

const markdownComponents: Components = {
  h1: ({ children }) => (
    <h2 className="mt-8 text-2xl font-semibold text-zinc-900 first:mt-0">{children}</h2>
  ),
  h2: ({ children }) => (
    <h3 className="mt-7 text-xl font-semibold text-zinc-900 first:mt-0">{children}</h3>
  ),
  h3: ({ children }) => (
    <h4 className="mt-6 text-lg font-semibold text-zinc-900 first:mt-0">{children}</h4>
  ),
  p: ({ children }) => <p className="mt-4 leading-relaxed text-zinc-700 first:mt-0">{children}</p>,
  a: ({ href, children }) => (
    <a href={href} className="text-indigo-600 underline underline-offset-2 hover:text-indigo-700">
      {children}
    </a>
  ),
  ul: ({ children }) => <ul className="mt-4 list-disc space-y-1.5 pl-6 text-zinc-700">{children}</ul>,
  ol: ({ children }) => <ol className="mt-4 list-decimal space-y-1.5 pl-6 text-zinc-700">{children}</ol>,
  li: ({ children }) => <li className="leading-relaxed">{children}</li>,
  blockquote: ({ children }) => (
    <blockquote className="mt-4 border-l-2 border-zinc-200 pl-4 text-zinc-500 italic">{children}</blockquote>
  ),
  code: ({ children, className }) => {
    const language = className?.replace("language-", "").toLowerCase();
    const isBlock = Boolean(className);
    const codeValue = String(children).replace(/\n$/, "");

    if (isBlock && language === "mermaid") {
      return <MermaidBlock chart={codeValue} />;
    }

    return isBlock ? (
      <code className={className}>{children}</code>
    ) : (
      <code className="rounded bg-zinc-100 px-1.5 py-0.5 font-mono text-sm text-zinc-800">{children}</code>
    );
  },
  pre: ({ children }) => (
    <pre className="mt-4 overflow-x-auto rounded-lg bg-zinc-900 p-4 text-sm text-zinc-100">{children}</pre>
  ),
  hr: () => <hr className="mt-8 border-zinc-200" />,
  table: ({ children }) => (
    <div className="mt-4 overflow-x-auto">
      <table className="w-full border-collapse text-sm">{children}</table>
    </div>
  ),
  th: ({ children }) => (
    <th className="border-b border-zinc-200 px-3 py-2 text-left font-medium text-zinc-900">{children}</th>
  ),
  td: ({ children }) => <td className="border-b border-zinc-100 px-3 py-2 text-zinc-700">{children}</td>,
};

export function SanitizedMarkdown({ content }: { content: string }) {
  const normalizedContent = normalizeMarkdownContent(content);

  return (
    <div>
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        rehypePlugins={[
          [rehypeHighlight, { detect: false, ignoreMissing: true }],
          [rehypeSanitize, markdownSchema],
        ]}
        components={markdownComponents}
      >
        {normalizedContent}
      </ReactMarkdown>
    </div>
  );
}
