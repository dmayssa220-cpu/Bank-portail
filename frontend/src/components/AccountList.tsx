import { useEffect, useState } from 'react'
import { getAccounts, type AccountDto } from '../api/client'

export function AccountList() {
  const [accounts, setAccounts] = useState<AccountDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    getAccounts()
      .then(setAccounts)
      .catch(() => setError("Impossible de charger les comptes. Vérifiez que l'API est démarrée."))
      .finally(() => setLoading(false))
  }, [])

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
        </div>
      ))}
    </div>
  )
}
