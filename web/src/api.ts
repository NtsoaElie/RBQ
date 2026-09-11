export type Source = {
  content: string;
  filename: string;
  pageNumber: number;
  similarity: number;
};

export type AskResponse = {
  answer: string;
  sources: Source[];
};

/** The only call to the C# query pipeline. */
export async function ask(question: string): Promise<AskResponse> {
  const response = await fetch("/api/ask", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ question }),
  });

  if (!response.ok) {
    const { error } = await response.json().catch(() => ({ error: "" }));
    throw new Error(error || `Request failed (${response.status}).`);
  }

  return response.json();
}
