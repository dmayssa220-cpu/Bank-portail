import { useCallback, useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import {
  depositToAccount,
  downloadTransactionsCsv,
  getAccount,
  getAccountTransactions,
  getApiErrorMessage,
  type AccountDto,
  type TransactionDto
} from '../api/client'
import { TYPE_LABELS, isDebit } from '../utils/transactions'

const PAGE_SIZE = 15

export function AccountDetails() {
  const { id } = useParams<{ id: string }>()

  const [account, setAccount] = useState<AccountDto | null>(null)
  const [items, setItems] = useState<TransactionDto[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [type, setType] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [loading, setLoading] = useState(true)
  const [exporting, setExporting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE))

  const load = useCallback(async () => {
    if (!id) return
    setLoading(true)
    setError(null)
    try {
      const [acc, tx] = await Promise.all([
        getAccount(id),
        getAccountTransactions(id, { type, from, to, page, pageSize: PAGE_SIZE })
      ])
      setAccount(acc)
      setItems(tx.items)
      setTotal(tx.total)
    } catch (err) {
      setError(getApiErrorMessage(err, "Impossible de charger les opérations de ce compte."))
    } finally {
      setLoading(false)
    }
  }, [id, type, from, to, page])

  useEffect(() => {
    load()
  }, [load])

  // Changer un filtre ramène à la première page
  const changeFilter = (setter: (value: string) => void) => (value: string) => {
    setter(value)
    setPage(1)
  }

  const handleExport = async () => {
    if (!id) return
    setExporting(true)
    try {
      await downloadTransactionsCsv(id, { type, from, to })
    } catch (err) {
      alert(getApiErrorMessage(err, "L'export a échoué."))
    } finally {
      setExporting(false)
    }
  }

  const handleDeposit = async () => {
    if (!id || !account) return
    const value = prompt('Montant à déposer (simulation, 10 000 maximum) :', '500')
    if (!value) return

    const amount = parseFloat(value.replace(',', '.'))
    if (!amount || amount <= 0) return

    try {
      await depositToAccount(id, amount)
      await load()
    } catch (err) {
      alert(getApiErrorMessage(err, 'Dépôt impossible.'))
    }
  }

  if (error) {
    return (
      <main className="dashboard">
        <Link to="/dashboard" className="back-link">← Retour aux comptes</Link>
        <p className="error">{error}</p>
      </main>
    )
  }

  return (
    <main className="dashboard">
      <Link to="/dashboard" className="back-link">← Retour aux comptes</Link>

      {account && (
        <div className="account-card account-card--hero">
          <div className="account-card__header">
            <span>{account.customerName}</span>
            <span className="account-card__iban">{account.iban}</span>
          </div>
          <div className="account-card__balance">
            {account.balance.toLocaleString('fr-FR', { minimumFractionDigits: 2 })} {account.currency}
          </div>
          <div className="account-card__actions">
            <button className="btn-outline" onClick={handleDeposit}>Déposer (démo)</button>
            <button className="btn-outline" onClick={handleExport} disabled={exporting}>
              {exporting ? 'Export...' : '⬇ Exporter en CSV'}
            </button>
          </div>
        </div>
      )}

      <div className="transfer-history">
        <h2>Opérations</h2>

        <div className="tx-filters">
          <label>
            Type
            <select value={type} onChange={(e) => changeFilter(setType)(e.target.value)}>
              <option value="">Tous</option>
              {Object.entries(TYPE_LABELS).map(([value, label]) => (
                <option key={value} value={value}>{label}</option>
              ))}
            </select>
          </label>
          <label>
            Du
            <input type="date" value={from} max={to || undefined} onChange={(e) => changeFilter(setFrom)(e.target.value)} />
          </label>
          <label>
            Au
            <input type="date" value={to} min={from || undefined} onChange={(e) => changeFilter(setTo)(e.target.value)} />
          </label>
          {(type || from || to) && (
            <button
              className="btn-outline"
              onClick={() => { setType(''); setFrom(''); setTo(''); setPage(1) }}
            >
              Réinitialiser
            </button>
          )}
        </div>

        {loading && <div className="spinner" />}

        {!loading && items.length === 0 && <p className="empty-state">Aucune opération pour ces critères.</p>}

        {!loading && items.length > 0 && (
          <div className="tx-table-wrapper">
            <table className="tx-table">
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Libellé</th>
                  <th>Type</th>
                  <th className="tx-table__amount">Montant</th>
                </tr>
              </thead>
              <tbody>
                {items.map((t) => {
                  const debit = isDebit(t.type)
                  return (
                    <tr key={t.id}>
                      <td>{new Date(t.createdAt).toLocaleString('fr-FR')}</td>
                      <td>{t.label}</td>
                      <td><span className="tx-badge">{TYPE_LABELS[t.type] ?? t.type}</span></td>
                      <td className={`tx-table__amount ${debit ? 'amount--debit' : 'amount--credit'}`}>
                        {debit ? '-' : '+'}{t.amount.toLocaleString('fr-FR', { minimumFractionDigits: 2 })} {account?.currency}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}

        {total > PAGE_SIZE && (
          <div className="pagination">
            <button className="btn-outline" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>← Précédent</button>
            <span>Page {page} / {totalPages} · {total} opérations</span>
            <button className="btn-outline" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>Suivant →</button>
          </div>
        )}
      </div>
    </main>
  )
}
