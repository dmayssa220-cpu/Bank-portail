import { useState } from 'react'
import { createAccount, deleteAccount, type AccountDto } from '../api/client'

interface Props {
  accounts: AccountDto[]
  loading: boolean
  error: string | null
  onChanged: () => void
}

export function AccountList({ accounts, loading, error, onChanged }: Props) {
  const [creating, setCreating] = useState(false)

  const handleCreate = async () => {
    setCreating(true)
    try {
      await createAccount('TND')
      onChanged()
    } finally {
      setCreating(false)
    }
  }

  const handleDelete = async (id: string) => {
    if (!confirm('Fermer ce compte ? (uniquement possible si le solde est à zéro)')) return
    try {
      await deleteAccount(id)
      onChanged()
    } catch {
      alert('Impossible de fermer ce compte (solde non nul ?).')
    }
  }

  if (loading) return <p>Chargement des comptes...</p>
  if (error) return <p className="error">{error}</p>

  return (
    <div className="account-list">
      {accounts.length === 0 && <p>Aucun compte trouvé.</p>}
      {accounts.map((acc) => (
        <div className="account-card" key={acc.id}>
          <div className="account-card__header">
            <span>{acc.customerName}</span>
            <span className="account-card__iban">{acc.iban}</span>
          </div>
          <div className="account-card__balance">
            {acc.balance.toLocaleString('fr-FR', { minimumFractionDigits: 2 })} {acc.currency}
          </div>
          {acc.balance === 0 && (
            <button className="account-card__close" onClick={() => handleDelete(acc.id)}>
              Fermer ce compte
            </button>
          )}
        </div>
      ))}
      <button className="account-list__add" onClick={handleCreate} disabled={creating}>
        {creating ? 'Création...' : '+ Ouvrir un nouveau compte'}
      </button>
    </div>
  )
}
