import { toAchievementLines } from "@/lib/experience-achievement";

export function AchievementDescription({
  text,
  paragraphClassName,
  listClassName,
}: {
  text: string;
  paragraphClassName: string;
  listClassName: string;
}) {
  const lines = toAchievementLines(text);

  if (lines.length <= 1) {
    return <p className={paragraphClassName}>{text}</p>;
  }

  return (
    <ul className={listClassName}>
      {lines.map((line, i) => (
        <li key={`${line}-${i}`}>{line}</li>
      ))}
    </ul>
  );
}
