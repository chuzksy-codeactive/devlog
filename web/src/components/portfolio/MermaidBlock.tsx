"use client";

import { useEffect, useId, useState } from "react";
import mermaid from "mermaid";
import DOMPurify from "dompurify";

let mermaidInitialized = false;

function initializeMermaid() {
  if (!mermaidInitialized) {
    mermaid.initialize({
      startOnLoad: false,
      securityLevel: "strict",
      theme: "neutral",
      flowchart: {
        htmlLabels: false,
      },
    });
    mermaidInitialized = true;
  }
}

export function MermaidBlock({ chart }: { chart: string }) {
  const [svg, setSvg] = useState<string>("");
  const [renderError, setRenderError] = useState<string>("");
  const diagramId = useId().replace(/:/g, "-");

  useEffect(() => {
    let disposed = false;
    initializeMermaid();

    mermaid
      .render(`mermaid-${diagramId}`, chart)
      .then(({ svg: renderedSvg }) => {
        if (!disposed) {
          const sanitizedSvg = DOMPurify.sanitize(renderedSvg, {
            USE_PROFILES: { svg: true },
          });
          setRenderError("");
          setSvg(sanitizedSvg);
        }
      })
      .catch((error: unknown) => {
        if (!disposed) {
          setSvg("");
          const detail = error instanceof Error ? error.message : "Unable to render Mermaid diagram.";
          setRenderError(detail);
        }
      });

    return () => {
      disposed = true;
    };
  }, [chart, diagramId]);

  if (renderError) {
    return (
      <pre className="mt-4 overflow-x-auto rounded-lg border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900">
        {renderError}
      </pre>
    );
  }

  if (!svg) {
    return (
      <div className="mt-4 rounded-lg border border-zinc-200 bg-zinc-50 px-4 py-3 text-sm text-zinc-500">
        Rendering diagram...
      </div>
    );
  }

  return (
    <div
      className="mermaid-block mt-4 overflow-x-auto rounded-lg border border-zinc-200 bg-white p-4"
      dangerouslySetInnerHTML={{ __html: svg }}
    />
  );
}
