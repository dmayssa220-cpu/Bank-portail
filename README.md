# Bank App — Squelette technique

Squelette fonctionnel : **Backend .NET 8** + **Frontend React (Vite/TS)** + **PostgreSQL**, entièrement conteneurisé avec Docker.

## Étape par étape : lancer le projet


### Étape 1 — Récupérer les fichiers
Placez le dossier `bank-app/` sur votre machine, puis ouvrez un terminal à sa racine.

```bash
cd bank-app
```

### Étape 2 — Configurer les variables d'environnement

Modifiez `.env` si besoin (mot de passe PostgreSQL, clé JWT). Pour un premier test local, les valeurs par défaut suffisent.

### Étape 3 — Construire et démarrer les conteneurs
```bash
docker compose up --build
```
Cette commande :
1. Télécharge l'image PostgreSQL et démarre la base de données.
2. Construit l'image du backend .NET (restore NuGet + compilation) et la démarre.
3. Construit l'image du frontend React (npm install + build) servie par Nginx.

Le tout premier build peut prendre quelques minutes (téléchargement des images .NET SDK, Node, PostgreSQL).

### Étape 4 — Vérifier que tout fonctionne
| Service | URL | Vérification |
|---|---|---|
| Frontend | http://localhost:3000 | Le tableau de bord affiche un compte de démonstration |
| Backend (Swagger) | http://localhost:5000/swagger | Documentation interactive de l'API |
| Backend (santé) | http://localhost:5000/health | Doit renvoyer `{"status":"healthy"}` |
| PostgreSQL | localhost:5432 | Accessible via un client (DBeaver, pgAdmin) avec les identifiants du `.env` |


### Étape 5 — Tester l'API manuellement (optionnel)
```bash
# Lister les comptes
curl http://localhost:5000/api/accounts

# Se connecter (stub d'authentification, sans vérification de mot de passe pour l'instant)
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"ahmed.bensalah@example.com","password":"peu-importe-ici"}'
```

### Étape 6 — Développement au quotidien
- **Modifier le backend** : éditez les fichiers dans `backend/BankApi/`, puis relancez `docker compose up --build backend`.
- **Modifier le frontend** : pour un rechargement à chaud plus rapide en développement, vous pouvez lancer le frontend hors Docker :
  ```bash
  cd frontend
  npm install
  npm run dev
  ```
  (Vite proxyfie automatiquement `/api` vers `http://localhost:5000`, voir `vite.config.ts`.)
- **Arrêter les conteneurs** :
  ```bash
  docker compose down
  ```
- **Tout réinitialiser (y compris les données)** :
  ```bash
  docker compose down -v
  ```

---

