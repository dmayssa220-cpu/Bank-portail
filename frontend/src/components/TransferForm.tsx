import { useState, type FormEvent } from 'react'
import { createTransfer, type AccountDto, type TransferResult } from '../api/client'

interface Props {
  accounts: AccountDto[]
  onTransferDone: () => void
}

export function TransferForm({ accounts, onTransferDone }: Props) {
  const [fromAccountId, setFromAccountId] = useState(accounts[0]?.id ?? '')
  const [toIban, setToIban] = useState('')
  const [amount, setAmount] = useState('')
  const [label, setLabel] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [result, setResult] = useState<TransferResult | null>(null)

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setError(null)
    setResult(null)

    const numericAmount = parseFloat(amount)
    if (!fromAccountId || !toIban || !numericAmount || numericAmount <= 0) {
      setError('Merci de remplir tous les champs avec un montant valide.')
      return
    }

    setLoading(true)
    try {
      const res = await createTransfer(fromAccountId, toIban, numericAmount, label || undefined)
      setResult(res)
      setToIban('')
      setAmount('')
      setLabel('')
      onTransferDone()
    } catch {
      setError("Le virement a échoué (solde insuffisant ou erreur serveur).")
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="transfer-form-card">
      <h2>Nouveau virement</h2>
      <form onSubmit={handleSubmit} className="transfer-form">
        <label>
          Compte source
          <select value={fromAccountId} onChange={(e) => setFromAccountId(e.target.value)}>
            {accounts.map((acc) => (
              <option key={acc.id} value={acc.id}>
                {acc.iban} — {acc.balance.toLocaleString('fr-FR')} {acc.currency}
              </option>
            ))}
          </select>
        </label>

        <label>
          IBAN du bénéficiaire
          <input value={toIban} onChange={(e) => setToIban(e.target.value)} placeholder="FR76 ..." required />
        </label>

        <label>
          Montant
          <input
            type="number"
            step="0.01"
            min="0.01"
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
            required
          />
        </label>

        <label>
          Libellé (optionnel)
          <input value={label} onChange={(e) => setLabel(e.target.value)} placeholder="Ex: Loyer" />
        </label>

        {error && <p className="error">{error}</p>}

        <button type="submit" disabled={loading || accounts.length === 0}>
          {loading ? 'Envoi...' : 'Effectuer le virement'}
        </button>
      </form>

      {result && (
        <div className="transfer-result">
          <p>✅ Virement effectué. Nouveau solde : <strong>{result.newBalance.toLocaleString('fr-FR')}</strong></p>
          {result.fraudAlert && (
            <div className="fraud-alert">
              ⚠️ Opération signalée comme potentiellement à risque ({(result.fraudAlert.probability * 100).toFixed(0)}%) :
              <ul>
                {result.fraudAlert.reasons.map((r, i) => <li key={i}>{r}</li>)}
              </ul>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
