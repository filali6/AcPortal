# AcPortal

AcPortal est un portail d'entreprise modulaire compose d'une API ASP.NET Core et d'une application Angular. La plateforme integre l'authentification Keycloak, la communication interservices avec Dapr, les notifications temps reel via SignalR et un systeme de plugins.

## Fonctionnalites principales

- Gestion des utilisateurs, roles, equipes, projets et taches
- Authentification JWT avec Keycloak
- Modules metier pour les contrats, evenements, notifications, chat, outils, Git et tableaux de bord
- Notifications et chat en temps reel avec SignalR
- Publication d'evenements via Dapr
- Regles de workflow configurables dans `Backend/workflow-config.json`
- Integrations IA avec Semantic Kernel et Google Gemini
- Metriques Prometheus et tableaux de bord Grafana
- Interface multilingue avec Angular et `@ngx-translate/core`

## Architecture

```text
Frontend (Angular 19 :4200)
          |
          v
Backend (ASP.NET Core 10 :5281)
   |               |        |
   v               v        v
Postgres         Keycloak  Dapr
                    
                
```

### Structure du depot

- `Backend/` : API ASP.NET Core, modules metier, persistance et infrastructure
- `Backend.Tests/` : tests unitaires et d'integration xUnit
- `Frontend/` : application Angular standalone
- `DemoSsoPlugin/` : plugin de demonstration SSO
- `Backend/docker-compose.yml` : environnement local complet
- `Backend/dapr/` : composants et configuration Dapr
- `Backend/monitoring/` : configuration Prometheus et Grafana

## Prerequis

Pour le developpement local :

- .NET SDK 10
- Node.js et npm
- Docker Desktop et Docker Compose
- Dapr CLI, uniquement pour le lancement avec un sidecar local

Keycloak, PostgreSQL, Redis et les autres services necessaires peuvent etre demarres avec Docker Compose.

## Demarrage rapide avec Docker

Depuis la racine du depot :

```powershell
cd Backend
docker compose up --build
```

Les services principaux sont alors accessibles ici :

| Service | URL |
| --- | --- |
| Frontend | http://localhost:4200 |
| API Backend | http://localhost:5281 |
| Keycloak | http://localhost:8181 |
| Kafka UI | http://localhost:8080 |
| Grafana | http://localhost:3000 |
| Prometheus | http://localhost:9090 |
| Demo SSO Plugin | http://localhost:5300 |

Le realm Keycloak est importe automatiquement depuis `Backend/acp-realm.json`.

Identifiants locaux fournis par Docker Compose :

- Keycloak : `admin` / `admin`
- PostgreSQL principal : `postgres` / `postgres`
- Grafana : `admin` / `admin`

Ces identifiants sont reserves au developpement local et doivent etre modifies dans un environnement partage ou de production.

## Developpement local

### Backend

Depuis la racine :

```powershell
dotnet restore Backend/Backend.csproj
dotnet build Backend/Backend.csproj
dotnet run --project Backend
```

L'API ecoute par defaut sur `http://localhost:5281`. Les services externes requis doivent etre disponibles avant le lancement du backend. Le fichier `Backend/appsettings.Development.json` contient actuellement uniquement la configuration de journalisation ; les parametres d'infrastructure peuvent etre fournis par variables d'environnement ou par configuration locale.


## Tests

### Backend

```powershell
dotnet test Backend.Tests/Backend.Tests.csproj
```

Les tests utilisent xUnit, FluentAssertions, Moq et EF Core InMemory selon les besoins.

## Configuration importante

- `Backend/appsettings.json` et `Backend/appsettings.Development.json` : configuration de l'API
- `Backend/acp-realm.json` : realm Keycloak importe localement
- `Backend/workflow-config.json` : regles evenement-action
- `Backend/dapr.yaml` : lancement Dapr local
- `Backend/docker-compose.yml` : services et variables de l'environnement Docker
Ne commitez pas de secrets reels dans les fichiers de configuration. Utilisez les variables d'environnement ou un gestionnaire de secrets pour les environnements non locaux.

## Persistance et migrations

Le backend utilise Entity Framework Core avec PostgreSQL. Le demarrage initialise la base avec `EnsureCreated()`. Pour faire evoluer le schema de maniere versionnee, creez une migration :

```powershell
dotnet ef migrations add NomDeLaMigration --project Backend
```

Verifiez ensuite la strategie d'application des migrations avant un deploiement, car le demarrage standard n'applique pas automatiquement les migrations EF Core.

## Contribution

1. Creez une branche de travail.
2. Modifiez le module concerne en respectant les conventions existantes.
3. Ajoutez ou adaptez les tests necessaires.
4. Lancez les builds et tests backend.
5. Verifiez que les changements de configuration ne contiennent aucun secret.

