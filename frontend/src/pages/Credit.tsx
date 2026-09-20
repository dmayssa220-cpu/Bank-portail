import { useEffect, useState, type FormEvent } from 'react'
import {
  simulateCredit,
  applyForCredit,
  getMyCreditRequests,
  type CreditSimulationResult,
  type CreditRequestResult
} from '../api/client'

const STATUS_LABELS: Record<string, string> = {
  Pending: 'En attente',
  Approved: 'Approuvé',
  Rejected: 'Refusé'
}

export function Credit() {
  const [amount, setAmount] = useState('5000')
  const [duration, setDuration] = useState('24')
  const [simulation, setSimulation] = useState<CreditSimulationResult | null>(null)
  const [requests, setRequests] = useState<CreditRequestResult[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [applied, setApplied] = useState(false)

  const loadRequests = () => {
    getMyCreditRequests().then(setRequests).catch(() => {})
  }

  useEffect(() => {
    loadRequests()
  }, [])

  const handleSimulate = async (e: FormEvent) => {
    e.preventDefault()
    setError(null)
    setApplied(false)
    setLoading(true)
    try {
      const result = await simulateCredit(parseFloat(amount), parseInt(duration, 10))
      setSimulation(result)
    } catch {
      setError('Simulation impossible — vérifiez le montant et la durée saisis.')
      setSimulation(null)
    } finally {
      setLoading(false)
    }
  }

  const handleApply = async () => {
    setLoading(true)
    try {
      await applyForCredit(parseFloat(amount), parseInt(duration, 10))
      setApplied(true)
      loadRequests()
    } catch {
      setError("La demande n'a pas pu être enregistrée.")
    } finally {
      setLoading(false)
    }
  }

  return (
    <main className="dashboard">
      <h1>Simulateur de crédit</h1>
      <p>Estimez vos mensualités, puis déposez une demande si le résultat vous convient.</p>

      <div className="transfer-form-card">
        <form onSubmit={handleSimulate} className="transfer-form">
          <label>
            Montant souhaité
            <input type="number" min="500" step="100" value={amount} onChange={(e) => setAmount(e.target.value)} />
          </label>

          <label>
            Durée (en mois)
            <input type="number" min="6" max="120" value={duration} onChange={(e) => setDuration(e.target.value)} />
          </label>

          {error && <p className="error">{error}</p>}

          <button type="submit" disabled={loading}>
            {loading ? 'Calcul...' : 'Simuler'}
          </button>
        </form>

        {simulation && (
          <div className="transfer-result">
            <p>Taux annuel : <strong>{simulation.annualRatePercent}%</strong></p>
            <p>Mensualité estimée : <strong>{simulation.monthlyPayment.toLocaleString('fr-FR')}</strong></p>
            <p>Coût total du crédit : <strong>{simulation.totalCost.toLocaleString('fr-FR')}</strong></p>
            <p>Dont intérêts : {simulation.totalInterest.toLocaleString('fr-FR')}</p>

            {!applied ? (
              <button onClick={handleApply} disabled={loading} style={{ marginTop: '0.75rem' }}>
                Déposer une demande
              </button>
            ) : (
              <p>✅ Demande enregistrée, statut : En attente</p>
            )}
          </div>
        )}
      </div>

      <h2 style={{ marginTop: '2rem' }}>Mes demandes de crédit</h2>
      <div className="account-list">
        {requests.length === 0 && <p>Aucune demande pour le moment.</p>}
        {requests.map((r) => (
          <div className="account-card" key={r.id}>
            <div className="account-card__header">
              <span>{new Date(r.createdAt).toLocaleDateString('fr-FR')}</span>
              <span>{STATUS_LABELS[r.status] ?? r.status}</span>
            </div>
            <div className="account-card__balance">
              {r.amount.toLocaleString('fr-FR')} sur {r.durationMonths} mois
            </div>
            <p style={{ margin: '0.3rem 0 0', color: '#667085', fontSize: '0.85rem' }}>
              Mensualité : {r.monthlyPayment.toLocaleString('fr-FR')}
            </p>
          </div>
        ))}
      </div>
    </main>
  )
}
