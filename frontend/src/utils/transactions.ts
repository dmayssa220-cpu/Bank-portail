export const TYPE_LABELS: Record<string, string> = {
  Deposit: 'Dépôt',
  Withdrawal: 'Retrait',
  TransferOut: 'Virement émis',
  TransferIn: 'Virement reçu'
}

// Une opération qui diminue le solde du compte
export const isDebit = (type: string): boolean => type === 'Withdrawal' || type === 'TransferOut'
