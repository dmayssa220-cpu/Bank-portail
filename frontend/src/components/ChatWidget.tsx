import { useState, useRef, useEffect } from 'react'
import { askChatbot, type ChatTurn } from '../api/client'

export function ChatWidget() {
  const [open, setOpen] = useState(false)
  const [messages, setMessages] = useState<ChatTurn[]>([
    { role: 'assistant', content: 'Bonjour 👋 Posez-moi une question sur votre compte ou vos dernières opérations.' }
  ])
  const [input, setInput] = useState('')
  const [loading, setLoading] = useState(false)
  const bottomRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages, open])

  const send = async () => {
    const text = input.trim()
    if (!text || loading) return

    const nextMessages: ChatTurn[] = [...messages, { role: 'user', content: text }]
    setMessages(nextMessages)
    setInput('')
    setLoading(true)

    try {
      const answer = await askChatbot(text, messages)
      setMessages([...nextMessages, { role: 'assistant', content: answer }])
    } catch {
      setMessages([
        ...nextMessages,
        { role: 'assistant', content: "Le chatbot est indisponible pour le moment (vérifiez qu'Ollama tourne bien)." }
      ])
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="chat-widget">
      {open && (
        <div className="chat-panel">
          <div className="chat-panel__header">
            <span>Assistant Ma Banque</span>
            <button onClick={() => setOpen(false)} aria-label="Fermer">✕</button>
          </div>
          <div className="chat-panel__messages">
            {messages.map((m, i) => (
              <div key={i} className={`chat-bubble chat-bubble--${m.role}`}>
                {m.content}
              </div>
            ))}
            {loading && <div className="chat-bubble chat-bubble--assistant">…</div>}
            <div ref={bottomRef} />
          </div>
          <div className="chat-panel__input">
            <input
              value={input}
              onChange={(e) => setInput(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && send()}
              placeholder="Votre question..."
            />
            <button onClick={send} disabled={loading}>Envoyer</button>
          </div>
        </div>
      )}
      <button className="chat-toggle" onClick={() => setOpen(!open)}>
        {open ? '✕' : '💬'}
      </button>
    </div>
  )
}
