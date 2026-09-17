# Bank App — Squelette technique

Squelette fonctionnel : **Backend .NET 8** + **Frontend React (Vite/TS)** + **PostgreSQL**, entièrement conteneurisé avec Docker.

## Structure du projet

```
bank-app/
├── backend/
│   └── BankApi/            # API .NET 8 (Web API + EF Core + PostgreSQL)
│       ├── Controllers/    # AccountsController, TransfersController, AuthController
│       ├── Models/         # Customer, Account, Transaction
│       ├── Data/           # BankDbContext (EF Core)
│       ├── DTOs/
│       ├── Program.cs
│       └── Dockerfile
├── frontend/
│   └── src/                # Application React (Vite + TypeScript)
│       ├── api/client.ts   # Client HTTP (axios)
│       ├── components/
│       ├── pages/
│       ├── Dockerfile
│       └── nginx.conf      # Reverse proxy /api -> backend
├── docker-compose.yml
└── .env.example
```

---

## Étape par étape : lancer le projet

### Prérequis
- Docker et Docker Compose installés (`docker --version`, `docker compose version`).
- Aucun besoin d'installer .NET SDK ou Node.js en local : tout se construit dans les conteneurs.

### Étape 1 — Récupérer les fichiers
Placez le dossier `bank-app/` sur votre machine, puis ouvrez un terminal à sa racine.

```bash
cd bank-app
```

### Étape 2 — Configurer les variables d'environnement
```bash
cp .env.example .env
```
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

Au premier démarrage, le backend crée automatiquement le schéma et insère un client + compte de démonstration (`Ahmed Ben Salah`, IBAN `TN59 1000 6035...`).

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

## Prochaines étapes techniques recommandées

Ce squelette est volontairement simplifié pour être fonctionnel rapidement. Avant toute mise en production bancaire réelle :

1. **Remplacer `EnsureCreated()` par de vraies migrations EF Core** :
   ```bash
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   ```
2. **Sécuriser `AuthController`** : hashage des mots de passe (ASP.NET Identity), MFA, refresh tokens.
3. **Ajouter la validation des entrées** (FluentValidation) sur `TransfersController`.
4. **Ajouter un bus d'événements** (RabbitMQ) pour découpler notifications, détection de fraude et synchronisation Odoo — voir le document *Architecture Microservices & Sécurité*.
5. **Ajouter les tests** (xUnit côté backend, Vitest/React Testing Library côté frontend).
6. **Ajouter un pipeline CI/CD** (GitHub Actions / GitLab CI) pour builder et tester automatiquement à chaque commit.

Ce squelette correspond à la **Phase 1 (Socle)** de la roadmap technique définie dans le document d'architecture.
