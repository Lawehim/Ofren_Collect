import { useState, type FormEvent } from 'react';
import { useMutation } from '@tanstack/react-query';
import { api, ApiError } from '../api/client';
import { useAuth } from '../auth/useAuth';
import type { AssistantAnswer } from '../types/models';

// The fixed set of questions the grounded assistant can answer. Offered as one-tap prompts so a
// user discovers its scope without guessing; anything else is politely declined by the backend.
const SUGGESTIONS = [
  'How much have I collected this week?',
  'How many customers are overdue?',
  'How many invoices are underpaid?',
  'How many active subscriptions do I have?',
];

export function AssistantPanel() {
  const { auth } = useAuth();
  const token = auth!.token;
  const [question, setQuestion] = useState('');

  const ask = useMutation<AssistantAnswer, Error, string>({
    mutationFn: (q: string) => api.askAssistant(token, q),
  });

  const submit = (q: string) => {
    const trimmed = q.trim();
    if (!trimmed || ask.isPending) return;
    setQuestion(trimmed);
    ask.mutate(trimmed);
  };

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();
    submit(question);
  };

  const answer = ask.data;
  const errorText =
    ask.error instanceof ApiError && ask.error.status === 401
      ? 'Your session has expired — sign in again.'
      : ask.error
        ? 'The assistant is unavailable right now. Please try again.'
        : null;

  return (
    <div className="panel assistant">
      <div className="panel-head">
        <h3>Ask about your collections</h3>
        <span className="assistant-tag">AI · read-only</span>
      </div>

      <div className="assistant-body">
        <form className="assistant-ask" onSubmit={onSubmit}>
          <input
            type="text"
            value={question}
            maxLength={500}
            placeholder="e.g. How much have I collected this week?"
            onChange={(event) => setQuestion(event.target.value)}
            aria-label="Ask the assistant a question"
          />
          <button type="submit" className="btn primary" disabled={ask.isPending || !question.trim()}>
            {ask.isPending ? 'Thinking…' : 'Ask'}
          </button>
        </form>

        <div className="assistant-chips">
          {SUGGESTIONS.map((s) => (
            <button key={s} type="button" className="chip" disabled={ask.isPending} onClick={() => submit(s)}>
              {s}
            </button>
          ))}
        </div>

        {errorText && <div className="assistant-answer err">{errorText}</div>}

        {answer && !errorText && (
          <div className={`assistant-answer ${answer.grounded ? 'grounded' : 'declined'}`}>
            <span className="assistant-badge">{answer.grounded ? 'From your data' : 'Not answered'}</span>
            <p>{answer.answer}</p>
          </div>
        )}

        <p className="assistant-note">
          Grounded &amp; read-only: every figure is computed from your own data, and the assistant can
          never create, change, or move money.
        </p>
      </div>
    </div>
  );
}
