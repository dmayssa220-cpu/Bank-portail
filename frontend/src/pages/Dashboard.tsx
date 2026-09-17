import { AccountList } from '../components/AccountList'

export function Dashboard() {
  return (
    <main className="dashboard">
      <h1>Tableau de bord</h1>
      <p>Vue d'ensemble de vos comptes</p>
      <AccountList />
    </main>
  )
}
