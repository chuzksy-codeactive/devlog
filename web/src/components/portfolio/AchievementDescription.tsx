import { toAchievementLines } from "@/lib/experience-achievement";

export function AchievementDescription({
  text,
  paragraphClassName,
  listClassName,
  maxItems,
}: {
  text: string;
  paragraphClassName: string;
  listClassName: string;
  maxItems?: number;
}) {
  const lines = toAchievementLines(text);
  const renderedLines =
    typeof maxItems === "number" && maxItems > 0 ? lines.slice(0, maxItems) : lines;

  if (renderedLines.length <= 1) {
    return <p className={paragraphClassName}>{renderedLines[0] ?? text}</p>;
  }

  return (
    <ul className={listClassName}>
      {renderedLines.map((line, i) => (
        <li key={`${line}-${i}`}>{line}</li>
      ))}
    </ul>
  );
}
