import { useEffect, useState } from 'react'
import { getTransferHistory, type TransferHistoryItem } from '../api/client'

export function TransferHistory({ refreshKey }: { refreshKey: number }) {
  const [items, setItems] = useState<TransferHistoryItem[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    setLoading(true)
    getTransferHistory()
      .then(setItems)
      .finally(() => setLoading(false))
  }, [refreshKey])

  return (
    <div className="transfer-history">
      <h2>Historique des virements</h2>
      {loading && <div className="spinner" />}
      {!loading && items.length === 0 && <p className="empty-state">Aucun virement pour le moment.</p>}
      {!loading && items.length > 0 && (
        <div className="transfer-history__list">
          {items.map((t) => {
            const incoming = t.type === 'TransferIn'
            return (
              <div className="transfer-history__item" key={t.id}>
                <div>
                  <div className="transfer-history__label">{t.label}</div>
                  <div className="transfer-history__meta">
                    {incoming ? 'Sur' : 'Depuis'} {t.accountIban} · {new Date(t.createdAt).toLocaleString('fr-FR')}
                  </div>
                </div>
                <div className={incoming ? 'amount--credit' : 'transfer-history__amount'}>
                  {incoming ? '+' : '-'}{t.amount.toLocaleString('fr-FR')}
                </div>
              </div>
            )
          })}
        </div>
      )}
    </div>
  )
}
