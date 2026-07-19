import ReactMarkdown, { type Components } from "react-markdown";
import remarkGfm from "remark-gfm";
import rehypeSanitize, { defaultSchema } from "rehype-sanitize";

// The actual fix for architecture doc Section 9's "stored XSS in rendered
// entries" — Sprint Journal content (PublishedEntry.SanitizedContent,
// including code snippets) is creator-controlled and renders on public
// pages recruiters view, so it's treated as untrusted at render time no
// matter what the write-time sanitization pipeline already did to it.
//
// react-markdown parses markdown into React elements directly — it never
// calls dangerouslySetInnerHTML unless the rehype-raw plugin is added,
// which is deliberately never imported anywhere in this codebase. That
// alone blocks literal <script>/<img onerror> tags in the source text
// (they render as inert text, not markup). rehypeSanitize adds a second,
// independent layer on top: an allowlist-based pass over the parsed AST
// that also strips dangerous constructs markdown syntax itself can
// produce — javascript: URIs in [text](url) links/images, event-handler
// attributes, <style>/<script> nodes a plugin might otherwise introduce.
//
// Schema is defaultSchema (rehype-sanitize's GitHub-flavored-markdown-safe
// allowlist) with nothing loosened — no raw HTML passthrough, per the
// architecture doc's instruction, full stop.
//
// Styled via ReactMarkdown's `components` map with explicit Tailwind
// utility classes, matching every other component in this codebase, rather
// than the @tailwindcss/typography plugin's `prose` classes — that plugin
// isn't installed anywhere else here, and adding a new dependency just for
// this one component's styling isn't worth it when the palette (zinc/
// indigo) already has an established convention to match.
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
    // No `inline` prop in react-markdown v9+ — a fenced code block's <code>
    // always has a `language-*` className from remark-gfm; an inline
    // `code` span never does. That's the actual signal, not a prop.
    const isBlock = Boolean(className);
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
  return (
    <div>
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        rehypePlugins={[[rehypeSanitize, defaultSchema]]}
        components={markdownComponents}
      >
        {content}
      </ReactMarkdown>
    </div>
  );
}
