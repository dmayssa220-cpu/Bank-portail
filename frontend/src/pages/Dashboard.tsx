import { useCallback, useEffect, useState } from 'react'
import { AccountList } from '../components/AccountList'
import { TransferForm } from '../components/TransferForm'
import { TransferHistory } from '../components/TransferHistory'
import { getAccounts, type AccountDto } from '../api/client'
import { useAuth } from '../auth/AuthContext'

export function Dashboard() {
  const [accounts, setAccounts] = useState<AccountDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [historyKey, setHistoryKey] = useState(0)
  const { fullName, logout } = useAuth()

  const loadAccounts = useCallback(() => {
    setLoading(true)
    getAccounts()
      .then(setAccounts)
      .catch(() => setError("Impossible de charger les comptes."))
      .finally(() => setLoading(false))
  }, [])

  useEffect(() => {
    loadAccounts()
  }, [loadAccounts])

  const handleTransferDone = () => {
    loadAccounts()
    setHistoryKey((k) => k + 1) // force le rafraîchissement de l'historique
  }

  return (
    <main className="dashboard">
      <div className="dashboard__topbar">
        <div>
          <h1>Tableau de bord</h1>
          <p>Bienvenue, {fullName}</p>
        </div>
        <button className="logout-btn" onClick={logout}>Déconnexion</button>
      </div>

      <AccountList accounts={accounts} loading={loading} error={error} onChanged={loadAccounts} />

      {accounts.length > 0 && (
        <TransferForm accounts={accounts} onTransferDone={handleTransferDone} />
      )}

      <TransferHistory refreshKey={historyKey} />
    </main>
  )
}
