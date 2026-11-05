import { useEffect, useState, type FormEvent } from 'react'
import {
  createTransfer,
  getApiErrorMessage,
  newIdempotencyKey,
  type AccountDto,
  type TransferResult
} from '../api/client'

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

  // Clé d'idempotence : elle reste la même tant que le formulaire n'est pas modifié. Un double clic ou
  // un nouvel essai après une coupure réseau renvoie donc le résultat d'origine, sans second débit.
  // Dès que l'utilisateur change un champ (ou après un succès, les champs étant vidés), une nouvelle clé est créée.
  const [idempotencyKey, setIdempotencyKey] = useState(newIdempotencyKey)
  useEffect(() => {
    setIdempotencyKey(newIdempotencyKey())
  }, [fromAccountId, toIban, amount, label])

  // Si le compte sélectionné disparaît (fermeture), on revient sur le premier compte disponible
  useEffect(() => {
    if (!accounts.some((a) => a.id === fromAccountId)) {
      setFromAccountId(accounts[0]?.id ?? '')
    }
  }, [accounts, fromAccountId])

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    if (loading) return

    setError(null)
    setResult(null)

    const numericAmount = parseFloat(amount.replace(',', '.'))
    if (!fromAccountId || !toIban.trim() || !numericAmount || numericAmount <= 0) {
      setError('Merci de remplir tous les champs avec un montant valide.')
      return
    }

    setLoading(true)
    try {
      const res = await createTransfer(fromAccountId, toIban.trim(), numericAmount, label || undefined, idempotencyKey)
      setResult(res)
      setToIban('')
      setAmount('')
      setLabel('')
      onTransferDone()
    } catch (err) {
      setError(getApiErrorMessage(err, 'Le virement a échoué (erreur serveur). Vous pouvez réessayer sans risque de double débit.'))
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="transfer-form-card">
      <h2>Nouveau virement</h2>
      <p className="security-help">
        Si l'IBAN appartient à un autre client de la banque, le virement est instantané et le bénéficiaire est crédité.
        Sinon, il est traité comme un virement externe.
      </p>
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
          <input value={toIban} onChange={(e) => setToIban(e.target.value)} placeholder="TN59 1000 ... ou FR76 ..." required />
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
          <p>
            ✅{' '}
            {result.replayed
              ? 'Ce virement avait déjà été enregistré : aucun second débit.'
              : result.isInternal
                ? `Virement interne effectué${result.beneficiaryName ? ` vers ${result.beneficiaryName}` : ''}.`
                : 'Virement externe effectué.'}
          </p>
          <p>
            Nouveau solde du compte source : <strong>{result.newBalance.toLocaleString('fr-FR')}</strong>
          </p>
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
