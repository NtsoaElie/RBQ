import { useEffect, useRef, useState, type FormEvent } from "react";
import { ask, type Source } from "./api";

type Turn = {
  question: string;
  answer?: string;
  sources?: Source[];
  error?: string;
};

const SUGGESTIONS = [
  "Quelles sont les compétences requises en administration ?",
  "Quelle est la note de passage à l'examen ?",
  "Comment gérer la santé et sécurité sur un chantier ?",
];

export default function App() {
  const [question, setQuestion] = useState("");
  const [turns, setTurns] = useState<Turn[]>([]);
  const [pending, setPending] = useState(false);
  const endOfThread = useRef<HTMLDivElement>(null);

  useEffect(() => {
    endOfThread.current?.scrollIntoView({ behavior: "smooth", block: "end" });
  }, [turns, pending]);

  /** Appends the turn, then fills in its answer or error once the API replies. */
  async function send(asked: string) {
    if (!asked || pending) return;

    const index = turns.length;
    setTurns((current) => [...current, { question: asked }]);
    setQuestion("");
    setPending(true);

    const result = await ask(asked).catch((error: Error) => ({ error: error.message }));

    setTurns((current) =>
      current.map((turn, i) => (i === index ? { ...turn, ...result } : turn)),
    );
    setPending(false);
  }

  function submit(event: FormEvent) {
    event.preventDefault();
    void send(question.trim());
  }

  return (
    <>
      <header className="header">
        <div className="brand">
          <Logo size={28} />
          <span className="wordmark">RBQPrep</span>
        </div>
        <span className="badge">Coach IA</span>
      </header>

      <main className="page">
        {turns.length === 0 ? (
          <>
            <span className="eyebrow">Références officielles · Québec</span>
            <h1>Posez vos questions sur les examens RBQ</h1>
            <p className="lede">
              Chaque réponse s'appuie sur vos documents et cite le fichier et la page
              d'où elle provient.
            </p>

            <section className="suggestions">
              <h2>Pour commencer</h2>
              <div className="chips">
                {SUGGESTIONS.map((suggestion) => (
                  <button
                    key={suggestion}
                    type="button"
                    className="chip"
                    onClick={() => void send(suggestion)}
                  >
                    {suggestion}
                  </button>
                ))}
              </div>
            </section>
          </>
        ) : (
          <section className="thread">
            {turns.map((turn, i) => (
              <Turn key={i} turn={turn} pending={pending && i === turns.length - 1} />
            ))}
            <div ref={endOfThread} />
          </section>
        )}

        <form className="composer" onSubmit={submit}>
          <input
            value={question}
            onChange={(event) => setQuestion(event.target.value)}
            placeholder="Posez votre question…"
            aria-label="Votre question"
          />
          <button type="submit" disabled={pending || !question.trim()}>
            {pending ? "…" : "Demander"}
          </button>
        </form>

        <p className="footnote">
          Basé sur vos documents indexés. Non affilié à la Régie du bâtiment du Québec.
        </p>
      </main>
    </>
  );
}

function Turn({ turn, pending }: { turn: Turn; pending: boolean }) {
  return (
    <>
      <p className="question">{turn.question}</p>

      <div className="reply">
        <Logo size={28} />
        <div className="card">
          {pending && <p className="status dots">Recherche dans vos documents</p>}
          {turn.error && <p className="status error">{turn.error}</p>}
          {turn.answer && <p className="answer">{turn.answer}</p>}

          {turn.sources && turn.sources.length > 0 && (
            <details className="sources">
              <summary>
                {turn.sources.length} source{turn.sources.length > 1 ? "s" : ""}
              </summary>
              {turn.sources.map((source, i) => (
                <div className="source" key={i}>
                  <p className="cite">
                    <span className="pill">{source.filename}</span>
                    <span>page {source.pageNumber}</span>
                    <span>· {(source.similarity * 100).toFixed(0)} % de similarité</span>
                  </p>
                  <p className="excerpt">{source.content}</p>
                </div>
              ))}
            </details>
          )}
        </div>
      </div>
    </>
  );
}

/** The RBQPrep mark: a hard hat with a check. */
function Logo({ size }: { size: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 512 512" role="img" aria-label="RBQPrep">
      <rect width="512" height="512" rx="128" fill="#ce742c" />
      <path
        d="M256 148c-64 0-108 48-108 108v28H116v32h280v-32H364v-28c0-60-44-108-108-108z"
        fill="#fff"
      />
      <path d="M148 284h216" stroke="#fff" strokeWidth="16" strokeLinecap="round" />
      <path
        d="M216 244l28 28 60-60"
        stroke="#ce742c"
        strokeWidth="28"
        strokeLinecap="round"
        strokeLinejoin="round"
        fill="none"
      />
    </svg>
  );
}
