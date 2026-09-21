import { useEffect, useState } from 'react'
import {
  getAccounts,
  getCards,
  createCard,
  toggleCardBlock,
  updateCardLimit,
  deleteCard,
  type AccountDto,
  type CardDto
} from '../api/client'

export function Cards() {
  const [cards, setCards] = useState<CardDto[]>([])
  const [accounts, setAccounts] = useState<AccountDto[]>([])
  const [loading, setLoading] = useState(true)
  const [creating, setCreating] = useState(false)
  const [selectedAccountId, setSelectedAccountId] = useState('')

  const load = async () => {
    setLoading(true)
    try {
      const [cardsData, accountsData] = await Promise.all([getCards(), getAccounts()])
      setCards(cardsData)
      setAccounts(accountsData)
      if (!selectedAccountId && accountsData.length > 0) {
        setSelectedAccountId(accountsData[0].id)
      }
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const handleCreate = async () => {
    if (!selectedAccountId) return
    setCreating(true)
    try {
      await createCard(selectedAccountId)
      await load()
    } finally {
      setCreating(false)
    }
  }

  const handleToggleBlock = async (card: CardDto) => {
    await toggleCardBlock(card.id, !card.isBlocked)
    await load()
  }

  const handleLimitChange = async (card: CardDto) => {
    const value = prompt('Nouveau plafond journalier :', String(card.dailyLimit))
    if (!value) return
    const parsed = parseFloat(value)
    if (!parsed || parsed <= 0) return
    await updateCardLimit(card.id, parsed)
    await load()
  }

  const handleDelete = async (card: CardDto) => {
    if (!confirm('Résilier définitivement cette carte ?')) return
    await deleteCard(card.id)
    await load()
  }

  if (loading) return <main className="dashboard"><div className="spinner" /></main>

  return (
    <main className="dashboard">
      <h1>Mes cartes bancaires</h1>

      <div className="transfer-form-card">
        <h2>Demander une nouvelle carte</h2>
        <div className="transfer-form">
          <label>
            Compte associé
            <select value={selectedAccountId} onChange={(e) => setSelectedAccountId(e.target.value)}>
              {accounts.map((a) => (
                <option key={a.id} value={a.id}>{a.iban}</option>
              ))}
            </select>
          </label>
          <button onClick={handleCreate} disabled={creating || accounts.length === 0}>
            {creating ? 'Création...' : '+ Demander une carte'}
          </button>
        </div>
      </div>

      <div className="account-list" style={{ marginTop: '1.5rem' }}>
        {cards.length === 0 && <p>Aucune carte pour le moment.</p>}
        {cards.map((card) => (
          <div className="account-card" key={card.id}>
            <div className="account-card__header">
              <span>{card.cardHolderName}</span>
              <span>{card.accountIban}</span>
            </div>
            <div className="account-card__balance" style={{ fontSize: '1.2rem' }}>
              {card.cardNumberMasked}
            </div>
            <p style={{ margin: '0.3rem 0', color: '#667085', fontSize: '0.85rem' }}>
              Expire le {new Date(card.expiryDate).toLocaleDateString('fr-FR')} · Plafond : {card.dailyLimit.toLocaleString('fr-FR')}/jour
            </p>
            <p style={{ margin: '0 0 0.5rem', fontWeight: 600, color: card.isBlocked ? '#d92d20' : '#12b76a' }}>
              {card.isBlocked ? '🔒 Bloquée' : '✅ Active'}
            </p>
            <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
              <button className="account-card__close" onClick={() => handleToggleBlock(card)}>
                {card.isBlocked ? 'Débloquer' : 'Bloquer'}
              </button>
              <button className="account-card__close" onClick={() => handleLimitChange(card)}>
                Modifier le plafond
              </button>
              <button className="account-card__close" onClick={() => handleDelete(card)}>
                Résilier
              </button>
            </div>
          </div>
        ))}
      </div>
    </main>
  )
}
